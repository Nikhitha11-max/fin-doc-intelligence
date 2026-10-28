using FinDoc.Domain.Entities;

namespace FinDoc.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
}

public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
}

public interface IDocumentStore
{
    Task<TenantMembership?> GetMembershipAsync(string userId, CancellationToken ct);
    Task<Document?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Document> Items, int Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken ct);
    void Add(Document document);
    void AddAudit(AuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken ct);
}

public class ForbiddenException(string message = "Access denied.") : Exception(message);
public class NotFoundException(string message = "Not found.") : Exception(message);
public class ValidationFailedException(string message) : Exception(message);
