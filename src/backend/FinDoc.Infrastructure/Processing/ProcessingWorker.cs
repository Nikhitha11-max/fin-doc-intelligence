using FinDoc.Application.Processing;
using FinDoc.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinDoc.Infrastructure.Processing;

public class ProcessingWorker(
    IServiceScopeFactory scopes,
    ProcessingOptions options,
    WorkerHeartbeatReporter heartbeat,
    ILogger<ProcessingWorker> logger) : BackgroundService
{
    public const string WorkerName = "processing";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = false;
            using var logScope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "worker-" + Guid.NewGuid().ToString("N") });
            try
            {
                await heartbeat.BeatAsync(WorkerName, stoppingToken);
                using var scope = scopes.CreateScope();
                didWork = await scope.ServiceProvider.GetRequiredService<ProcessingService>().ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                // Only the exception type is logged: messages from parsers or the database may echo document content.
                logger.LogError("Document processing loop failed ({ErrorType}).", e.GetType().Name);
            }

            if (!didWork)
            {
                try { await Task.Delay(options.PollInterval, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
