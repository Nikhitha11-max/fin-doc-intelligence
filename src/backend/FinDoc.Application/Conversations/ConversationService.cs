using FinDoc.Application.Abstractions;
using FinDoc.Application.Answers;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Conversations;

public sealed class ConversationService(
    IDocumentStore documents,
    IConversationStore conversations,
    ICurrentUser user,
    HistoryOptions options)
{
    private static readonly TenantRole[] Readers =
        [TenantRole.TenantAdmin, TenantRole.DocumentManager, TenantRole.Analyst];

    public async Task<ConversationPage> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(ct);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var (items, total) = await conversations.ListAsync(
            caller.TenantId, caller.UserId, skip, pageSize, DateTimeOffset.UtcNow, ct);
        return new ConversationPage(items, total, page, pageSize);
    }

    public async Task<ConversationDto> GetAsync(Guid id, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(ct);
        var snapshot = await conversations.GetAsync(caller.TenantId, caller.UserId, id, DateTimeOffset.UtcNow, ct)
            ?? throw new NotFoundException("Conversation not found.");

        var messages = snapshot.Messages.Select(message =>
        {
            var text = message.Role == "assistant"
                && message.CitationCount > 0
                && message.Citations.Count != message.CitationCount
                    ? "This answer is hidden because some source documents are no longer available."
                    : message.Text;
            return new ConversationMessageDto(message.Id, message.Role, text, message.CreatedAt, message.Citations);
        }).ToList();
        return new ConversationDto(snapshot.Id, snapshot.Title, snapshot.CreatedAt, snapshot.UpdatedAt, messages);
    }

    public async Task EnsureCanAppendAsync(Guid id, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(ct);
        if (!await conversations.CanAppendAsync(caller.TenantId, caller.UserId, id, DateTimeOffset.UtcNow, ct))
            throw new NotFoundException("Conversation not found.");
    }

    public async Task<Guid> SaveExchangeAsync(Guid? id, string question, AnswerResponse answer, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(ct);
        var conversationId = await conversations.SaveExchangeAsync(
            caller.TenantId, caller.UserId, id, question, answer, options.RetentionDays, ct);
        return conversationId ?? throw new NotFoundException("Conversation not found.");
    }

    private async Task<(Guid TenantId, string UserId)> AuthorizeAsync(CancellationToken ct)
    {
        if (user.UserId is null)
            throw new ForbiddenException();
        var membership = await documents.GetMembershipAsync(user.UserId, ct);
        if (membership is null || !Readers.Contains(membership.Role))
            throw new ForbiddenException();
        return (membership.TenantId, user.UserId);
    }
}

public sealed class ConversationCleanupService(IConversationStore conversations)
{
    public Task<int> CleanupExpiredAsync(CancellationToken ct) =>
        conversations.DeleteExpiredAsync(DateTimeOffset.UtcNow, ct);
}
