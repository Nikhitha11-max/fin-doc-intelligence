using FinDoc.Application.Abstractions;
using FinDoc.Application.Conversations;
using FinDoc.Application.Search;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Answers;

public sealed class AnswerService(
    IDocumentStore documents,
    ICurrentUser user,
    SearchService search,
    PromptBuilder promptBuilder,
    IAnswerProvider provider,
    CitationValidator citationValidator,
    IAnswerAuditStore audit,
    ConversationService conversations,
    AnswerOptions options)
{
    private static readonly TenantRole[] Readers =
        [TenantRole.TenantAdmin, TenantRole.DocumentManager, TenantRole.Analyst];

    public async Task<AnswerResponse> AnswerAsync(AskRequest request, CancellationToken ct)
    {
        var caller = await AuthorizeAsync(ct);
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new ValidationFailedException("A question is required.");
        var question = request.Question.Trim();
        if (question.Length > options.MaxQuestionLength)
            throw new ValidationFailedException($"The question must be {options.MaxQuestionLength} characters or fewer.");
        if (request.ConversationId is Guid conversationId)
            await conversations.EnsureCanAppendAsync(conversationId, ct);

        var response = await search.SearchAsync(new SearchRequest(question, options.MaxPassages), ct);
        var retrieved = response.Results
            .Where(result => result.Score >= options.MinimumRelevanceScore)
            .Take(options.MaxPassages)
            .ToList();
        if (retrieved.Count == 0)
        {
            await audit.RecordAsync(caller.TenantId, caller.UserId, [], ct);
            return await SaveConversationAsync(request.ConversationId, question, InsufficientEvidence(), ct);
        }

        AnswerProviderResult result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(options.Timeout);
            try
            {
                result = await provider.AnswerAsync(promptBuilder.Build(question, retrieved), timeout.Token);
            }
            catch (AnswerProviderException)
            {
                await audit.RecordAsync(caller.TenantId, caller.UserId, [], ct);
                throw;
            }
            catch (TimeoutException)
            {
                await audit.RecordAsync(caller.TenantId, caller.UserId, [], ct);
                throw new AnswerProviderException();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await audit.RecordAsync(caller.TenantId, caller.UserId, [], ct);
                throw new AnswerProviderException();
            }
        }

        var citations = citationValidator.Validate(result.CitationNumbers, retrieved);
        if (citations.Count == 0 || string.IsNullOrWhiteSpace(result.Answer))
        {
            await audit.RecordAsync(caller.TenantId, caller.UserId, [], ct);
            return await SaveConversationAsync(request.ConversationId, question, InsufficientEvidence(), ct);
        }

        var answer = result.Answer.Length <= options.MaxAnswerLength
            ? result.Answer
            : result.Answer[..options.MaxAnswerLength];
        await audit.RecordAsync(caller.TenantId, caller.UserId, citations.Select(citation => citation.DocumentId).Distinct().ToList(), ct);
        return await SaveConversationAsync(request.ConversationId, question, new AnswerResponse(answer, false, citations), ct);
    }

    private async Task<AnswerResponse> SaveConversationAsync(
        Guid? conversationId,
        string question,
        AnswerResponse answer,
        CancellationToken ct)
    {
        var savedId = await conversations.SaveExchangeAsync(conversationId, question, answer, ct);
        return answer with { ConversationId = savedId };
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

    private static AnswerResponse InsufficientEvidence() =>
        new(AnswerMessages.InsufficientEvidence, true, []);
}
