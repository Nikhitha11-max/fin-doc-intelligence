using FinDoc.Application.Abstractions;
using FinDoc.Application.Access;
using FinDoc.Application.Documents;
using FinDoc.Application.Processing;
using FinDoc.Application.Search;
using FinDoc.Infrastructure.Embeddings;
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
            o.UseNpgsql(config.GetConnectionString("Default"), npgsql => npgsql.UseVector()));
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

        var embeddings = new EmbeddingOptions();
        config.GetSection("Embeddings").Bind(embeddings);
        if (!string.Equals(embeddings.Provider, "Fake", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the local Fake embedding provider is currently supported.");
        if (embeddings.Dimensions != EmbeddingOptions.SchemaDimensions)
            throw new InvalidOperationException($"Embeddings:Dimensions must be {EmbeddingOptions.SchemaDimensions} for the current database schema.");
        if (embeddings.BatchSize < 1 || embeddings.MaxQueryLength < 1
            || embeddings.MaxResults is < 1 or > 20 || embeddings.MaxAttempts < 1
            || embeddings.Timeout <= TimeSpan.Zero || embeddings.Lease <= TimeSpan.Zero
            || embeddings.PollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("The Embeddings configuration contains invalid limits or timeouts.");
        services.AddSingleton(embeddings);
        services.AddSingleton<IEmbeddingProvider, FakeEmbeddingProvider>();
        services.AddScoped<IIndexingStore, IndexingStore>();
        services.AddScoped<ISearchStore, SearchStore>();
        services.AddScoped<IndexingService>();
        services.AddScoped<SearchService>();
        if (config.GetValue("Embeddings:WorkerEnabled", true))
            services.AddHostedService<IndexingWorker>();

        services.AddScoped<MemberService>();
        services.AddScoped<PlatformTenantService>();
        return services;
    }
}
