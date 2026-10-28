using FinDoc.Application.Abstractions;
using FinDoc.Application.Documents;
using FinDoc.Infrastructure.Persistence;
using FinDoc.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinDoc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("Default")));
        services.AddScoped<IDocumentStore, DocumentStore>();
        services.AddSingleton<IFileStorage>(_ =>
            new LocalFileStorage(config["Storage:RootPath"] ?? "storage"));
        services.AddScoped<DocumentService>();
        return services;
    }
}
