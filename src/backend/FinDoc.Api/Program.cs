using FinDoc.Api;
using FinDoc.Application.Abstractions;
using FinDoc.Infrastructure;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>(o => o.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await DevSeeder.SeedAsync(app.Services, app.Configuration["Seed:Password"]);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>();
app.MapControllers();

app.Run();

public partial class Program;
