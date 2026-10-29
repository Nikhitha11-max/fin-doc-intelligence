using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Infrastructure.Persistence;

public class AccessStore(AppDbContext db) : IAccessStore
{
    public async Task<T> InTenantLockAsync<T>(Guid tenantId, Func<Task<T>> work, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tenants\" WHERE \"Id\" = {tenantId} FOR UPDATE", ct);
        var result = await work();
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<(TenantMembership Membership, Tenant Tenant)?> GetCallerAsync(string userId, CancellationToken ct)
    {
        var row = await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Tenants.AsNoTracking(), m => m.TenantId, t => t.Id, (m, t) => new { m, t })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : (row.m, row.t);
    }

    public async Task<(IReadOnlyList<MemberInfo> Items, int Total)> ListMembersAsync(Guid tenantId, int skip, int take, CancellationToken ct)
    {
        var query = db.Memberships.AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m, u.Email });
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Email).ThenBy(x => x.m.UserId).Skip(skip).Take(take).ToListAsync(ct);
        return (rows.Select(x => new MemberInfo(x.m.UserId, x.Email ?? string.Empty, x.m.Role, x.m.CreatedAt)).ToList(), total);
    }

    public Task<TenantMembership?> GetMemberAsync(Guid tenantId, string userId, CancellationToken ct) =>
        db.Memberships.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);

    public Task<int> CountAdminsAsync(Guid tenantId, CancellationToken ct) =>
        db.Memberships.CountAsync(m => m.TenantId == tenantId && m.Role == TenantRole.TenantAdmin, ct);

    public Task<bool> EmailInUseAsync(string email, CancellationToken ct)
    {
        var normalized = email.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct);
    }

    public void AddMembership(TenantMembership membership) => db.Memberships.Add(membership);

    public void RemoveMembership(TenantMembership membership) => db.Memberships.Remove(membership);

    public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken ct) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);

    public async Task<(IReadOnlyList<TenantSummary> Items, int Total)> ListTenantsAsync(int skip, int take, CancellationToken ct)
    {
        var total = await db.Tenants.CountAsync(ct);
        var items = await db.Tenants.AsNoTracking().OrderBy(t => t.Name).ThenBy(t => t.Id).Skip(skip).Take(take)
            .Select(t => new TenantSummary(t.Id, t.Name, t.IsActive, t.CreatedAt)).ToListAsync(ct);
        return (items, total);
    }

    public void AddTenant(Tenant tenant) => db.Tenants.Add(tenant);

    public void AddAudit(AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
