using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using FinDoc.Application.Processing;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Infrastructure.Processing;

public class ProcessingStore(AppDbContext db) : IProcessingStore
{
    private const string SystemActor = "system";

    public async Task<ClaimedDocument?> ClaimNextAsync(DateTimeOffset now, ProcessingOptions options, CancellationToken ct)
    {
        await FailExhaustedAsync(now, options, ct);

        // SKIP LOCKED lets several workers or instances claim different documents without blocking each other.
        const string sql = """
            WITH next AS (
                SELECT "Id" FROM "Documents"
                WHERE "DeletedAt" IS NULL
                  AND ("Status" = 0 OR ("Status" = 1 AND "ProcessingLeaseUntil" < @now))
                ORDER BY "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            UPDATE "Documents" d
            SET "Status" = 1, "ProcessingStartedAt" = @now, "ProcessingLeaseUntil" = @lease,
                "Attempts" = d."Attempts" + 1, "FailureCode" = NULL, "FailureMessage" = NULL
            FROM next
            WHERE d."Id" = next."Id"
            RETURNING d."Id", d."TenantId", d."StorageKey", d."Extension", d."Attempts"
            """;

        var rows = await QueryAsync(sql, [("now", now), ("lease", now + options.Lease)], r =>
            new ClaimedDocument(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetInt32(4)), ct);
        var claim = rows.FirstOrDefault();
        if (claim is null)
            return null;

        db.AuditEvents.Add(SystemAudit(claim.TenantId, claim.Id, "document.processing_started", null));
        await db.SaveChangesAsync(ct);
        return claim;
    }

    public async Task<bool> CompleteAsync(ClaimedDocument claim, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Only the current holder of the claim may finish; a deleted or re-claimed document is left alone.
        var updated = await db.Documents
            .Where(d => d.Id == claim.Id && d.Status == DocumentStatus.Processing && d.Attempts == claim.Attempt && d.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DocumentStatus.Completed)
                .SetProperty(d => d.ProcessingLeaseUntil, (DateTimeOffset?)null)
                .SetProperty(d => d.FailureCode, (string?)null)
                .SetProperty(d => d.FailureMessage, (string?)null), ct);
        if (updated == 0)
            return false;

        await db.Chunks.IgnoreQueryFilters().Where(c => c.DocumentId == claim.Id).ExecuteDeleteAsync(ct);
        db.Chunks.AddRange(chunks);
        db.AuditEvents.Add(SystemAudit(claim.TenantId, claim.Id, "document.processing_completed", null));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }

    public async Task<bool> FailAsync(ClaimedDocument claim, string code, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var message = FailureCodes.MessageFor(code);
        var updated = await db.Documents
            .Where(d => d.Id == claim.Id && d.Status == DocumentStatus.Processing && d.Attempts == claim.Attempt && d.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DocumentStatus.Failed)
                .SetProperty(d => d.ProcessingLeaseUntil, (DateTimeOffset?)null)
                .SetProperty(d => d.FailureCode, code)
                .SetProperty(d => d.FailureMessage, message), ct);
        if (updated == 0)
            return false;

        await db.Chunks.IgnoreQueryFilters().Where(c => c.DocumentId == claim.Id).ExecuteDeleteAsync(ct);
        db.AuditEvents.Add(SystemAudit(claim.TenantId, claim.Id, "document.processing_failed", code));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }

    // A document whose worker keeps dying would otherwise be retried forever.
    private async Task FailExhaustedAsync(DateTimeOffset now, ProcessingOptions options, CancellationToken ct)
    {
        const string sql = """
            UPDATE "Documents"
            SET "Status" = 3, "ProcessingLeaseUntil" = NULL, "FailureCode" = @code, "FailureMessage" = @message
            WHERE "DeletedAt" IS NULL AND "Status" = 1 AND "ProcessingLeaseUntil" < @now AND "Attempts" >= @max
            RETURNING "Id", "TenantId"
            """;
        var failed = await QueryAsync(sql,
            [("code", FailureCodes.Internal), ("message", FailureCodes.MessageFor(FailureCodes.Internal)), ("now", now), ("max", options.MaxAttempts)],
            r => (Id: r.GetGuid(0), TenantId: r.GetGuid(1)), ct);
        if (failed.Count == 0)
            return;

        foreach (var (id, tenantId) in failed)
            db.AuditEvents.Add(SystemAudit(tenantId, id, "document.processing_failed", FailureCodes.Internal));
        await db.SaveChangesAsync(ct);
    }

    private static AuditEvent SystemAudit(Guid tenantId, Guid documentId, string action, string? detail) =>
        new() { TenantId = tenantId, ActorUserId = SystemActor, Action = action, DocumentId = documentId, Detail = detail };

    private async Task<List<T>> QueryAsync<T>(string sql, (string Name, object Value)[] parameters, Func<DbDataReader, T> map, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var (name, value) in parameters)
            {
                var p = command.CreateParameter();
                p.ParameterName = name;
                p.Value = value;
                command.Parameters.Add(p);
            }
            var result = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(map(reader));
            return result;
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }
}
