using FinDoc.Application.Processing;
using FinDoc.Domain.Entities;

namespace FinDoc.Tests.Processing;

public class ProcessingServiceTests
{
    private class RecordingStore : IProcessingStore
    {
        public ClaimedDocument? Next { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), "key", ".csv", 1);
        public List<DocumentChunk>? Completed { get; private set; }
        public string? FailedWith { get; private set; }

        public Task<ClaimedDocument?> ClaimNextAsync(DateTimeOffset now, ProcessingOptions options, CancellationToken ct) => Task.FromResult(Next);

        public Task<bool> CompleteAsync(ClaimedDocument claim, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct)
        {
            Completed = chunks.ToList();
            return Task.FromResult(true);
        }

        public Task<bool> FailAsync(ClaimedDocument claim, string code, CancellationToken ct)
        {
            FailedWith = code;
            return Task.FromResult(true);
        }
    }

    private class StubExtractor(Func<CancellationToken, IReadOnlyList<ExtractedUnit>> action) : IDocumentExtractor
    {
        public string Extension => ".csv";
        public IReadOnlyList<ExtractedUnit> Extract(Stream content, ProcessingOptions options, CancellationToken ct) => action(ct);
    }

    private static ProcessingService Service(RecordingStore store, IDocumentExtractor extractor, ProcessingOptions? options = null)
    {
        var files = new FakeFiles();
        files.Saved["key"] = [1];
        return new ProcessingService(store, files, [extractor], options ?? new ProcessingOptions());
    }

    [Fact]
    public async Task Nothing_to_do_returns_false()
    {
        var store = new RecordingStore { Next = null };
        Assert.False(await Service(store, new StubExtractor(_ => [])).ProcessNextAsync(default));
    }

    [Fact]
    public async Task A_slow_parser_fails_with_timeout()
    {
        var store = new RecordingStore();
        var service = Service(store, new StubExtractor(ct =>
        {
            ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            ct.ThrowIfCancellationRequested();
            return [];
        }), new ProcessingOptions { Timeout = TimeSpan.FromMilliseconds(200) });

        Assert.True(await service.ProcessNextAsync(default));
        Assert.Equal(FailureCodes.Timeout, store.FailedWith);
        Assert.Null(store.Completed);
    }

    [Fact]
    public async Task Unexpected_errors_become_an_internal_failure_without_details()
    {
        var store = new RecordingStore();
        var service = Service(store, new StubExtractor(_ => throw new InvalidOperationException("secret cell value 4111-1111")));

        await service.ProcessNextAsync(default);

        Assert.Equal(FailureCodes.Internal, store.FailedWith);
        Assert.DoesNotContain("4111", FailureCodes.MessageFor(store.FailedWith!));
    }

    [Fact]
    public async Task Shutdown_does_not_mark_the_document_failed()
    {
        var store = new RecordingStore();
        using var stop = new CancellationTokenSource();
        var service = Service(store, new StubExtractor(ct =>
        {
            stop.Cancel();
            ct.ThrowIfCancellationRequested();
            return [];
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ProcessNextAsync(stop.Token));
        Assert.Null(store.FailedWith);
    }

    [Fact]
    public async Task A_file_with_no_chunks_is_a_no_text_failure()
    {
        var store = new RecordingStore();
        await Service(store, new StubExtractor(_ => [new ExtractedUnit("   ")])).ProcessNextAsync(default);
        Assert.Equal(FailureCodes.NoExtractableText, store.FailedWith);
    }
}
