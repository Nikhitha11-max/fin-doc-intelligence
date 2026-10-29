using System.Security.Cryptography;
using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Documents;

public record DocumentDto(Guid Id, string Title, string FileName, string Extension, long SizeBytes, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string Status, string? FailureCode, string? FailureMessage);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record DownloadResult(Stream Content, string FileName);

public class DocumentService(IDocumentStore store, IFileStorage files, ICurrentUser user)
{
    private static readonly TenantRole[] Writers = [TenantRole.TenantAdmin, TenantRole.DocumentManager];
    private static readonly TenantRole[] Readers = [TenantRole.TenantAdmin, TenantRole.DocumentManager, TenantRole.Analyst];

    public async Task<DocumentDto> UploadAsync(string? title, string fileName, Stream content, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(Writers, ct);
        var extension = await UploadValidator.ValidateAsync(fileName, content, ct);

        var displayName = Path.GetFileName(fileName);
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(displayName) : title.Trim();
        if (cleanTitle.Length > 200)
            throw new ValidationFailedException("The title must be 200 characters or fewer.");

        var hash = Convert.ToHexString(await SHA256.HashDataAsync(content, ct));
        content.Position = 0;

        var document = new Document
        {
            TenantId = caller.TenantId,
            Title = cleanTitle,
            OriginalFileName = displayName.Length > 255 ? displayName[..255] : displayName,
            Extension = extension,
            SizeBytes = content.Length,
            Sha256 = hash,
            UploadedByUserId = caller.UserId,
        };
        document.StorageKey = $"{caller.TenantId:N}/{document.Id:N}";

        await files.SaveAsync(document.StorageKey, content, ct);
        store.Add(document);
        Audit(caller, "document.upload", document.Id);
        await store.SaveChangesAsync(ct);
        return ToDto(document);
    }

    public async Task<PagedResult<DocumentDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(Readers, ct);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await store.ListAsync(caller.TenantId, (page - 1) * pageSize, pageSize, ct);
        return new PagedResult<DocumentDto>(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<DocumentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(Readers, ct);
        return ToDto(await FindAsync(caller, id, ct));
    }

    public async Task<DownloadResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(Readers, ct);
        var document = await FindAsync(caller, id, ct);
        var stream = await files.OpenReadAsync(document.StorageKey, ct);
        Audit(caller, "document.download", document.Id);
        await store.SaveChangesAsync(ct);
        return new DownloadResult(stream, document.OriginalFileName);
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, string? title, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(Writers, ct);
        var document = await FindAsync(caller, id, ct);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ValidationFailedException("The title is required and must be 200 characters or fewer.");

        document.Title = title.Trim();
        document.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(caller, "document.update", document.Id);
        await store.SaveChangesAsync(ct);
        return ToDto(document);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(Writers, ct);
        var document = await FindAsync(caller, id, ct);
        document.DeletedAt = DateTimeOffset.UtcNow;
        Audit(caller, "document.delete", document.Id);
        await store.SaveChangesAsync(ct);
    }

    public async Task<DocumentDto> RetryAsync(Guid id, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(Writers, ct);
        var document = await FindAsync(caller, id, ct);
        if (!await store.TryRetryAsync(caller.TenantId, id, ct))
            throw new ConflictException("Only failed documents can be retried.");

        Audit(caller, "document.retry", document.Id);
        await store.SaveChangesAsync(ct);
        var refreshed = await FindAsync(caller, id, ct);
        return ToDto(refreshed);
    }

    private record Caller(string UserId, Guid TenantId);

    private async Task<Caller> AuthorizeAsync(TenantRole[] allowed, CancellationToken ct)
    {
        if (user.UserId is null)
            throw new ForbiddenException();
        var membership = await store.GetMembershipAsync(user.UserId, ct);
        if (membership is null || !allowed.Contains(membership.Role))
            throw new ForbiddenException();
        return new Caller(user.UserId, membership.TenantId);
    }

    // Tenant scoping happens in the query, so other tenants' ids look like missing documents.
    private async Task<Document> FindAsync(Caller caller, Guid id, CancellationToken ct) =>
        await store.GetAsync(caller.TenantId, id, ct) ?? throw new NotFoundException("Document not found.");

    private void Audit(Caller caller, string action, Guid documentId) =>
        store.AddAudit(new AuditEvent { TenantId = caller.TenantId, ActorUserId = caller.UserId, Action = action, DocumentId = documentId });

    private static DocumentDto ToDto(Document d) =>
        new(d.Id, d.Title, d.OriginalFileName, d.Extension, d.SizeBytes, d.CreatedAt, d.UpdatedAt, d.Status.ToString(), d.FailureCode, d.FailureMessage);
}
