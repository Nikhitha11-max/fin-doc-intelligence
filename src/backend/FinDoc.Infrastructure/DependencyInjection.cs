using FinDoc.Application.Abstractions;
using FinDoc.Application.Access;
using FinDoc.Application.Documents;
using FinDoc.Application.Processing;
using FinDoc.Infrastructure.Processing;
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
        services.AddScoped<IAccessStore, AccessStore>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<DocumentService>();

        var processing = new ProcessingOptions();
        config.GetSection("Processing").Bind(processing);
        services.AddSingleton(processing);
        services.AddSingleton<IDocumentExtractor, PdfExtractor>();
        services.AddSingleton<IDocumentExtractor, CsvExtractor>();
        services.AddSingleton<IDocumentExtractor, XlsxExtractor>();
        services.AddScoped<IProcessingStore, ProcessingStore>();
        services.AddScoped<ProcessingService>();
        if (config.GetValue("Processing:WorkerEnabled", true))
            services.AddHostedService<ProcessingWorker>();

        services.AddScoped<MemberService>();
        services.AddScoped<PlatformTenantService>();
        return services;
    }
}
