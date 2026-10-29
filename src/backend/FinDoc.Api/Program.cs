using FinDoc.Api;
using FinDoc.Application.Abstractions;
using FinDoc.Infrastructure;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await DevSeeder.SeedAsync(app.Services, app.Configuration["Seed:Password"]);
}

await PlatformBootstrap.RunAsync(app.Services, app.Configuration["Bootstrap:PlatformAdminEmail"], app.Configuration["Bootstrap:PlatformAdminPassword"]);

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Users are created only by administrators (feature 002), so open self-registration is not exposed.
app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>()
    .AddEndpointFilter(async (context, next) =>
        context.HttpContext.Request.Path.StartsWithSegments("/api/auth/register")
            ? Results.NotFound()
            : await next(context));
app.MapControllers();

app.Run();

public partial class Program;
