using FinDoc.Domain.Entities;

namespace FinDoc.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsPlatformAdmin { get; }
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
public class ConflictException(string message) : Exception(message);

public record MemberInfo(string UserId, string Email, TenantRole Role, DateTimeOffset CreatedAt);

public record TenantSummary(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt);

public interface IUserDirectory
{
    Task<(string? UserId, string? Error)> CreateUserAsync(string email, string password, CancellationToken ct);
    Task DeleteUserAsync(string userId, CancellationToken ct);
}

public interface IAccessStore
{
    // Runs work in one transaction holding a lock on the tenant, so concurrent membership changes are serialized.
    Task<T> InTenantLockAsync<T>(Guid tenantId, Func<Task<T>> work, CancellationToken ct);
    Task<(TenantMembership Membership, Tenant Tenant)?> GetCallerAsync(string userId, CancellationToken ct);
    Task<(IReadOnlyList<MemberInfo> Items, int Total)> ListMembersAsync(Guid tenantId, int skip, int take, CancellationToken ct);
    Task<TenantMembership?> GetMemberAsync(Guid tenantId, string userId, CancellationToken ct);
    Task<int> CountAdminsAsync(Guid tenantId, CancellationToken ct);
    Task<bool> EmailInUseAsync(string email, CancellationToken ct);
    void AddMembership(TenantMembership membership);
    void RemoveMembership(TenantMembership membership);
    Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken ct);
    Task<(IReadOnlyList<TenantSummary> Items, int Total)> ListTenantsAsync(int skip, int take, CancellationToken ct);
    void AddTenant(Tenant tenant);
    void AddAudit(AuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken ct);
}
