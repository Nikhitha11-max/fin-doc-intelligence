using FinDoc.Application.Audit;
using FinDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Infrastructure.Audit;

public sealed class AuditStore(AppDbContext db) : IAuditStore
{
    public async Task<(IReadOnlyList<AuditEventDto> Items, int Total)> ListAsync(
        Guid tenantId,
        AuditQuery query,
        int skip,
        int take,
        CancellationToken ct)
    {
        var events = db.AuditEvents.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (query.From is { } from)
            events = events.Where(item => item.OccurredAt >= from);
        if (query.To is { } to)
            events = events.Where(item => item.OccurredAt <= to);
        if (query.Actor is { } actor)
            events = events.Where(item => item.ActorUserId == actor);
        if (query.Action is { } action)
            events = events.Where(item => item.Action == action);

        var total = await events.CountAsync(ct);
        var items = await events
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(take)
            .Select(item => new AuditEventDto(
                item.Id, item.ActorUserId, item.Action, item.DocumentId, item.TargetUserId, item.Detail, item.OccurredAt))
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        db.AuditEvents.Where(item => item.OccurredAt < cutoff).ExecuteDeleteAsync(ct);
}
