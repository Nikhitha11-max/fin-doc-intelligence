using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinDoc.Application.Processing;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinDoc.Tests.Processing;

// Runs against a throwaway PostgreSQL database derived from FINDOC_TEST_DB, with its own name so it never
// collides with the other integration tests running in parallel.
public abstract class PgFixture(string suffix, bool workerEnabled) : IAsyncLifetime
{
    public const string Pw = "Passw0rd!x";
    public const string PlatformEmail = "root@platform.test";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private readonly string _storage = Path.Combine(Path.GetTempPath(), "findoc-tests-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FINDOC_TEST_DB") ?? "Host=localhost;Database=findoc_test");
        builder.Database += "_" + suffix;
        var name = builder.Database!;
        var conn = builder.ConnectionString;
        builder.Database = "postgres";
        await using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)";
            await cmd.ExecuteNonQueryAsync();
        }

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Default", conn);
            b.UseSetting("Storage:RootPath", _storage);
            b.UseSetting("Bootstrap:PlatformAdminEmail", PlatformEmail);
            b.UseSetting("Bootstrap:PlatformAdminPassword", Pw);
            b.UseSetting("Processing:WorkerEnabled", workerEnabled.ToString());
            b.UseSetting("Processing:PollInterval", "00:00:00.100");
        });
        _ = Factory.Server;
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        if (Directory.Exists(_storage)) Directory.Delete(_storage, true);
        return Task.CompletedTask;
    }
}

public class ManualProcessingFixture() : PgFixture("proc", workerEnabled: false);

public class WorkerProcessingFixture() : PgFixture("worker", workerEnabled: true);

public abstract class ProcessingTestBase(WebApplicationFactory<Program> factory)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected record Tenant(HttpClient Admin, HttpClient Manager, HttpClient Analyst, Guid Id);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    protected async Task<HttpClient> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = PgFixture.Pw });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<Tenant> NewTenantAsync()
    {
        var platform = await LoginAsync(PgFixture.PlatformEmail);
        var adminEmail = $"{Unique("admin")}@t.test";
        var response = await platform.PostAsJsonAsync("/api/platform/tenants", new { name = Unique("Org"), adminEmail, adminPassword = PgFixture.Pw });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var admin = await LoginAsync(adminEmail);
        var managerEmail = $"{Unique("manager")}@t.test";
        var analystEmail = $"{Unique("analyst")}@t.test";
        (await admin.PostAsJsonAsync("/api/members", new { email = managerEmail, password = PgFixture.Pw, role = "DocumentManager" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/members", new { email = analystEmail, password = PgFixture.Pw, role = "Analyst" })).EnsureSuccessStatusCode();
        return new Tenant(admin, await LoginAsync(managerEmail), await LoginAsync(analystEmail), id);
    }

    protected static async Task<JsonElement> UploadAsync(HttpClient client, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", fileName } };
        var response = await client.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected static async Task<JsonElement> GetDocAsync(HttpClient client, Guid id) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/documents/{id}");

    protected async Task<int> DrainAsync()
    {
        var processed = 0;
        while (true)
        {
            using var scope = factory.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ProcessingService>().ProcessNextAsync(default))
                return processed;
            processed++;
        }
    }

    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected Task<List<DocumentChunk>> ChunksAsync(Guid documentId, bool ignoreFilter = false) =>
        WithDbAsync(db => (ignoreFilter ? db.Chunks.IgnoreQueryFilters() : db.Chunks)
            .Where(c => c.DocumentId == documentId).OrderBy(c => c.Ordinal).ToListAsync());

    protected Task<List<AuditEvent>> AuditsAsync(Guid documentId) =>
        WithDbAsync(db => db.AuditEvents.Where(a => a.DocumentId == documentId).OrderBy(a => a.OccurredAt).ToListAsync());
}
