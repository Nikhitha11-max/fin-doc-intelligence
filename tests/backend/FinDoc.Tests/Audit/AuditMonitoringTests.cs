using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinDoc.Application.Audit;
using FinDoc.Application.Observability;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinDoc.Tests.Audit;

public class AuditMonitoringTests : IClassFixture<AuditMonitoringTests.Fixture>
{
    private const string Password = "Passw0rd!x";
    private const string PlatformEmail = "root@platform.test";

    public sealed class Fixture : IAsyncLifetime
    {
        private readonly string _storage = Path.Combine(Path.GetTempPath(), "findoc-audit-tests-" + Guid.NewGuid().ToString("N"));
        public WebApplicationFactory<Program> Factory { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("FINDOC_TEST_DB") ?? "Host=localhost;Database=findoc_test");
            builder.Database += "_audit";
            var database = builder.Database!;
            var connection = builder.ConnectionString;
            builder.Database = "postgres";
            await using (var admin = new NpgsqlConnection(builder.ConnectionString))
            {
                await admin.OpenAsync();
                await using var command = admin.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
                await command.ExecuteNonQueryAsync();
            }

            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            {
                host.UseSetting("ConnectionStrings:Default", connection);
                host.UseSetting("Storage:RootPath", _storage);
                host.UseSetting("Bootstrap:PlatformAdminEmail", PlatformEmail);
                host.UseSetting("Bootstrap:PlatformAdminPassword", Password);
                host.UseSetting("Processing:WorkerEnabled", "false");
                host.UseSetting("Embeddings:WorkerEnabled", "false");
                host.UseSetting("History:WorkerEnabled", "false");
                host.UseSetting("Audit:WorkerEnabled", "false");
            });
            _ = Factory.Server;
        }

        public Task DisposeAsync()
        {
            Factory.Dispose();
            if (Directory.Exists(_storage))
                Directory.Delete(_storage, true);
            return Task.CompletedTask;
        }
    }

    private sealed record Tenant(HttpClient Admin, HttpClient Analyst, HttpClient Auditor, HttpClient Manager, Guid Id);

    private readonly WebApplicationFactory<Program> _factory;

    public AuditMonitoringTests(Fixture fixture) => _factory = fixture.Factory;

    [Fact]
    public async Task Auditor_and_admin_can_list_while_other_roles_and_anonymous_are_denied()
    {
        var tenant = await CreateTenantAsync();
        Assert.Equal(HttpStatusCode.OK, (await tenant.Auditor.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tenant.Admin.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Analyst.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Manager.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/audit")).StatusCode);
    }

    [Fact]
    public async Task Audit_listing_is_tenant_scoped()
    {
        var a = await CreateTenantAsync();
        var b = await CreateTenantAsync();
        var aJson = await (await a.Auditor.GetAsync("/api/audit?pageSize=100")).Content.ReadFromJsonAsync<JsonElement>();
        var bIds = (await (await b.Auditor.GetAsync("/api/audit?pageSize=100")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToHashSet();
        var aIds = aJson.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.NotEmpty(aIds);
        Assert.Empty(aIds.Where(bIds.Contains));
        var dbIds = await WithDbAsync(db => db.AuditEvents.Where(e => e.TenantId == a.Id).Select(e => e.Id).ToListAsync());
        Assert.All(aIds, id => Assert.Contains(id, dbIds));
    }

    [Fact]
    public async Task Audit_filters_paging_and_validation_work()
    {
        var tenant = await CreateTenantAsync();
        var byAction = await (await tenant.Auditor.GetAsync("/api/audit?action=" + AuditActions.MemberAdd))
            .Content.ReadFromJsonAsync<JsonElement>();
        var items = byAction.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(AuditActions.MemberAdd, item.GetProperty("action").GetString()));

        var tomorrow = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var yesterday = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var none = await (await tenant.Auditor.GetAsync($"/api/audit?from={tomorrow}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, none.GetProperty("total").GetInt32());

        var capped = await (await tenant.Auditor.GetAsync("/api/audit?pageSize=100000")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, capped.GetProperty("pageSize").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await tenant.Auditor.GetAsync($"/api/audit?from={tomorrow}&to={yesterday}")).StatusCode);
    }

    [Fact]
    public async Task Audit_has_no_write_endpoints()
    {
        var tenant = await CreateTenantAsync();
        Assert.True((await tenant.Admin.PostAsJsonAsync("/api/audit", new { }))
            .StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
        Assert.True((await tenant.Admin.DeleteAsync("/api/audit")).StatusCode
            is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Member_actions_record_exactly_one_event_without_secrets()
    {
        var tenant = await CreateTenantAsync();
        var email = $"extra-{Guid.NewGuid():N}@tenant.local";
        var created = await tenant.Admin.PostAsJsonAsync("/api/members", new { email, password = Password, role = "Analyst" });
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await tenant.Admin.DeleteAsync($"/api/members/{userId}")).StatusCode);

        var events = await WithDbAsync(db => db.AuditEvents
            .Where(e => e.TenantId == tenant.Id && e.TargetUserId == userId).ToListAsync());
        Assert.Single(events, e => e.Action == AuditActions.MemberAdd);
        Assert.Single(events, e => e.Action == AuditActions.MemberRemove);
        Assert.DoesNotContain(events, e => (e.Detail ?? "").Contains(Password));
    }

    [Fact]
    public async Task Retention_purge_removes_only_expired_events()
    {
        var tenant = await CreateTenantAsync();
        var oldId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.AuditEvents.Add(new AuditEvent
            {
                Id = oldId,
                TenantId = tenant.Id,
                ActorUserId = "old-actor",
                Action = AuditActions.MemberAdd,
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-400),
            });
            await db.SaveChangesAsync();
            return 0;
        });
        var before = await WithDbAsync(db => db.AuditEvents.CountAsync(e => e.TenantId == tenant.Id));

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AuditCleanupService>().CleanupExpiredAsync(default);

        Assert.Equal(0, await WithDbAsync(db => db.AuditEvents.CountAsync(e => e.Id == oldId)));
        Assert.Equal(before - 1, await WithDbAsync(db => db.AuditEvents.CountAsync(e => e.TenantId == tenant.Id)));
    }

    [Fact]
    public async Task Correlation_id_is_echoed_and_unsafe_values_are_replaced()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "trace-123");
        var echoed = await client.SendAsync(request);
        Assert.Equal("trace-123", echoed.Headers.GetValues("X-Correlation-Id").Single());

        var unsafeRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        unsafeRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", "bad id<script>");
        var value = (await client.SendAsync(unsafeRequest)).Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual("bad id<script>", value);
        Assert.Matches("^[A-Za-z0-9._-]{1,64}$", value);

        Assert.True((await client.GetAsync("/health/live")).Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Health_endpoints_report_status_only()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var body = await ready.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Host=", body);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task Diagnostics_are_platform_admin_only()
    {
        var tenant = await CreateTenantAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Admin.GetAsync("/api/platform/diagnostics")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Auditor.GetAsync("/api/platform/diagnostics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/platform/diagnostics")).StatusCode);
        var platform = await LoginAsync(PlatformEmail);
        Assert.Equal(HttpStatusCode.OK, (await platform.GetAsync("/api/platform/diagnostics")).StatusCode);
    }

    [Fact]
    public void Log_redactor_masks_tokens_and_sensitive_values()
    {
        string[] keys = ["password", "apikey", "connectionstring"];
        var text = "Authorization: Bearer abc.def.ghi password=hunter2 apikey=sk-123 ok=visible";
        var redacted = LogRedactor.Redact(text, keys);
        Assert.DoesNotContain("abc.def.ghi", redacted);
        Assert.DoesNotContain("hunter2", redacted);
        Assert.DoesNotContain("sk-123", redacted);
        Assert.Contains("ok=visible", redacted);
    }

    private async Task<Tenant> CreateTenantAsync()
    {
        var platform = await LoginAsync(PlatformEmail);
        var adminEmail = $"admin-{Guid.NewGuid():N}@tenant.local";
        var created = await platform.PostAsJsonAsync("/api/platform/tenants", new
        {
            name = $"Tenant-{Guid.NewGuid():N}",
            adminEmail,
            adminPassword = Password,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tenantId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var admin = await LoginAsync(adminEmail);

        async Task<HttpClient> AddAsync(string role)
        {
            var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@tenant.local";
            var response = await admin.PostAsJsonAsync("/api/members", new { email, password = Password, role });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await LoginAsync(email);
        }

        return new Tenant(admin, await AddAsync("Analyst"), await AddAsync("Auditor"), await AddAsync("DocumentManager"), tenantId);
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
