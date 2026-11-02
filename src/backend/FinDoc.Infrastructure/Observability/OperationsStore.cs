using FinDoc.Application.Observability;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace FinDoc.Infrastructure.Observability;

public sealed class OperationsStore(AppDbContext db) : IOperationsStore
{
    public Task<bool> CanConnectAsync(CancellationToken ct) => db.Database.CanConnectAsync(ct);

    public async Task RecordHeartbeatAsync(string worker, DateTimeOffset now, CancellationToken ct)
    {
        var updated = await db.WorkerHeartbeats
            .Where(item => item.Name == worker)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.LastBeatAt, now), ct);
        if (updated > 0)
            return;
        try
        {
            db.WorkerHeartbeats.Add(new WorkerHeartbeat { Name = worker, LastBeatAt = now });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another instance inserted the row first.
            db.ChangeTracker.Clear();
            await db.WorkerHeartbeats
                .Where(item => item.Name == worker)
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.LastBeatAt, now), ct);
        }
    }

    public async Task<IReadOnlyList<WorkerStatus>> GetHeartbeatsAsync(CancellationToken ct) =>
        await db.WorkerHeartbeats.AsNoTracking()
            .Select(item => new WorkerStatus(item.Name, item.LastBeatAt))
            .ToListAsync(ct);

    public async Task<QueueDepth> GetQueueDepthAsync(CancellationToken ct)
    {
        var processing = await db.Documents.CountAsync(item => item.DeletedAt == null
            && (item.Status == DocumentStatus.Pending || item.Status == DocumentStatus.Processing), ct);
        var indexing = await db.Documents.CountAsync(item => item.DeletedAt == null
            && item.Status == DocumentStatus.Completed
            && (item.IndexStatus == DocumentIndexStatus.NotIndexed || item.IndexStatus == DocumentIndexStatus.Indexing), ct);
        return new QueueDepth(processing, indexing);
    }

    public async Task<IReadOnlyList<FailureCount>> GetFailureCountsAsync(CancellationToken ct)
    {
        var processing = await db.Documents.AsNoTracking()
            .Where(item => item.DeletedAt == null && item.Status == DocumentStatus.Failed)
            .GroupBy(item => item.FailureCode)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        var indexing = await db.Documents.AsNoTracking()
            .Where(item => item.DeletedAt == null && item.IndexStatus == DocumentIndexStatus.IndexFailed)
            .GroupBy(item => item.IndexFailureCode)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        return processing.Select(item => new FailureCount("processing", item.Code ?? "unknown", item.Count))
            .Concat(indexing.Select(item => new FailureCount("indexing", item.Code ?? "unknown", item.Count)))
            .ToList();
    }
}

// Workers call this on every loop; writes are throttled so an idle poll loop does not hit the database each time.
public sealed class WorkerHeartbeatReporter(
    IServiceScopeFactory scopes,
    MonitoringOptions options,
    ILogger<WorkerHeartbeatReporter> logger)
{
    private readonly Dictionary<string, DateTimeOffset> _last = [];
    private readonly object _gate = new();

    public async Task BeatAsync(string worker, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (_last.TryGetValue(worker, out var previous) && now - previous < options.HeartbeatInterval)
                return;
            _last[worker] = now;
        }

        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IOperationsStore>().RecordHeartbeatAsync(worker, now, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            lock (_gate)
                _last.Remove(worker);
            logger.LogWarning("Worker heartbeat failed ({ErrorType}).", exception.GetType().Name);
        }
    }
}

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IOperationsStore>().CanConnectAsync(ct)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}

public sealed class WorkerHealthCheck(
    IServiceScopeFactory scopes,
    ExpectedWorkers expected,
    MonitoringOptions options) : IHealthCheck
{
    private static readonly DateTimeOffset ProcessStart = DateTimeOffset.UtcNow;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (expected.Names.Count == 0)
            return HealthCheckResult.Healthy();
        try
        {
            using var scope = scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IOperationsStore>();
            var now = DateTimeOffset.UtcNow;
            var workers = await DiagnosticsService.EvaluateWorkersAsync(store, expected, options, now, ct);
            // Workers are given one stale window after process start to publish their first heartbeat.
            var inGrace = now - ProcessStart < options.WorkerStaleAfter;
            return workers.Any(worker => worker.Stale && !(inGrace && worker.LastBeatAt is null))
                ? HealthCheckResult.Degraded()
                : HealthCheckResult.Healthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
