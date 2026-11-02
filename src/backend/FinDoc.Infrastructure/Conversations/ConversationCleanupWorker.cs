using FinDoc.Application.Conversations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinDoc.Infrastructure.Conversations;

public sealed class ConversationCleanupWorker(
    IServiceScopeFactory scopes,
    HistoryOptions options,
    ILogger<ConversationCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.CleanupInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var deleted = await scope.ServiceProvider
                    .GetRequiredService<ConversationCleanupService>()
                    .CleanupExpiredAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Removed {ConversationCount} expired conversations.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Conversation cleanup failed ({ErrorType}).", exception.GetType().Name);
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
