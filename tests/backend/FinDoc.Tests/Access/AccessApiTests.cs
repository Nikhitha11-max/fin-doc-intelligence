using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinDoc.Tests.Access;

// Runs against a throwaway PostgreSQL database (set FINDOC_TEST_DB to override the connection string).
public class AccessApiTests : IClassFixture<AccessApiTests.Fixture>
{
    private const string Pw = "Passw0rd!x";
    private const string PlatformEmail = "root@platform.test";

    public class Fixture : IAsyncLifetime
    {
        public WebApplicationFactory<Program> Factory { get; private set; } = null!;
        private string _storage = Path.Combine(Path.GetTempPath(), "findoc-tests-" + Guid.NewGuid().ToString("N"));

        public async Task InitializeAsync()
        {
            var conn = Environment.GetEnvironmentVariable("FINDOC_TEST_DB") ?? "Host=localhost;Database=findoc_test";
            var builder = new NpgsqlConnectionStringBuilder(conn);
            var name = builder.Database!;
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

    private readonly WebApplicationFactory<Program> _factory;

    public AccessApiTests(Fixture fixture) => _factory = fixture.Factory;

    private async Task<HttpClient> LoginAsync(string email, string password = Pw)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<(Guid TenantId, string AdminEmail)> CreateTenantAsync(HttpClient platform)
    {
        var adminEmail = $"{Unique("admin")}@t.test";
        var response = await platform.PostAsJsonAsync("/api/platform/tenants", new { name = Unique("Org"), adminEmail, adminPassword = Pw });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (id, adminEmail);
    }

    private static async Task<string> AddMemberAsync(HttpClient admin, string role, string? email = null)
    {
        email ??= $"{Unique(role.ToLowerInvariant())}@t.test";
        var response = await admin.PostAsJsonAsync("/api/members", new { email, password = Pw, role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;
    }

    private static MultipartFormDataContent Csv()
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("a,b\n1,2\n"u8.ToArray());
        content.Add(file, "file", "data.csv");
        return content;
    }

    [Fact]
    public async Task Team_lifecycle_takes_effect_immediately_on_existing_sessions()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (_, adminEmail) = await CreateTenantAsync(platform);
        var admin = await LoginAsync(adminEmail);

        var analystEmail = $"{Unique("analyst")}@t.test";
        var analystId = await AddMemberAsync(admin, "Analyst", analystEmail);
        var analyst = await LoginAsync(analystEmail);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync("/api/documents", Csv())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync("/api/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsync("/api/documents", Csv())).StatusCode);

        var promote = await admin.PutAsJsonAsync($"/api/members/{analystId}/role", new { role = "DocumentManager" });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await analyst.PostAsync("/api/documents", Csv())).StatusCode);

        var me = await analyst.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("DocumentManager", me.GetProperty("role").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/members/{analystId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync("/api/documents")).StatusCode);
        Assert.False((await analyst.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("hasAccess").GetBoolean());
    }

    [Fact]
    public async Task Deactivating_a_tenant_cuts_access_at_once_and_reactivation_restores_it()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (tenantId, adminEmail) = await CreateTenantAsync(platform);
        var admin = await LoginAsync(adminEmail);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/documents")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await platform.PostAsync($"/api/platform/tenants/{tenantId}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/members")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await platform.PostAsync($"/api/platform/tenants/{tenantId}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/members")).StatusCode);
    }

