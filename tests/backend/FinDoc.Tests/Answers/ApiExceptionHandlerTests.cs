using FinDoc.Api;
using FinDoc.Application.Answers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FinDoc.Tests.Answers;

public class ApiExceptionHandlerTests
{
    private sealed class CapturingLogger : ILogger<ApiExceptionHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task Answer_provider_failures_are_safe_and_not_logged()
    {
        var logger = new CapturingLogger();
        var handler = new ApiExceptionHandler(logger);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await handler.TryHandleAsync(context, new AnswerProviderException(), default);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Empty(logger.Messages);
        body.Position = 0;
        using var reader = new StreamReader(body);
        var error = await reader.ReadToEndAsync();
        Assert.Contains("temporarily unavailable", error);
        Assert.DoesNotContain("prompt", error, StringComparison.OrdinalIgnoreCase);
    }
}
