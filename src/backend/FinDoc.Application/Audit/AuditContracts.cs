using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Audit;

public sealed class AuditOptions
{
    public int RetentionDays { get; set; } = 365;
    public int MaxPageSize { get; set; } = 100;
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);
    public bool WorkerEnabled { get; set; } = true;
}

public sealed record AuditQuery(DateTimeOffset? From, DateTimeOffset? To, string? Actor, string? Action);

public sealed record AuditEventDto(
    Guid Id,
    string ActorUserId,
    string Action,
    Guid? DocumentId,
    string? TargetUserId,
    string? Detail,
    DateTimeOffset OccurredAt);

public sealed record AuditPage(IReadOnlyList<AuditEventDto> Items, int Total, int Page, int PageSize);

// Read and retention only: audit rows are never updated or deleted through the application, except by expiry.
public interface IAuditStore
{
    Task<(IReadOnlyList<AuditEventDto> Items, int Total)> ListAsync(
        Guid tenantId, AuditQuery query, int skip, int take, CancellationToken ct);
    Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken ct);
}

public sealed class AuditQueryService(
    IDocumentStore documents,
    IAuditStore audit,
    ICurrentUser user,
    AuditOptions options)
{
    private static readonly TenantRole[] Viewers = [TenantRole.TenantAdmin, TenantRole.Auditor];

    public async Task<AuditPage> ListAsync(AuditQuery query, int page, int pageSize, CancellationToken ct)
    {
        if (user.UserId is null)
            throw new ForbiddenException();
        var membership = await documents.GetMembershipAsync(user.UserId, ct);
        if (membership is null || !Viewers.Contains(membership.Role))
            throw new ForbiddenException();

        if (query.From > query.To)
            throw new ValidationFailedException("The start date must not be after the end date.");
        if (query.Actor?.Length > 450 || query.Action?.Length > 100)
            throw new ValidationFailedException("A filter value is too long.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var normalized = query with
        {
            Actor = string.IsNullOrWhiteSpace(query.Actor) ? null : query.Actor.Trim(),
            Action = string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim(),
        };
        var (items, total) = await audit.ListAsync(membership.TenantId, normalized, skip, pageSize, ct);
        return new AuditPage(items, total, page, pageSize);
    }
}

public sealed class AuditCleanupService(IAuditStore audit, AuditOptions options)
{
    public Task<int> CleanupExpiredAsync(CancellationToken ct) =>
        audit.DeleteExpiredAsync(DateTimeOffset.UtcNow.AddDays(-options.RetentionDays), ct);
}
