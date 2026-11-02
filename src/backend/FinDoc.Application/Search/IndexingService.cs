namespace FinDoc.Application.Search;

public sealed class IndexingService(
    IIndexingStore store,
    IEmbeddingProvider provider,
    EmbeddingOptions options)
{
    public async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        var claim = await store.ClaimNextAsync(DateTimeOffset.UtcNow, options, stoppingToken);
        if (claim is null)
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            var passages = await store.GetPassagesAsync(claim, timeout.Token);
            if (passages.Count == 0)
                throw new EmbeddingException(IndexFailureCodes.NoPassages);

            var embeddings = new List<NewChunkEmbedding>(passages.Count);
            foreach (var batch in passages.Chunk(Math.Max(1, options.BatchSize)))
            {
                timeout.Token.ThrowIfCancellationRequested();
                var vectors = await provider.EmbedAsync(batch.Select(p => p.Text).ToArray(), timeout.Token);
                if (vectors.Count != batch.Length || vectors.Any(v => v.Length != options.Dimensions))
                    throw new EmbeddingException(IndexFailureCodes.InvalidConfig);

                for (var i = 0; i < batch.Length; i++)
                {
                    var passage = batch[i];
                    embeddings.Add(new NewChunkEmbedding(
                        passage.ChunkId, passage.TenantId, passage.DocumentId, vectors[i], provider.Model));
                }
            }

            await store.CompleteAsync(claim, embeddings, stoppingToken);
        }
        catch (EmbeddingException e)
        {
            await store.FailAsync(claim, e.Code, stoppingToken);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            await store.FailAsync(claim, IndexFailureCodes.ProviderUnavailable, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            await store.FailAsync(claim, IndexFailureCodes.Internal, stoppingToken);
        }

        return true;
    }
}
