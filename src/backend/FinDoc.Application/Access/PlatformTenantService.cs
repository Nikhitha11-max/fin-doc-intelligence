using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Access;

public class PlatformTenantService(IAccessStore store, MemberService members, ICurrentUser user)
{
    public async Task<PagedResultDto<TenantSummary>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        RequirePlatformAdmin();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MemberService.MaxPageSize);
        var (items, total) = await store.ListTenantsAsync((page - 1) * pageSize, pageSize, ct);
        return new PagedResultDto<TenantSummary>(items, total, page, pageSize);
    }

    public async Task<TenantSummary> CreateAsync(string? name, string? adminEmail, string? adminPassword, CancellationToken ct)
    {
        var actor = RequirePlatformAdmin();
        var clean = CleanName(name);
        var tenant = new Tenant { Name = clean };
        // The tenant and its first administrator are saved together so a failed admin never leaves an empty tenant.
        await members.CreateMemberAsync(actor, tenant.Id, adminEmail, adminPassword, TenantRole.TenantAdmin, ct, tenant);
        return ToSummary(tenant);
    }

    public async Task<TenantSummary> RenameAsync(Guid id, string? name, CancellationToken ct)
    {
        var actor = RequirePlatformAdmin();
        var tenant = await FindAsync(id, ct);
        tenant.Name = CleanName(name);
        Audit(tenant.Id, actor, "tenant.rename");
        await store.SaveChangesAsync(ct);
        return ToSummary(tenant);
    }

    public async Task<TenantSummary> SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var actor = RequirePlatformAdmin();
        var tenant = await FindAsync(id, ct);
        if (tenant.IsActive != active)
        {
            tenant.IsActive = active;
            tenant.DeactivatedAt = active ? null : DateTimeOffset.UtcNow;
            Audit(tenant.Id, actor, active ? "tenant.reactivate" : "tenant.deactivate");
            await store.SaveChangesAsync(ct);
        }
        return ToSummary(tenant);
    }

    public async Task<MemberInfo> CreateAdminAsync(Guid id, string? email, string? password, CancellationToken ct)
    {
        var actor = RequirePlatformAdmin();
        var tenant = await FindAsync(id, ct);
        return await members.CreateMemberAsync(actor, tenant.Id, email, password, TenantRole.TenantAdmin, ct);
    }

    private string RequirePlatformAdmin()
    {
        if (user.UserId is null || !user.IsPlatformAdmin)
            throw new ForbiddenException();
        return user.UserId;
    }

    private static string CleanName(string? name)
    {
        var clean = name?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > 200)
            throw new ValidationFailedException("The organization name is required and must be 200 characters or fewer.");
        return clean;
    }

    private async Task<Tenant> FindAsync(Guid id, CancellationToken ct) =>
        await store.GetTenantAsync(id, ct) ?? throw new NotFoundException("Organization not found.");

    private void Audit(Guid tenantId, string actor, string action) =>
        store.AddAudit(new AuditEvent { TenantId = tenantId, ActorUserId = actor, Action = action });

    private static TenantSummary ToSummary(Tenant t) => new(t.Id, t.Name, t.IsActive, t.CreatedAt);
}
