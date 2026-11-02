using FinDoc.Application.Answers;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;

namespace FinDoc.Infrastructure.Answers;

public sealed class AnswerAuditStore(AppDbContext db) : IAnswerAuditStore
{
    public async Task RecordAsync(
        Guid tenantId,
        string actorUserId,
        IReadOnlyList<Guid> citedDocumentIds,
        CancellationToken ct)
    {
        var documentIds = citedDocumentIds.Distinct().Select(id => id.ToString("N")).ToArray();
        db.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            Action = "answer.question",
            Detail = documentIds.Length == 0 ? null : string.Join(',', documentIds),
        });
        await db.SaveChangesAsync(ct);
    }
}
