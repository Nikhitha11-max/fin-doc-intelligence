using System.Data;
using System.Data.Common;
using FinDoc.Application.Search;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pgvector;

namespace FinDoc.Infrastructure.Embeddings;

public sealed class IndexingStore(AppDbContext db) : IIndexingStore
{
    private const string SystemActor = "system";

    public async Task<IndexClaim?> ClaimNextAsync(DateTimeOffset now, EmbeddingOptions options, CancellationToken ct)
    {
        await FailExhaustedAsync(now, options, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        const string sql = """
            WITH next AS (
                SELECT "Id" FROM "Documents"
                WHERE "DeletedAt" IS NULL AND "Status" = 2
                  AND ("IndexStatus" = 0 OR ("IndexStatus" = 1 AND "IndexingLeaseUntil" < @now))
                ORDER BY "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            UPDATE "Documents" d
            SET "IndexStatus" = 1, "IndexingLeaseUntil" = @lease,
                "IndexingAttempts" = d."IndexingAttempts" + 1,
                "IndexFailureCode" = NULL, "IndexFailureMessage" = NULL
            FROM next
            WHERE d."Id" = next."Id"
            RETURNING d."Id", d."TenantId", d."IndexingAttempts"
            """;

        var claims = await QueryAsync(sql, [("now", now), ("lease", now + options.Lease)],
            reader => new IndexClaim(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2)), ct);
        var claim = claims.FirstOrDefault();
        if (claim is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }

        db.AuditEvents.Add(SystemAudit(claim.TenantId, claim.DocumentId, "document.indexing_started", null));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return claim;
    }

    public async Task<IReadOnlyList<PassageForIndexing>> GetPassagesAsync(IndexClaim claim, CancellationToken ct) =>
        await (
            from chunk in db.Chunks
            join document in db.Documents on chunk.DocumentId equals document.Id
            where chunk.TenantId == claim.TenantId
                && document.TenantId == claim.TenantId
                && document.Id == claim.DocumentId
                && document.Status == DocumentStatus.Completed
                && document.IndexStatus == DocumentIndexStatus.Indexing
                && document.IndexingAttempts == claim.Attempt
                && document.DeletedAt == null
            orderby chunk.Ordinal
            select new PassageForIndexing(chunk.Id, chunk.TenantId, chunk.DocumentId, chunk.Text))
            .ToListAsync(ct);

    public async Task<bool> CompleteAsync(IndexClaim claim, IReadOnlyList<NewChunkEmbedding> embeddings, CancellationToken ct)
    {
        if (embeddings.Any(e => e.TenantId != claim.TenantId || e.DocumentId != claim.DocumentId
                || e.Vector.Length != EmbeddingOptions.SchemaDimensions)
            || embeddings.Select(e => e.ChunkId).Distinct().Count() != embeddings.Count)
            throw new InvalidOperationException("The indexing result does not match the document claim.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Documents
            .Where(d => d.Id == claim.DocumentId && d.TenantId == claim.TenantId
                && d.Status == DocumentStatus.Completed && d.IndexStatus == DocumentIndexStatus.Indexing
                && d.IndexingAttempts == claim.Attempt && d.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.IndexStatus, DocumentIndexStatus.Indexed)
                .SetProperty(d => d.IndexingLeaseUntil, (DateTimeOffset?)null)
                .SetProperty(d => d.IndexFailureCode, (string?)null)
                .SetProperty(d => d.IndexFailureMessage, (string?)null), ct);
        if (updated == 0)
            return false;

        await db.ChunkEmbeddings.IgnoreQueryFilters()
            .Where(e => e.TenantId == claim.TenantId && e.DocumentId == claim.DocumentId)
            .ExecuteDeleteAsync(ct);
        db.ChunkEmbeddings.AddRange(embeddings.Select(e => new ChunkEmbedding
        {
            ChunkId = e.ChunkId,
            TenantId = e.TenantId,
            DocumentId = e.DocumentId,
            Vector = new Vector(e.Vector),
            Model = e.Model,
        }));
        db.AuditEvents.Add(SystemAudit(claim.TenantId, claim.DocumentId, "document.indexing_completed", null));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }

    public async Task<bool> FailAsync(IndexClaim claim, string code, CancellationToken ct)
    {
        var safeCode = code is IndexFailureCodes.ProviderUnavailable or IndexFailureCodes.RateLimited
            or IndexFailureCodes.InvalidConfig or IndexFailureCodes.NoPassages or IndexFailureCodes.TooLarge
            ? code
            : IndexFailureCodes.Internal;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Documents
            .Where(d => d.Id == claim.DocumentId && d.TenantId == claim.TenantId
                && d.Status == DocumentStatus.Completed && d.IndexStatus == DocumentIndexStatus.Indexing
                && d.IndexingAttempts == claim.Attempt && d.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.IndexStatus, DocumentIndexStatus.IndexFailed)
                .SetProperty(d => d.IndexingLeaseUntil, (DateTimeOffset?)null)
                .SetProperty(d => d.IndexFailureCode, safeCode)
                .SetProperty(d => d.IndexFailureMessage, IndexFailureCodes.MessageFor(safeCode)), ct);
        if (updated == 0)
            return false;

        await db.ChunkEmbeddings.IgnoreQueryFilters()
            .Where(e => e.TenantId == claim.TenantId && e.DocumentId == claim.DocumentId)
            .ExecuteDeleteAsync(ct);
        db.AuditEvents.Add(SystemAudit(claim.TenantId, claim.DocumentId, "document.indexing_failed", safeCode));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }

    private async Task FailExhaustedAsync(DateTimeOffset now, EmbeddingOptions options, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        const string sql = """
            UPDATE "Documents"
            SET "IndexStatus" = 3, "IndexingLeaseUntil" = NULL,
                "IndexFailureCode" = @code, "IndexFailureMessage" = @message
            WHERE "DeletedAt" IS NULL AND "Status" = 2 AND "IndexStatus" = 1
              AND "IndexingLeaseUntil" < @now AND "IndexingAttempts" >= @max
            RETURNING "Id", "TenantId"
            """;
        var failed = await QueryAsync(sql,
            [("code", IndexFailureCodes.Internal), ("message", IndexFailureCodes.MessageFor(IndexFailureCodes.Internal)),
                ("now", now), ("max", options.MaxAttempts)],
            reader => (DocumentId: reader.GetGuid(0), TenantId: reader.GetGuid(1)), ct);
        if (failed.Count == 0)
        {
            await tx.CommitAsync(ct);
            return;
        }

        foreach (var item in failed)
        {
            await db.ChunkEmbeddings.IgnoreQueryFilters()
                .Where(e => e.TenantId == item.TenantId && e.DocumentId == item.DocumentId)
                .ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(SystemAudit(item.TenantId, item.DocumentId, "document.indexing_failed", IndexFailureCodes.Internal));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static AuditEvent SystemAudit(Guid tenantId, Guid documentId, string action, string? detail) =>
        new() { TenantId = tenantId, ActorUserId = SystemActor, Action = action, DocumentId = documentId, Detail = detail };

    private async Task<List<T>> QueryAsync<T>(
        string sql,
        (string Name, object Value)[] parameters,
        Func<DbDataReader, T> map,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
            await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            var result = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(map(reader));
            return result;
        }
        finally
        {
            if (wasClosed)
                await connection.CloseAsync();
        }
    }
}
