using FinDoc.Application.Search;

namespace FinDoc.Tests.Search;

public class FakeEmbeddingProviderTests
{
    [Fact]
    public async Task Fake_vectors_are_deterministic_normalized_and_lexically_related()
    {
        var provider = new FakeEmbeddingProvider(new EmbeddingOptions());

        var vectors = await provider.EmbedAsync(
            ["revenue increased during quarter", "quarter during revenue increased", "cash flow obligations"],
            default);

        Assert.Equal(EmbeddingOptions.SchemaDimensions, vectors[0].Length);
        Assert.Equal(vectors[0], vectors[1]);
        Assert.NotEqual(vectors[0], vectors[2]);
        Assert.InRange(MathF.Sqrt(vectors[0].Sum(x => x * x)), 0.999f, 1.001f);
        Assert.Equal("fake-token-hash-v1", provider.Model);
    }

    [Fact]
    public async Task Fake_vectors_do_not_use_a_configured_dimension_different_from_the_schema()
    {
        var provider = new FakeEmbeddingProvider(new EmbeddingOptions { Dimensions = 384 });

        var error = await Assert.ThrowsAsync<EmbeddingException>(() => provider.EmbedAsync(["text"], default));

        Assert.Equal(IndexFailureCodes.InvalidConfig, error.Code);
    }
}
