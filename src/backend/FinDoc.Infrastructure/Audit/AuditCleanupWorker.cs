using FinDoc.Application.Audit;
using FinDoc.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinDoc.Infrastructure.Audit;

public sealed class AuditCleanupWorker(
    IServiceScopeFactory scopes,
    AuditOptions options,
    WorkerHeartbeatReporter heartbeat,
    ILogger<AuditCleanupWorker> logger) : BackgroundService
{
    public const string WorkerName = "audit-cleanup";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.CleanupInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "worker-" + Guid.NewGuid().ToString("N") });
            try
            {
                await heartbeat.BeatAsync(WorkerName, stoppingToken);
                using var serviceScope = scopes.CreateScope();
                var deleted = await serviceScope.ServiceProvider
                    .GetRequiredService<AuditCleanupService>()
                    .CleanupExpiredAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Removed {AuditEventCount} expired audit events.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Audit cleanup failed ({ErrorType}).", exception.GetType().Name);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
