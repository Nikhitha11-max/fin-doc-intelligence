using FinDoc.Api;
using FinDoc.Api.Logging;
using FinDoc.Api.Middleware;
using FinDoc.Application.Abstractions;
using FinDoc.Infrastructure;
using FinDoc.Infrastructure.Observability;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.FormatterName = RedactingJsonConsoleFormatter.FormatterName);
builder.Services.AddSingleton<ConsoleFormatter, RedactingJsonConsoleFormatter>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<WorkerHealthCheck>("workers", tags: ["ready"]);

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var loginLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitsPerMinute", 10);
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path.StartsWithSegments("/api/auth")
            ? RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("none"));
});
builder.Services.Configure<IdentityOptions>(o =>
{
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    o.Lockout.AllowedForNewUsers = true;
});
builder.Services.AddIdentityApiEndpoints<IdentityUser>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!context.Request.Path.StartsWithSegments("/swagger"))
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseExceptionHandler();
app.UseMiddleware<RequestMetricsMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await DevSeeder.SeedAsync(app.Services, app.Configuration["Seed:Password"]);
}

await PlatformBootstrap.RunAsync(app.Services, app.Configuration["Bootstrap:PlatformAdminEmail"], app.Configuration["Bootstrap:PlatformAdminPassword"]);

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Users are created only by administrators (feature 002), so open self-registration is not exposed.
app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>()
    .AddEndpointFilter(async (context, next) =>
        context.HttpContext.Request.Path.StartsWithSegments("/api/auth/register")
            ? Results.NotFound()
            : await next(context));
app.MapControllers();

// Status only: no exception text, connection details or per-tenant data is exposed.
Task WriteHealthAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
    }));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealthAsync });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthAsync,
});

app.Run();

public partial class Program;
