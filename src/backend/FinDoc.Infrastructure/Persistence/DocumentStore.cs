using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Infrastructure.Persistence;

public class DocumentStore(AppDbContext db) : IDocumentStore
{
    public Task<TenantMembership?> GetMembershipAsync(string userId, CancellationToken ct) =>
        db.Memberships.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == userId, ct);

    public Task<Document?> GetAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Documents.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id && d.DeletedAt == null, ct);

    public async Task<(IReadOnlyList<Document> Items, int Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking().Where(d => d.TenantId == tenantId && d.DeletedAt == null);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public void Add(Document document) => db.Documents.Add(document);

    public void AddAudit(AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
