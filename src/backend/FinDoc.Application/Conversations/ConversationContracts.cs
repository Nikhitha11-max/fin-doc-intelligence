using FinDoc.Application.Answers;

namespace FinDoc.Application.Conversations;

public sealed class HistoryOptions
{
    public int RetentionDays { get; set; } = 90;
    public int MaxPageSize { get; set; } = 100;
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    public bool WorkerEnabled { get; set; } = true;
}

public sealed record ConversationListItem(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);

public sealed record ConversationMessageRecord(
    Guid Id,
    string Role,
    string Text,
    DateTimeOffset CreatedAt,
    int CitationCount,
    IReadOnlyList<AnswerCitation> Citations);

public sealed record ConversationSnapshot(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ConversationMessageRecord> Messages);

public sealed record ConversationMessageDto(
    Guid Id,
    string Role,
    string Text,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AnswerCitation> Citations);

public sealed record ConversationDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ConversationMessageDto> Messages);

public sealed record ConversationPage(
    IReadOnlyList<ConversationListItem> Items,
    int Total,
    int Page,
    int PageSize);

public interface IConversationStore
{
    Task<(IReadOnlyList<ConversationListItem> Items, int Total)> ListAsync(
        Guid tenantId, string ownerUserId, int skip, int take, DateTimeOffset now, CancellationToken ct);
    Task<ConversationSnapshot?> GetAsync(Guid tenantId, string ownerUserId, Guid id, DateTimeOffset now, CancellationToken ct);
    Task<bool> CanAppendAsync(Guid tenantId, string ownerUserId, Guid id, DateTimeOffset now, CancellationToken ct);
    Task<Guid?> SaveExchangeAsync(
        Guid tenantId,
        string ownerUserId,
        Guid? conversationId,
        string question,
        AnswerResponse answer,
        int retentionDays,
        CancellationToken ct);
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct);
}