    [Fact]
    public async Task Platform_and_tenant_roles_are_separate()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (_, adminEmail) = await CreateTenantAsync(platform);
        var admin = await LoginAsync(adminEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await platform.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await platform.GetAsync("/api/members")).StatusCode);
        var platformMe = await platform.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.True(platformMe.GetProperty("isPlatformAdmin").GetBoolean());
        Assert.False(platformMe.GetProperty("hasAccess").GetBoolean());
        Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("isPlatformAdmin").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/platform/tenants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/platform/tenants", new { name = "X", adminEmail = "x@t.test", adminPassword = Pw })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/platform/tenants")).StatusCode);
    }

    [Fact]
    public async Task Tenants_are_isolated_and_a_client_supplied_tenant_id_is_ignored()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (_, emailA) = await CreateTenantAsync(platform);
        var (tenantB, emailB) = await CreateTenantAsync(platform);
        var adminA = await LoginAsync(emailA);
        var adminB = await LoginAsync(emailB);
        var memberOfB = await AddMemberAsync(adminB, "Analyst");

        Assert.Equal(HttpStatusCode.NotFound, (await adminA.DeleteAsync($"/api/members/{memberOfB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminA.PutAsJsonAsync($"/api/members/{memberOfB}/role", new { role = "Auditor" })).StatusCode);

        var email = $"{Unique("sneaky")}@t.test";
        var response = await adminA.PostAsJsonAsync("/api/members", new { email, password = Pw, role = "Analyst", tenantId = tenantB });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var list = await adminB.GetFromJsonAsync<JsonElement>("/api/members?pageSize=100");
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), m => m.GetProperty("email").GetString() == email);
    }

    [Fact]
    public async Task Duplicate_email_in_another_tenant_gives_the_same_answer_as_a_duplicate_in_the_same_tenant()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (_, emailA) = await CreateTenantAsync(platform);
        var (_, emailB) = await CreateTenantAsync(platform);
        var adminA = await LoginAsync(emailA);

        async Task<string> Attempt(string email)
        {
            var r = await adminA.PostAsJsonAsync("/api/members", new { email, password = Pw, role = "Analyst" });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;
        }

        Assert.Equal(await Attempt(emailA), await Attempt(emailB));
    }

    [Fact]
    public async Task Concurrent_demotions_never_leave_a_tenant_without_an_admin()
    {
        var platform = await LoginAsync(PlatformEmail);
        for (var round = 0; round < 5; round++)
        {
            var (tenantId, emailA) = await CreateTenantAsync(platform);
            var adminA = await LoginAsync(emailA);
            var emailB = $"{Unique("adminb")}@t.test";
            var idB = await AddMemberAsync(adminA, "TenantAdmin", emailB);
            var adminB = await LoginAsync(emailB);
            var idA = (await adminA.GetFromJsonAsync<JsonElement>("/api/members?pageSize=100")).GetProperty("items")
                .EnumerateArray().Single(m => m.GetProperty("email").GetString() == emailA).GetProperty("userId").GetString()!;

            var results = await Task.WhenAll(
                adminA.PutAsJsonAsync($"/api/members/{idB}/role", new { role = "Analyst" }),
                adminB.PutAsJsonAsync($"/api/members/{idA}/role", new { role = "Analyst" }));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Memberships.CountAsync(m => m.TenantId == tenantId && m.Role == TenantRole.TenantAdmin) >= 1);
            Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Concurrent_removals_never_leave_a_tenant_without_an_admin()
    {
        var platform = await LoginAsync(PlatformEmail);
        for (var round = 0; round < 5; round++)
        {
            var (tenantId, emailA) = await CreateTenantAsync(platform);
            var adminA = await LoginAsync(emailA);
            var emailB = $"{Unique("adminb")}@t.test";
            var idB = await AddMemberAsync(adminA, "TenantAdmin", emailB);
            var adminB = await LoginAsync(emailB);
            var idA = (await adminA.GetFromJsonAsync<JsonElement>("/api/members?pageSize=100")).GetProperty("items")
                .EnumerateArray().Single(m => m.GetProperty("email").GetString() == emailA).GetProperty("userId").GetString()!;

            await Task.WhenAll(adminA.DeleteAsync($"/api/members/{idB}"), adminB.DeleteAsync($"/api/members/{idA}"));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Memberships.CountAsync(m => m.TenantId == tenantId && m.Role == TenantRole.TenantAdmin));
        }
    }

    [Fact]
    public async Task Last_admin_rules_are_enforced_over_http()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (_, adminEmail) = await CreateTenantAsync(platform);
        var admin = await LoginAsync(adminEmail);
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/members")).GetProperty("items")[0].GetProperty("userId").GetString()!;

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/members/{adminId}/role", new { role = "Analyst" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/members/{adminId}")).StatusCode);
    }

    [Fact]
    public async Task Audit_events_are_written_without_secrets()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (tenantId, adminEmail) = await CreateTenantAsync(platform);
        var admin = await LoginAsync(adminEmail);
        var memberId = await AddMemberAsync(admin, "Auditor");
        await admin.PutAsJsonAsync($"/api/members/{memberId}/role", new { role = "Analyst" });
        await admin.DeleteAsync($"/api/members/{memberId}");
        await platform.PutAsJsonAsync($"/api/platform/tenants/{tenantId}", new { name = Unique("Renamed") });
        await platform.PostAsync($"/api/platform/tenants/{tenantId}/deactivate", null);
        await platform.PostAsync($"/api/platform/tenants/{tenantId}/reactivate", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await db.AuditEvents.Where(a => a.TenantId == tenantId).OrderBy(a => a.OccurredAt).ToListAsync();
        Assert.Equal(
            ["tenant.create", "member.add", "member.add", "member.role_change", "member.remove", "tenant.rename", "tenant.deactivate", "tenant.reactivate"],
            events.Select(a => a.Action));
        Assert.All(events, a => Assert.False(string.IsNullOrEmpty(a.ActorUserId)));
        Assert.DoesNotContain(events, a => (a.Detail ?? "").Contains(Pw));
    }

    [Fact]
    public async Task Removed_members_keep_their_documents_and_audit_records()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (tenantId, adminEmail) = await CreateTenantAsync(platform);
        var admin = await LoginAsync(adminEmail);
        var managerEmail = $"{Unique("mgr")}@t.test";
        var managerId = await AddMemberAsync(admin, "DocumentManager", managerEmail);
        var manager = await LoginAsync(managerEmail);
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsync("/api/documents", Csv())).StatusCode);

        await admin.DeleteAsync($"/api/members/{managerId}");

        var docs = await admin.GetFromJsonAsync<JsonElement>("/api/documents");
        Assert.Equal(1, docs.GetProperty("total").GetInt32());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(await db.AuditEvents.Where(a => a.TenantId == tenantId).ToListAsync(), a => a.ActorUserId == managerId && a.Action == "document.upload");
    }

    [Fact]
    public async Task Weak_password_is_rejected_and_leaves_no_user_behind()
    {
        var platform = await LoginAsync(PlatformEmail);
        var (_, adminEmail) = await CreateTenantAsync(platform);
        var admin = await LoginAsync(adminEmail);
        var email = $"{Unique("weak")}@t.test";
        var response = await admin.PostAsJsonAsync("/api/members", new { email, password = "abc", role = "Analyst" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Bootstrap_is_idempotent_and_self_registered_users_have_no_access()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.PostAsJsonAsync("/api/auth/register", new { email = "x@t.test", password = Pw })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        await FinDoc.Api.PlatformBootstrap.RunAsync(scope.ServiceProvider, PlatformEmail, Pw);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == PlatformEmail));
    }
}
