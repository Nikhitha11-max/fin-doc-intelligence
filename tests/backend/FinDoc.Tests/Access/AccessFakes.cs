using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;

namespace FinDoc.Tests.Access;

public class FakeAccessStore : IAccessStore
{
    public List<Tenant> Tenants { get; } = [];
    public List<TenantMembership> Memberships { get; } = [];
    public Dictionary<string, string> Emails { get; } = [];
    public List<AuditEvent> Audits { get; } = [];

    public async Task<T> InTenantLockAsync<T>(Guid tenantId, Func<Task<T>> work, CancellationToken ct) => await work();

    public Task<(TenantMembership Membership, Tenant Tenant)?> GetCallerAsync(string userId, CancellationToken ct)
    {
        var m = Memberships.FirstOrDefault(x => x.UserId == userId);
        var t = m is null ? null : Tenants.FirstOrDefault(x => x.Id == m.TenantId);
        return Task.FromResult<(TenantMembership, Tenant)?>(m is null || t is null ? null : (m, t));
    }

    public Task<(IReadOnlyList<MemberInfo> Items, int Total)> ListMembersAsync(Guid tenantId, int skip, int take, CancellationToken ct)
    {
        var all = Memberships.Where(m => m.TenantId == tenantId)
            .Select(m => new MemberInfo(m.UserId, Emails.GetValueOrDefault(m.UserId, ""), m.Role, m.CreatedAt))
            .OrderBy(m => m.Email).ToList();
        return Task.FromResult<(IReadOnlyList<MemberInfo>, int)>((all.Skip(skip).Take(take).ToList(), all.Count));
    }

    public Task<TenantMembership?> GetMemberAsync(Guid tenantId, string userId, CancellationToken ct) =>
        Task.FromResult(Memberships.FirstOrDefault(m => m.TenantId == tenantId && m.UserId == userId));

    public Task<int> CountAdminsAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult(Memberships.Count(m => m.TenantId == tenantId && m.Role == TenantRole.TenantAdmin));

    public Task<bool> EmailInUseAsync(string email, CancellationToken ct) =>
        Task.FromResult(Emails.Values.Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase)));

    public void AddMembership(TenantMembership membership) => Memberships.Add(membership);
    public void RemoveMembership(TenantMembership membership) => Memberships.Remove(membership);
    public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(Tenants.FirstOrDefault(t => t.Id == tenantId));

    public Task<(IReadOnlyList<TenantSummary> Items, int Total)> ListTenantsAsync(int skip, int take, CancellationToken ct)
    {
        var all = Tenants.OrderBy(t => t.Name).Select(t => new TenantSummary(t.Id, t.Name, t.IsActive, t.CreatedAt)).ToList();
        return Task.FromResult<(IReadOnlyList<TenantSummary>, int)>((all.Skip(skip).Take(take).ToList(), all.Count));
    }

    public void AddTenant(Tenant tenant) => Tenants.Add(tenant);
    public void AddAudit(AuditEvent auditEvent) => Audits.Add(auditEvent);
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

public class FakeUserDirectory(FakeAccessStore store) : IUserDirectory
{
    public Task<(string? UserId, string? Error)> CreateUserAsync(string email, string password, CancellationToken ct)
    {
        if (password.Length < 8)
            return Task.FromResult<(string?, string?)>((null, "Passwords must be at least 8 characters."));
        var id = Guid.NewGuid().ToString("N");
        store.Emails[id] = email;
        return Task.FromResult<(string?, string?)>((id, null));
    }

    public Task DeleteUserAsync(string userId, CancellationToken ct)
    {
        store.Emails.Remove(userId);
        return Task.CompletedTask;
    }
}
