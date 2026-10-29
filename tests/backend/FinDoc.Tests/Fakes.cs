using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;

namespace FinDoc.Tests;

public class FakeUser : ICurrentUser
{
    public string? UserId { get; set; }
    public bool IsPlatformAdmin { get; set; }
}

public class FakeFiles : IFileStorage
{
    public Dictionary<string, byte[]> Saved { get; } = [];

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        Saved[key] = ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream>(new MemoryStream(Saved[key]));
}

public class FakeStore : IDocumentStore
{
    public List<TenantMembership> Memberships { get; } = [];
    public List<Document> Documents { get; } = [];
    public List<AuditEvent> Audits { get; } = [];

    public Task<TenantMembership?> GetMembershipAsync(string userId, CancellationToken ct) =>
        Task.FromResult(Memberships.FirstOrDefault(m => m.UserId == userId));

    public Task<Document?> GetAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(Documents.FirstOrDefault(d => d.TenantId == tenantId && d.Id == id && d.DeletedAt == null));

    public Task<(IReadOnlyList<Document> Items, int Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken ct)
    {
        var all = Documents.Where(d => d.TenantId == tenantId && d.DeletedAt == null).OrderByDescending(d => d.CreatedAt).ToList();
        return Task.FromResult<(IReadOnlyList<Document>, int)>((all.Skip(skip).Take(take).ToList(), all.Count));
    }

    public void Add(Document document) => Documents.Add(document);
    public void AddAudit(AuditEvent auditEvent) => Audits.Add(auditEvent);
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
