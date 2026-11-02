using System.Text.Json;
using FinDoc.Application.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace FinDoc.Api.Logging;

// One JSON object per line. Messages are redacted, correlation ids come from logging scopes,
// and exceptions are reduced to their type name because messages and stack traces may echo user content.
public sealed class RedactingJsonConsoleFormatter(MonitoringOptions options)
    : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "findoc-json";

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
            return;

        string? correlationId = null;
        scopeProvider?.ForEachScope((scope, _) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                foreach (var pair in pairs)
                {
                    if (pair.Key == "CorrelationId")
                        correlationId = pair.Value?.ToString();
                }
            }
        }, 0);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", DateTimeOffset.UtcNow);
            writer.WriteString("level", logEntry.LogLevel.ToString());
            writer.WriteString("category", logEntry.Category);
            writer.WriteString("message", LogRedactor.Redact(message, options.SensitiveKeys));
            if (correlationId is not null)
                writer.WriteString("correlationId", correlationId);
            if (logEntry.Exception is not null)
                writer.WriteString("exceptionType", logEntry.Exception.GetType().Name);
            writer.WriteEndObject();
        }

        textWriter.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }
}
