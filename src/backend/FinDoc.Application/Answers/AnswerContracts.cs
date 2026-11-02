using FinDoc.Application.Search;

namespace FinDoc.Application.Answers;

public sealed class AnswerOptions
{
    public string Provider { get; set; } = "Fake";
    public double MinimumRelevanceScore { get; set; } = 0.1;
    public int MaxQuestionLength { get; set; } = 1000;
    public int MaxPassages { get; set; } = 8;
    public int MaxAnswerLength { get; set; } = 1000;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed record NumberedPassage(int Number, SearchResult Source);

public sealed record AnswerProviderRequest(
    string SystemInstructions,
    string Question,
    IReadOnlyList<NumberedPassage> Passages,
    string DelimitedContext);

public sealed record AnswerProviderResult(string Answer, IReadOnlyList<int> CitationNumbers);

public interface IAnswerProvider
{
    Task<AnswerProviderResult> AnswerAsync(AnswerProviderRequest request, CancellationToken ct);
}

public interface IAnswerAuditStore
{
    Task RecordAsync(Guid tenantId, string actorUserId, IReadOnlyList<Guid> citedDocumentIds, CancellationToken ct);
}

public sealed record AskRequest(string? Question);

public sealed record AnswerCitation(
    Guid DocumentId,
    string DocumentTitle,
    Guid ChunkId,
    int? PageStart,
    int? PageEnd,
    string? SheetName,
    int? RowStart,
    int? RowEnd);

public sealed record AnswerResponse(string Answer, bool InsufficientEvidence, IReadOnlyList<AnswerCitation> Citations);

public static class AnswerMessages
{
    public const string InsufficientEvidence = "I couldn't find enough relevant information in your documents to answer that question.";
}

public sealed class AnswerProviderException() : Exception("The answer provider is temporarily unavailable.");
