using FinDoc.Application.Search;

namespace FinDoc.Tests.Search;

public class IndexingServiceTests
{
    private sealed class Store : IIndexingStore
    {
        public IndexClaim? Claim { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), 1);
        public IReadOnlyList<PassageForIndexing> Passages { get; set; } = [];
        public IReadOnlyList<NewChunkEmbedding>? Completed { get; private set; }
        public string? Failure { get; private set; }

        public Task<IndexClaim?> ClaimNextAsync(DateTimeOffset now, EmbeddingOptions options, CancellationToken ct) =>
            Task.FromResult(Claim);
        public Task<IReadOnlyList<PassageForIndexing>> GetPassagesAsync(IndexClaim claim, CancellationToken ct) =>
            Task.FromResult(Passages);
        public Task<bool> CompleteAsync(IndexClaim claim, IReadOnlyList<NewChunkEmbedding> embeddings, CancellationToken ct)
        {
            Completed = embeddings;
            return Task.FromResult(true);
        }
        public Task<bool> FailAsync(IndexClaim claim, string code, CancellationToken ct)
        {
            Failure = code;
            return Task.FromResult(true);
        }
    }

    private sealed class Provider(Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<float[]>>> embed) : IEmbeddingProvider
    {
        public string Model => "test-provider";
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct) => embed(texts, ct);
    }

    [Fact]
    public async Task Indexing_batches_passages_and_saves_exactly_one_vector_per_passage()
    {
        var tenant = Guid.NewGuid();
        var document = Guid.NewGuid();
        var passages = Enumerable.Range(0, 5)
            .Select(i => new PassageForIndexing(Guid.NewGuid(), tenant, document, $"passage {i}"))
            .ToList();
        var store = new Store { Passages = passages };
        var batches = new List<int>();
        var provider = new Provider((texts, _) =>
        {
            batches.Add(texts.Count);
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[EmbeddingOptions.SchemaDimensions]).ToList());
        });
        var service = new IndexingService(store, provider, new EmbeddingOptions { BatchSize = 2 });

        Assert.True(await service.ProcessNextAsync(default));

        Assert.Equal([2, 2, 1], batches);
        Assert.Equal(passages.Select(p => p.ChunkId), store.Completed!.Select(e => e.ChunkId));
        Assert.All(store.Completed!, embedding =>
        {
            Assert.Equal(tenant, embedding.TenantId);
            Assert.Equal(document, embedding.DocumentId);
            Assert.Equal("test-provider", embedding.Model);
        });
        Assert.Null(store.Failure);
    }

    [Fact]
    public async Task Provider_failure_is_recorded_with_a_safe_code()
    {
        var store = new Store
        {
            Passages = [new PassageForIndexing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "private passage")],
        };
        var provider = new Provider((_, _) => throw new EmbeddingException(IndexFailureCodes.ProviderUnavailable));
        var service = new IndexingService(store, provider, new EmbeddingOptions());

        await service.ProcessNextAsync(default);

        Assert.Equal(IndexFailureCodes.ProviderUnavailable, store.Failure);
        Assert.Null(store.Completed);
        Assert.DoesNotContain("private passage", IndexFailureCodes.MessageFor(store.Failure!));
    }

    [Fact]
    public async Task An_empty_document_fails_without_calling_the_provider()
    {
        var store = new Store { Passages = [] };
        var provider = new Provider((_, _) => throw new InvalidOperationException("must not be called"));
        var service = new IndexingService(store, provider, new EmbeddingOptions());

        await service.ProcessNextAsync(default);

        Assert.Equal(IndexFailureCodes.NoPassages, store.Failure);
    }

    [Fact]
    public async Task A_provider_returning_the_wrong_vector_dimension_fails_safely()
    {
        var store = new Store
        {
            Passages = [new PassageForIndexing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "private passage")],
        };
        var provider = new Provider((_, _) =>
            Task.FromResult<IReadOnlyList<float[]>>([new float[8]]));
        var service = new IndexingService(store, provider, new EmbeddingOptions());

        await service.ProcessNextAsync(default);

        Assert.Equal(IndexFailureCodes.InvalidConfig, store.Failure);
    }
}
