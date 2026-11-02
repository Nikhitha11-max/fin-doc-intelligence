using FinDoc.Application.Answers;
using FinDoc.Application.Conversations;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Infrastructure.Conversations;

public sealed class ConversationStore(AppDbContext db) : IConversationStore
{
    public async Task<(IReadOnlyList<ConversationListItem> Items, int Total)> ListAsync(
        Guid tenantId,
        string ownerUserId,
        int skip,
        int take,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var query = db.Conversations.AsNoTracking()
            .Where(conversation => conversation.TenantId == tenantId
                && conversation.OwnerUserId == ownerUserId
                && conversation.ExpiresAt > now);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ThenBy(conversation => conversation.Id)
            .Skip(skip)
            .Take(take)
            .Select(conversation => new ConversationListItem(
                conversation.Id,
                conversation.Title,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                db.ConversationMessages.Count(message => message.ConversationId == conversation.Id)))
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<ConversationSnapshot?> GetAsync(
        Guid tenantId,
        string ownerUserId,
        Guid id,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var conversation = await db.Conversations.AsNoTracking()
            .Where(item => item.Id == id && item.TenantId == tenantId
                && item.OwnerUserId == ownerUserId && item.ExpiresAt > now)
            .Select(item => new { item.Id, item.Title, item.CreatedAt, item.UpdatedAt })
            .FirstOrDefaultAsync(ct);
        if (conversation is null)
            return null;

        var messages = await db.ConversationMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversation.Id)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(message => new
            {
                message.Id,
                message.Role,
                message.Text,
                message.CreatedAt,
                message.CitationCount,
            })
            .ToListAsync(ct);

        var messageIds = messages.Select(message => message.Id).ToArray();
        var visibleCitations = await (
            from citation in db.ConversationCitations.AsNoTracking()
            join document in db.Documents.AsNoTracking() on citation.DocumentId equals document.Id
            where messageIds.Contains(citation.MessageId)
                && document.TenantId == tenantId
                && document.DeletedAt == null
                && document.Status == DocumentStatus.Completed
            select new
            {
                citation.MessageId,
                Citation = new AnswerCitation(
                    document.Id,
                    document.Title,
                    citation.ChunkId,
                    citation.PageStart,
                    citation.PageEnd,
                    citation.SheetName,
                    citation.RowStart,
                    citation.RowEnd),
            }).ToListAsync(ct);
        var citationsByMessage = visibleCitations
            .GroupBy(item => item.MessageId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AnswerCitation>)group.Select(item => item.Citation).ToList());

        var records = messages.Select(message => new ConversationMessageRecord(
            message.Id,
            message.Role,
            message.Text,
            message.CreatedAt,
            message.CitationCount,
            citationsByMessage.GetValueOrDefault(message.Id, []))).ToList();
        return new ConversationSnapshot(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            records);
    }

    public Task<bool> CanAppendAsync(
        Guid tenantId,
        string ownerUserId,
        Guid id,
        DateTimeOffset now,
        CancellationToken ct) =>
        db.Conversations.AnyAsync(conversation =>
            conversation.Id == id
            && conversation.TenantId == tenantId
            && conversation.OwnerUserId == ownerUserId
            && conversation.ExpiresAt > now, ct);

    public async Task<Guid?> SaveExchangeAsync(
        Guid tenantId,
        string ownerUserId,
        Guid? conversationId,
        string question,
        AnswerResponse answer,
        int retentionDays,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Conversation conversation;
        var isNew = conversationId is null;
        if (conversationId is Guid id)
        {
            var existing = await db.Conversations.FirstOrDefaultAsync(item =>
                    item.Id == id
                    && item.TenantId == tenantId
                    && item.OwnerUserId == ownerUserId
                    && item.ExpiresAt > now, ct);
            if (existing is null)
                return null;
            conversation = existing;
            conversation.UpdatedAt = now;
            conversation.ExpiresAt = now.AddDays(retentionDays);
        }
        else
        {
            conversation = new Conversation
            {
                TenantId = tenantId,
                OwnerUserId = ownerUserId,
                Title = question.Length <= 200 ? question : question[..200],
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = now.AddDays(retentionDays),
            };
            db.Conversations.Add(conversation);
        }

        var userMessage = new ConversationMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Text = question,
            CreatedAt = now,
        };
        var assistantMessage = new ConversationMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Text = answer.Answer,
            CreatedAt = now.AddMilliseconds(1),
            CitationCount = answer.Citations.Count,
        };
        db.ConversationMessages.AddRange(userMessage, assistantMessage);
        db.ConversationCitations.AddRange(answer.Citations.Select(citation => new ConversationCitation
        {
            MessageId = assistantMessage.Id,
            DocumentId = citation.DocumentId,
            ChunkId = citation.ChunkId,
            PageStart = citation.PageStart,
            PageEnd = citation.PageEnd,
            SheetName = citation.SheetName,
            RowStart = citation.RowStart,
            RowEnd = citation.RowEnd,
        }));
        db.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = ownerUserId,
            Action = isNew ? AuditActions.ConversationCreate : AuditActions.ConversationAppend,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return conversation.Id;
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct) =>
        db.Conversations.Where(conversation => conversation.ExpiresAt <= now).ExecuteDeleteAsync(ct);
}
