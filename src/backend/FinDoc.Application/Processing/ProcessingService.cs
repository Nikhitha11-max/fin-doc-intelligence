using FinDoc.Application.Abstractions;

namespace FinDoc.Application.Processing;

public class ProcessingService(IProcessingStore store, IFileStorage files, IEnumerable<IDocumentExtractor> extractors, ProcessingOptions options)
{
    /// <summary>Claims and processes one document. Returns false when nothing was waiting.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        var claim = await store.ClaimNextAsync(DateTimeOffset.UtcNow, options, stoppingToken);
        if (claim is null)
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            var extractor = extractors.FirstOrDefault(e => e.Extension == claim.Extension)
                ?? throw new ExtractionException(FailureCodes.Internal);

            await using var stream = await files.OpenReadAsync(claim.StorageKey, timeout.Token);
            var units = extractor.Extract(stream, options, timeout.Token);
            var chunks = ChunkBuilder.Build(claim.TenantId, claim.Id, units, options);
            if (chunks.Count == 0)
                throw new ExtractionException(FailureCodes.NoExtractableText);

            await store.CompleteAsync(claim, chunks, stoppingToken);
        }
        catch (ExtractionException e)
        {
            await store.FailAsync(claim, e.Code, stoppingToken);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            await store.FailAsync(claim, FailureCodes.Timeout, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Shutting down: the lease expires and the document is picked up again.
        }
        catch (Exception)
        {
            await store.FailAsync(claim, FailureCodes.Internal, stoppingToken);
        }
        return true;
    }
}
