using FinDoc.Application.Abstractions;
using FinDoc.Application.Answers;
using FinDoc.Application.Audit;
using FinDoc.Application.Conversations;
using FinDoc.Application.Observability;
using FinDoc.Application.Access;
using FinDoc.Application.Documents;
using FinDoc.Application.Processing;
using FinDoc.Application.Search;
using FinDoc.Infrastructure.Embeddings;
using FinDoc.Infrastructure.Answers;
using FinDoc.Infrastructure.Audit;
using FinDoc.Infrastructure.Conversations;
using FinDoc.Infrastructure.Observability;
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
        var workers = new List<string>();
        if (config.GetValue("Processing:WorkerEnabled", true))
        {
            services.AddHostedService<ProcessingWorker>();
            workers.Add(ProcessingWorker.WorkerName);
        }

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
        {
            services.AddHostedService<IndexingWorker>();
            workers.Add(IndexingWorker.WorkerName);
        }

        var answers = new AnswerOptions();
        config.GetSection("Answers").Bind(answers);
        if (!string.Equals(answers.Provider, "Fake", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the local Fake answer provider is currently supported.");
        if (!double.IsFinite(answers.MinimumRelevanceScore) || answers.MinimumRelevanceScore is < -1 or > 1
            || answers.MaxQuestionLength is < 1 or > 2000
            || answers.MaxPassages is < 1 or > 8
            || answers.MaxAnswerLength is < 1 or > 4000
            || answers.Timeout <= TimeSpan.Zero || answers.Timeout > TimeSpan.FromMinutes(2))
            throw new InvalidOperationException("The Answers configuration contains invalid limits or timeouts.");
        services.AddSingleton(answers);
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<CitationValidator>();
        services.AddSingleton<IAnswerProvider, FakeAnswerProvider>();
        services.AddScoped<IAnswerAuditStore, AnswerAuditStore>();
        services.AddScoped<IConversationStore, ConversationStore>();
        services.AddScoped<ConversationService>();
        services.AddScoped<AnswerService>();

        var history = new HistoryOptions();
        config.GetSection("History").Bind(history);
        if (history.RetentionDays is < 1 or > 3650
            || history.MaxPageSize is < 1 or > 100
            || history.CleanupInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("The History configuration contains invalid limits or intervals.");
        services.AddSingleton(history);
        services.AddScoped<ConversationCleanupService>();
        if (history.WorkerEnabled)
        {
            services.AddHostedService<ConversationCleanupWorker>();
            workers.Add(ConversationCleanupWorker.WorkerName);
        }

        var auditOptions = new AuditOptions();
        config.GetSection("Audit").Bind(auditOptions);
        if (auditOptions.RetentionDays is < 1 or > 3650
            || auditOptions.MaxPageSize is < 1 or > 100
            || auditOptions.CleanupInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("The Audit configuration contains invalid limits or intervals.");
        services.AddSingleton(auditOptions);
        services.AddScoped<IAuditStore, AuditStore>();
        services.AddScoped<AuditQueryService>();
        services.AddScoped<AuditCleanupService>();
        if (auditOptions.WorkerEnabled)
        {
            services.AddHostedService<AuditCleanupWorker>();
            workers.Add(AuditCleanupWorker.WorkerName);
        }

        var monitoring = new MonitoringOptions();
        config.GetSection("Monitoring").Bind(monitoring);
        if (monitoring.WorkerStaleAfter <= TimeSpan.Zero || monitoring.HeartbeatInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("The Monitoring configuration contains invalid intervals.");
        services.AddSingleton(monitoring);
        services.AddSingleton(new ExpectedWorkers(workers));
        services.AddSingleton<WorkerHeartbeatReporter>();
        services.AddScoped<IOperationsStore, OperationsStore>();
        services.AddScoped<DiagnosticsService>();

        services.AddScoped<MemberService>();
        services.AddScoped<PlatformTenantService>();
        return services;
    }
}
