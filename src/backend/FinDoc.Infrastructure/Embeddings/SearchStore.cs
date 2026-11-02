using FinDoc.Application.Search;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace FinDoc.Infrastructure.Embeddings;

public sealed class SearchStore(AppDbContext db) : ISearchStore
{
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(Guid tenantId, float[] vector, int limit, CancellationToken ct)
    {
        var queryVector = new Vector(vector);
        var matches = await (
            from embedding in db.ChunkEmbeddings
            join chunk in db.Chunks on embedding.ChunkId equals chunk.Id
            join document in db.Documents on chunk.DocumentId equals document.Id
            where embedding.TenantId == tenantId
                && embedding.DocumentId == document.Id
                && chunk.TenantId == tenantId
                && document.TenantId == tenantId
                && document.Status == DocumentStatus.Completed
                && document.IndexStatus == DocumentIndexStatus.Indexed
                && document.DeletedAt == null
            orderby embedding.Vector.CosineDistance(queryVector)
            select new
            {
                DocumentId = document.Id,
                document.Title,
                ChunkId = chunk.Id,
                chunk.Ordinal,
                chunk.PageStart,
                chunk.PageEnd,
                chunk.SheetName,
                chunk.RowStart,
                chunk.RowEnd,
                chunk.Text,
                Distance = embedding.Vector.CosineDistance(queryVector),
            })
            .Take(limit)
            .ToListAsync(ct);

        return matches.Select(m => new SearchHit(
            m.DocumentId, m.Title, m.ChunkId, m.Ordinal, m.PageStart, m.PageEnd, m.SheetName,
            m.RowStart, m.RowEnd, m.Text, 1d - m.Distance)).ToList();
    }

    public async Task AddAuditAsync(Guid tenantId, string actorUserId, CancellationToken ct)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            Action = "search.query",
        });
        await db.SaveChangesAsync(ct);
    }
}
