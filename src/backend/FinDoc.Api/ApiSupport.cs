using System.Security.Claims;
using FinDoc.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api;

public static class PlatformRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsPlatformAdmin => accessor.HttpContext?.User.IsInRole(PlatformRoles.PlatformAdmin) == true;
}

// Maps application exceptions to safe responses; unexpected errors never leak details.
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, detail) = exception switch
        {
            ForbiddenException => (StatusCodes.Status403Forbidden, "Access denied."),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            ValidationFailedException e => (StatusCodes.Status400BadRequest, e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, e.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Detail = detail }, ct);
        return true;
    }
}
