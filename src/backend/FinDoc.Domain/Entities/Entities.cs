namespace FinDoc.Domain.Entities;

public enum TenantRole
{
    TenantAdmin = 1,
    DocumentManager = 2,
    Analyst = 3,
    Auditor = 4,
}

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeactivatedAt { get; set; }
}

public class TenantMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public TenantRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum DocumentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public DateTimeOffset? ProcessingLeaseUntil { get; set; }
    public int Attempts { get; set; }
}

// Derived text; never returned by any API in this feature. Always carries the document's tenant.
public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? PageStart { get; set; }
    public int? PageEnd { get; set; }
    public string? SheetName { get; set; }
    public int? RowStart { get; set; }
    public int? RowEnd { get; set; }
}

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public string? TargetUserId { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
