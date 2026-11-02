using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using FinDoc.Application.Abstractions;

namespace FinDoc.Application.Observability;

public sealed class MonitoringOptions
{
    public TimeSpan WorkerStaleAfter { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
    public string[] SensitiveKeys { get; set; } =
        ["password", "passwd", "secret", "token", "apikey", "api_key", "authorization", "connectionstring", "cookie"];
}

// Counters carry no tenant, user or document identifiers so telemetry can never expose business data.
public static class Telemetry
{
    public const string MeterName = "FinDoc";

    private static readonly Meter Meter = new(MeterName);
    private static long _processingQueue;
    private static long _indexingQueue;

    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("findoc.http.requests");
    public static readonly Counter<long> RequestErrors = Meter.CreateCounter<long>("findoc.http.errors");
    public static readonly Counter<long> ProcessingFailures = Meter.CreateCounter<long>("findoc.processing.failures");
    public static readonly Counter<long> IndexingFailures = Meter.CreateCounter<long>("findoc.indexing.failures");
    public static readonly Counter<long> ProviderFailures = Meter.CreateCounter<long>("findoc.provider.failures");

    static Telemetry()
    {
        Meter.CreateObservableGauge("findoc.queue.depth", () => new[]
        {
            new Measurement<long>(Interlocked.Read(ref _processingQueue), new KeyValuePair<string, object?>("queue", "processing")),
            new Measurement<long>(Interlocked.Read(ref _indexingQueue), new KeyValuePair<string, object?>("queue", "indexing")),
        });
    }

    public static void UpdateQueueDepth(QueueDepth depth)
    {
        Interlocked.Exchange(ref _processingQueue, depth.Processing);
        Interlocked.Exchange(ref _indexingQueue, depth.Indexing);
    }
}

public static partial class LogRedactor
{
    public const string Mask = "[redacted]";

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*")]
    private static partial Regex JwtPattern();

    public static string Redact(string? text, IEnumerable<string> sensitiveKeys)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var result = JwtPattern().Replace(BearerPattern().Replace(text, $"Bearer {Mask}"), Mask);
        foreach (var key in sensitiveKeys)
        {
            var pattern = new Regex(
                $@"(?<key>""?{Regex.Escape(key)}""?\s*[:=]\s*)(""[^""]*""|[^\s,;&]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(50));
            try
            {
                result = pattern.Replace(result, match => match.Groups["key"].Value + Mask);
            }
            catch (RegexMatchTimeoutException)
            {
                return Mask;
            }
        }

        return result;
    }
}

public sealed record QueueDepth(int Processing, int Indexing);

public sealed record WorkerStatus(string Name, DateTimeOffset LastBeatAt);

public sealed record FailureCount(string Stage, string Code, int Count);

public interface IOperationsStore
{
    Task<bool> CanConnectAsync(CancellationToken ct);
    Task RecordHeartbeatAsync(string worker, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<WorkerStatus>> GetHeartbeatsAsync(CancellationToken ct);
    Task<QueueDepth> GetQueueDepthAsync(CancellationToken ct);
    Task<IReadOnlyList<FailureCount>> GetFailureCountsAsync(CancellationToken ct);
}

// Names of the background workers enabled in this process; readiness expects a recent heartbeat from each.
public sealed class ExpectedWorkers(IReadOnlyCollection<string> names)
{
    public IReadOnlyCollection<string> Names { get; } = names;
}

public sealed record WorkerHealth(string Name, DateTimeOffset? LastBeatAt, bool Stale);

public sealed record DiagnosticsDto(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<WorkerHealth> Workers,
    QueueDepth QueueDepth,
    IReadOnlyList<FailureCount> Failures);

public sealed class DiagnosticsService(
    ICurrentUser user,
    IOperationsStore store,
    ExpectedWorkers expected,
    MonitoringOptions options)
{
    public async Task<DiagnosticsDto> GetAsync(CancellationToken ct)
    {
        if (user.UserId is null || !user.IsPlatformAdmin)
            throw new ForbiddenException();

        var now = DateTimeOffset.UtcNow;
        var queue = await store.GetQueueDepthAsync(ct);
        Telemetry.UpdateQueueDepth(queue);
        var workers = await EvaluateWorkersAsync(store, expected, options, now, ct);
        return new DiagnosticsDto(now, workers, queue, await store.GetFailureCountsAsync(ct));
    }

    public static async Task<IReadOnlyList<WorkerHealth>> EvaluateWorkersAsync(
        IOperationsStore store,
        ExpectedWorkers expected,
        MonitoringOptions options,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var beats = (await store.GetHeartbeatsAsync(ct)).ToDictionary(beat => beat.Name, beat => beat.LastBeatAt);
        return expected.Names.Order().Select(name =>
        {
            DateTimeOffset? last = beats.TryGetValue(name, out var value) ? value : null;
            return new WorkerHealth(name, last, last is null || now - last > options.WorkerStaleAfter);
        }).ToList();
    }
}
