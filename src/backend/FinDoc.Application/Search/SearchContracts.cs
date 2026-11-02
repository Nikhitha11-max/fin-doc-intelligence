using FinDoc.Domain.Entities;

namespace FinDoc.Application.Search;

public sealed class EmbeddingOptions
{
    public const int SchemaDimensions = 1536;

    public int Dimensions { get; set; } = SchemaDimensions;
    public int BatchSize { get; set; } = 32;
    public string Provider { get; set; } = "Fake";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public int MaxAttempts { get; set; } = 5;
    public int MaxQueryLength { get; set; } = 500;
    public int MaxResults { get; set; } = 20;
}

public interface IEmbeddingProvider
{
    string Model { get; }
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);
}

public static class IndexFailureCodes
{
    public const string ProviderUnavailable = "provider_unavailable";
    public const string RateLimited = "rate_limited";
    public const string InvalidConfig = "invalid_config";
    public const string NoPassages = "no_passages";
    public const string TooLarge = "too_large";
    public const string Internal = "internal";

    public static string MessageFor(string code) => code switch
    {
        ProviderUnavailable => "The embedding provider is temporarily unavailable.",
        RateLimited => "The embedding provider is temporarily rate-limiting requests.",
        InvalidConfig => "The embedding provider is not configured correctly.",
        NoPassages => "The document has no passages to index.",
        TooLarge => "The document exceeds the indexing limits.",
        _ => "Indexing failed unexpectedly.",
    };
}

public sealed class EmbeddingException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public record IndexClaim(Guid DocumentId, Guid TenantId, int Attempt);

public record PassageForIndexing(Guid ChunkId, Guid TenantId, Guid DocumentId, string Text);

public record NewChunkEmbedding(Guid ChunkId, Guid TenantId, Guid DocumentId, float[] Vector, string Model);

public record SearchHit(
    Guid DocumentId,
    string DocumentTitle,
    Guid ChunkId,
    int Ordinal,
    int? PageStart,
    int? PageEnd,
    string? SheetName,
    int? RowStart,
    int? RowEnd,
    string Text,
    double Score);

public interface IIndexingStore
{
    Task<IndexClaim?> ClaimNextAsync(DateTimeOffset now, EmbeddingOptions options, CancellationToken ct);
    Task<IReadOnlyList<PassageForIndexing>> GetPassagesAsync(IndexClaim claim, CancellationToken ct);
    Task<bool> CompleteAsync(IndexClaim claim, IReadOnlyList<NewChunkEmbedding> embeddings, CancellationToken ct);
    Task<bool> FailAsync(IndexClaim claim, string code, CancellationToken ct);
}

public interface ISearchStore
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(Guid tenantId, float[] vector, int limit, CancellationToken ct);
    Task AddAuditAsync(Guid tenantId, string actorUserId, CancellationToken ct);
}

public record SearchRequest(string? Query, int? Limit);

public record SearchResult(
    Guid DocumentId,
    string DocumentTitle,
    Guid ChunkId,
    int Ordinal,
    int? PageStart,
    int? PageEnd,
    string? SheetName,
    int? RowStart,
    int? RowEnd,
    double Score,
    string Snippet);

public record SearchResponse(IReadOnlyList<SearchResult> Results);
