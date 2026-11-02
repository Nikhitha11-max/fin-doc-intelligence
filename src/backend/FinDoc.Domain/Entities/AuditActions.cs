namespace FinDoc.Domain.Entities;

// Single catalog of audit actions. Events never carry document text, prompts, answers or credentials.
public static class AuditActions
{
    public const string DocumentUpload = "document.upload";
    public const string DocumentDownload = "document.download";
    public const string DocumentUpdate = "document.update";
    public const string DocumentDelete = "document.delete";
    public const string DocumentRetry = "document.retry";
    public const string DocumentReindex = "document.reindex";
    public const string DocumentProcessingStarted = "document.processing_started";
    public const string DocumentProcessingCompleted = "document.processing_completed";
    public const string DocumentProcessingFailed = "document.processing_failed";
    public const string DocumentIndexingStarted = "document.indexing_started";
    public const string DocumentIndexingCompleted = "document.indexing_completed";
    public const string DocumentIndexingFailed = "document.indexing_failed";

    public const string MemberAdd = "member.add";
    public const string MemberRoleChange = "member.role_change";
    public const string MemberRemove = "member.remove";

    public const string TenantCreate = "tenant.create";
    public const string TenantRename = "tenant.rename";
    public const string TenantDeactivate = "tenant.deactivate";
    public const string TenantReactivate = "tenant.reactivate";

    public const string SearchQuery = "search.query";
    public const string AnswerQuestion = "answer.question";
    public const string ConversationCreate = "conversation.create";
    public const string ConversationAppend = "conversation.append";
}
