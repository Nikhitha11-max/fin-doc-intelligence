using FinDoc.Application.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinDoc.Infrastructure.Embeddings;

public sealed class IndexingWorker(
    IServiceScopeFactory scopes,
    EmbeddingOptions options,
    ILogger<IndexingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = false;
            try
            {
                using var scope = scopes.CreateScope();
                didWork = await scope.ServiceProvider.GetRequiredService<IndexingService>().ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError("Document indexing loop failed ({ErrorType}).", e.GetType().Name);
            }

            if (!didWork)
            {
                try { await Task.Delay(options.PollInterval, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
