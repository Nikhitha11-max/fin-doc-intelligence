using System.Text.RegularExpressions;
using FinDoc.Application.Observability;

namespace FinDoc.Api.Middleware;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex ValidId();

    public async Task InvokeAsync(HttpContext context)
    {
        // Client-supplied ids are accepted only in a safe format, so they cannot inject log content.
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var id = supplied is not null && ValidId().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");
        context.Items[ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object> { [ItemKey] = id });
        await next(context);
    }
}

public sealed class RequestMetricsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        finally
        {
            Telemetry.Requests.Add(1);
            if (context.Response.StatusCode >= 500)
                Telemetry.RequestErrors.Add(1);
        }
    }
}
