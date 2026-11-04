using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using FinDoc.Application.Search;
using FinDoc.Application.Answers;
using FinDoc.Application.Conversations;
using FinDoc.Application.Processing;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FinDoc.Tests.Search;

public class SearchApiTests : IClassFixture<SearchApiTests.Fixture>
{
    private const string Password = "Passw0rd!x";
    private const string PlatformEmail = "root@platform.test";

    public sealed class CapturingAnswerProvider : IAnswerProvider
    {
        public AnswerProviderRequest? LastRequest { get; private set; }
        public int Calls { get; private set; }
        public bool FailWithSensitiveTimeout { get; set; }

        public void Reset()
        {
            LastRequest = null;
            Calls = 0;
            FailWithSensitiveTimeout = false;
        }

        public Task<AnswerProviderResult> AnswerAsync(AnswerProviderRequest request, CancellationToken ct)
        {
            LastRequest = request;
            Calls++;
            if (FailWithSensitiveTimeout)
                throw new TimeoutException("private passage: sensitive-revenue-text");
            return Task.FromResult(new AnswerProviderResult("Captured answer.", [1]));
        }
    }

    public sealed class Fixture : IAsyncLifetime
    {
        private readonly string _storage = Path.Combine(Path.GetTempPath(), "findoc-search-tests-" + Guid.NewGuid().ToString("N"));
        public WebApplicationFactory<Program> Factory { get; private set; } = null!;
        public CapturingAnswerProvider AnswerProvider { get; } = new();

        public async Task InitializeAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("FINDOC_TEST_DB") ?? "Host=localhost;Database=findoc_test");
            builder.Database += "_search";
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
                host.UseSetting("RateLimiting:LoginPermitsPerMinute", "100000");
                host.UseSetting("Processing:WorkerEnabled", "false");
                host.UseSetting("Embeddings:WorkerEnabled", "false");
                host.UseSetting("History:WorkerEnabled", "false");
                host.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAnswerProvider>();
                    services.AddSingleton<IAnswerProvider>(AnswerProvider);
                });
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

    private sealed record Tenant(HttpClient Admin, HttpClient Analyst, HttpClient Auditor, Guid Id);

    private readonly Fixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;

    public SearchApiTests(Fixture fixture)
    {
        _fixture = fixture;
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Completed_documents_are_indexed_once_and_search_returns_source_metadata()
    {
        var tenant = await CreateTenantAsync();
        var documentIds = new[]
        {
            await UploadCsvAsync(tenant.Admin, "liquidity.csv", "liquidity,covenant,maturity"),
            await UploadCsvAsync(tenant.Admin, "costs.csv", "operating,costs,declined"),
            await UploadCsvAsync(tenant.Admin, "debt.csv", "debt,refinancing,quarter"),
        };
        await DrainProcessingAsync();
        await DrainIndexingAsync();

        var documents = await DocumentsAsync(documentIds);
        Assert.All(documents, document => Assert.Equal(DocumentIndexStatus.Indexed, document.IndexStatus));
        var chunks = await ChunksAsync(documentIds);
        var embeddings = await EmbeddingsAsync(documentIds);
        Assert.Equal(chunks.Count, embeddings.Count);
        Assert.Equal(chunks.Select(c => c.Id).Order(), embeddings.Select(e => e.ChunkId).Order());
        Assert.All(embeddings, embedding =>
        {
            Assert.Equal(tenant.Id, embedding.TenantId);
            Assert.Equal(EmbeddingOptions.SchemaDimensions, embedding.Vector.Memory.Length);
        });
        var indexAudits = await AuditEventsAsync(documentIds[0]);
        Assert.Contains(indexAudits, audit => audit.Action == "document.indexing_started" && audit.ActorUserId == "system");
        Assert.Contains(indexAudits, audit => audit.Action == "document.indexing_completed" && audit.ActorUserId == "system");

        var response = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query = "liquidity covenant maturity", limit = 5 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray().ToList();
        Assert.NotEmpty(results);
        Assert.Equal(documentIds[0], results[0].GetProperty("documentId").GetGuid());
        Assert.Equal("liquidity", results[0].GetProperty("documentTitle").GetString());
        Assert.Equal(0, results[0].GetProperty("ordinal").GetInt32());
        Assert.Equal(1, results[0].GetProperty("rowStart").GetInt32());
        Assert.Equal(1, results[0].GetProperty("rowEnd").GetInt32());
        Assert.True(results[0].GetProperty("score").GetDouble() > 0.9);
        Assert.Contains("liquidity", results[0].GetProperty("snippet").GetString());
    }

    [Fact]
    public async Task Search_is_tenant_scoped_and_deleted_documents_are_excluded()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenantA.Admin, "private.csv", "quasar,restricted,forecast");
        await DrainProcessingAsync();
        await DrainIndexingAsync();

        var otherTenant = await tenantB.Analyst.PostAsJsonAsync("/api/search", new { query = "quasar restricted forecast" });
        Assert.Empty((await otherTenant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await tenantA.Admin.DeleteAsync($"/api/documents/{documentId}")).StatusCode);
        var deleted = await tenantA.Analyst.PostAsJsonAsync("/api/search", new { query = "quasar restricted forecast" });
        Assert.Empty((await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray());
        Assert.Single(await EmbeddingsAsync([documentId]));
    }

    [Fact]
    public async Task Removed_members_and_auditors_cannot_search()
    {
        var tenant = await CreateTenantAsync();
        var memberResponse = await tenant.Admin.PostAsJsonAsync("/api/members", new
        {
            email = $"temporary-{Guid.NewGuid():N}@test.local",
            password = Password,
            role = "Analyst",
        });
        Assert.Equal(HttpStatusCode.Created, memberResponse.StatusCode);
        var member = await memberResponse.Content.ReadFromJsonAsync<JsonElement>();
        var memberId = member.GetProperty("userId").GetString()!;
        var removedMember = await LoginAsync(member.GetProperty("email").GetString()!);

        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Auditor.PostAsJsonAsync("/api/search", new { query = "report" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await tenant.Admin.DeleteAsync($"/api/members/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await removedMember.PostAsJsonAsync("/api/search", new { query = "report" })).StatusCode);
    }

    [Fact]
    public async Task Reindexing_hides_old_vectors_until_the_atomic_replacement_is_ready()
    {
        var tenant = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenant.Admin, "annual.csv", "earnings,improved,substantially");
        await DrainProcessingAsync();
        await DrainIndexingAsync();

        Assert.Equal(HttpStatusCode.Accepted, (await tenant.Admin.PostAsync($"/api/documents/{documentId}/reindex", null)).StatusCode);
        Assert.Empty((await (await tenant.Analyst.PostAsJsonAsync("/api/search", new { query = "earnings improved" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray());
        Assert.Empty(await EmbeddingsAsync([documentId]));

        await DrainIndexingAsync();
        Assert.Single(await EmbeddingsAsync([documentId]));
        var indexed = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query = "earnings improved" });
        Assert.NotEmpty((await indexed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray());
        Assert.Single((await AuditEventsAsync(documentId)).Where(a => a.Action == "document.reindex"));
    }

    [Fact]
    public async Task Reindex_can_recover_an_index_failed_document()
    {
        var tenant = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenant.Admin, "recover.csv", "recovery,workflow,completed");
        await DrainProcessingAsync();
        await WithDbAsync(async db =>
        {
            await db.Documents.Where(d => d.Id == documentId).ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.IndexStatus, DocumentIndexStatus.IndexFailed)
                .SetProperty(d => d.IndexFailureCode, IndexFailureCodes.ProviderUnavailable)
                .SetProperty(d => d.IndexFailureMessage, IndexFailureCodes.MessageFor(IndexFailureCodes.ProviderUnavailable)));
            return 0;
        });

        Assert.Equal(HttpStatusCode.Accepted, (await tenant.Admin.PostAsync($"/api/documents/{documentId}/reindex", null)).StatusCode);
        var queued = await (await tenant.Admin.GetAsync($"/api/documents/{documentId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NotIndexed", queued.GetProperty("indexStatus").GetString());
        Assert.Null(queued.GetProperty("indexFailureCode").GetString());

        await DrainIndexingAsync();
        var recovered = await (await tenant.Admin.GetAsync($"/api/documents/{documentId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Indexed", recovered.GetProperty("indexStatus").GetString());
        Assert.Null(recovered.GetProperty("indexFailureCode").GetString());
    }

    [Fact]
    public async Task Expired_indexing_leases_are_reclaimed_and_stale_results_are_rejected()
    {
        var tenant = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenant.Admin, "lease.csv", "lease,recovery,content");
        await DrainProcessingAsync();

        IndexClaim oldClaim;
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIndexingStore>();
            oldClaim = (await store.ClaimNextAsync(DateTimeOffset.UtcNow, new EmbeddingOptions { Lease = TimeSpan.FromSeconds(-1) }, default))!;
        }

        IndexClaim newClaim;
        IReadOnlyList<PassageForIndexing> passages;
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIndexingStore>();
            newClaim = (await store.ClaimNextAsync(DateTimeOffset.UtcNow, new EmbeddingOptions(), default))!;
            passages = await store.GetPassagesAsync(newClaim, default);
            Assert.Equal(documentId, newClaim.DocumentId);
            Assert.Equal(oldClaim.Attempt + 1, newClaim.Attempt);
        }

        var embeddings = (await new FakeEmbeddingProvider(new EmbeddingOptions()).EmbedAsync(passages.Select(p => p.Text).ToArray(), default))
            .Select((vector, i) => new NewChunkEmbedding(passages[i].ChunkId, tenant.Id, documentId, vector, "fake-token-hash-v1"))
            .ToList();
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIndexingStore>();
            Assert.False(await store.CompleteAsync(oldClaim, embeddings, default));
            Assert.True(await store.CompleteAsync(newClaim, embeddings, default));
        }

        Assert.Equal(passages.Count, (await EmbeddingsAsync([documentId])).Count);
        Assert.Equal(2, (await AuditEventsAsync(documentId)).Count(e => e.Action == "document.indexing_started"));
    }

    [Fact]
    public async Task Repeated_worker_crashes_eventually_fail_with_a_safe_reason()
    {
        var tenant = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenant.Admin, "crashes.csv", "unstable,worker,document");
        await DrainProcessingAsync();
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIndexingStore>();
        var options = new EmbeddingOptions { Lease = TimeSpan.FromSeconds(-1), MaxAttempts = 1 };

        Assert.NotNull(await store.ClaimNextAsync(DateTimeOffset.UtcNow, options, default));
        Assert.Null(await store.ClaimNextAsync(DateTimeOffset.UtcNow, options, default));

        var doc = await (await tenant.Admin.GetAsync($"/api/documents/{documentId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IndexFailed", doc.GetProperty("indexStatus").GetString());
        Assert.Equal(IndexFailureCodes.Internal, doc.GetProperty("indexFailureCode").GetString());
        Assert.Equal(IndexFailureCodes.MessageFor(IndexFailureCodes.Internal), doc.GetProperty("indexFailureMessage").GetString());
        Assert.Equal("Completed", doc.GetProperty("status").GetString());
        Assert.Empty(await EmbeddingsAsync([documentId]));
        var failureAudit = (await AuditEventsAsync(documentId)).Single(audit => audit.Action == "document.indexing_failed");
        Assert.Equal("internal", failureAudit.Detail);
    }

    [Fact]
    public async Task Search_validates_query_and_caps_the_result_count()
    {
        var tenant = await CreateTenantAsync();
        var empty = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query = "  " });
        var longQuery = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query = new string('x', 501) });
        var zeroLimit = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query = "report", limit = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longQuery.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zeroLimit.StatusCode);
    }

    [Fact]
    public async Task Search_audit_does_not_store_the_query_or_passage_content()
    {
        var tenant = await CreateTenantAsync();
        var marker = "distinctive-confidential-search-query";
        var documentId = await UploadCsvAsync(tenant.Admin, "query.csv", marker);
        await DrainProcessingAsync();
        await DrainIndexingAsync();
        var response = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query = marker });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audit = (await SearchAuditEventsAsync(tenant.Id)).Single();
        Assert.Equal(tenant.Id, audit.TenantId);
        Assert.NotEqual("system", audit.ActorUserId);
        Assert.Null(audit.Detail);
    }

    [Fact]
    public async Task Answer_returns_only_valid_source_citations_and_audits_without_content()
    {
        var tenant = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenant.Admin, "revenue.csv", "revenue,increased,10,percent");
        await DrainProcessingAsync();
        await DrainIndexingAsync();
        _fixture.AnswerProvider.Reset();

        var response = await tenant.Analyst.PostAsJsonAsync("/api/answers", new { question = "revenue increased 10 percent" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(answer.GetProperty("insufficientEvidence").GetBoolean());
        Assert.Equal("Captured answer.", answer.GetProperty("answer").GetString());
        var citation = Assert.Single(answer.GetProperty("citations").EnumerateArray());
        Assert.Equal(documentId, citation.GetProperty("documentId").GetGuid());
        Assert.Equal(1, citation.GetProperty("rowStart").GetInt32());

        var audit = await AnswerAuditEventsAsync(tenant.Id);
        var eventForDocument = Assert.Single(audit);
        Assert.Null(eventForDocument.DocumentId);
        Assert.NotEqual("system", eventForDocument.ActorUserId);
        Assert.Equal(documentId.ToString("N"), eventForDocument.Detail);
    }

    [Fact]
    public async Task Answer_does_not_return_another_tenants_passages_and_audits_insufficient_evidence()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenantA.Admin, "private.csv", "alpha ultraviolet,confidential,forecast");
        await DrainProcessingAsync();
        await DrainIndexingAsync();

        _fixture.AnswerProvider.Reset();
        var response = await tenantB.Analyst.PostAsJsonAsync("/api/answers", new { question = "ultraviolet confidential forecast" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(answer.GetProperty("insufficientEvidence").GetBoolean());
        Assert.Empty(answer.GetProperty("citations").EnumerateArray());
        Assert.DoesNotContain("ultraviolet", answer.GetProperty("answer").GetString(), StringComparison.OrdinalIgnoreCase);
        var noEvidenceAudit = Assert.Single(await AnswerAuditEventsAsync(tenantB.Id));
        Assert.Null(noEvidenceAudit.DocumentId);
        Assert.Null(noEvidenceAudit.Detail);
        Assert.Equal(0, _fixture.AnswerProvider.Calls);

        var tenantBDocumentId = await UploadCsvAsync(tenantB.Admin, "tenant-b.csv", "beta ultraviolet,confidential,forecast");
        await DrainProcessingAsync();
        await DrainIndexingAsync();

        var tenantBResponse = await tenantB.Analyst.PostAsJsonAsync("/api/answers", new { question = "ultraviolet confidential forecast" });
        Assert.Equal(HttpStatusCode.OK, tenantBResponse.StatusCode);
        Assert.Equal(1, _fixture.AnswerProvider.Calls);
        Assert.Contains("beta", Assert.Single(_fixture.AnswerProvider.LastRequest!.Passages).Source.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alpha", _fixture.AnswerProvider.LastRequest.DelimitedContext, StringComparison.OrdinalIgnoreCase);
        var tenantBAnswer = await tenantBResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(tenantBDocumentId, Assert.Single(tenantBAnswer.GetProperty("citations").EnumerateArray()).GetProperty("documentId").GetGuid());
        Assert.Contains(await AnswerAuditEventsAsync(tenantB.Id), auditEvent =>
            auditEvent.Detail == tenantBDocumentId.ToString("N"));

        _fixture.AnswerProvider.Reset();
        var authorized = await tenantA.Analyst.PostAsJsonAsync("/api/answers", new { question = "ultraviolet confidential forecast" });
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal(1, _fixture.AnswerProvider.Calls);
        Assert.Contains("alpha", Assert.Single(_fixture.AnswerProvider.LastRequest!.Passages).Source.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beta", _fixture.AnswerProvider.LastRequest.DelimitedContext, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await EmbeddingsAsync([documentId]));
    }

    [Fact]
    public async Task Answer_rejects_auditors_and_oversized_questions()
    {
        var tenant = await CreateTenantAsync();

        var forbidden = await tenant.Auditor.PostAsJsonAsync("/api/answers", new { question = "report" });
        var oversized = await tenant.Analyst.PostAsJsonAsync("/api/answers", new { question = new string('q', 1001) });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    [Fact]
    public async Task Provider_failure_returns_a_safe_error_and_audits_without_content()
    {
        var tenant = await CreateTenantAsync();
        await UploadCsvAsync(tenant.Admin, "sensitive.csv", "sensitive-revenue-text,forecast,quarter");
        await DrainProcessingAsync();
        await DrainIndexingAsync();
        _fixture.AnswerProvider.Reset();
        _fixture.AnswerProvider.FailWithSensitiveTimeout = true;

        var response = await tenant.Analyst.PostAsJsonAsync("/api/answers", new { question = "sensitive-revenue-text forecast" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("sensitive-revenue-text", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private passage", body, StringComparison.OrdinalIgnoreCase);
        var audit = Assert.Single(await AnswerAuditEventsAsync(tenant.Id));
        Assert.Null(audit.DocumentId);
        Assert.Null(audit.Detail);
    }

    [Fact]
    public async Task Conversations_append_and_list_only_for_the_owner_with_paging()
    {
        var tenant = await CreateTenantAsync();
        await UploadCsvAsync(tenant.Admin, "revenue.csv", "revenue,increased,10,percent");
        await DrainProcessingAsync();
        await DrainIndexingAsync();
        _fixture.AnswerProvider.Reset();

        var firstAnswer = await tenant.Analyst.PostAsJsonAsync("/api/answers", new { question = "revenue increased" });
        Assert.Equal(HttpStatusCode.OK, firstAnswer.StatusCode);
        var firstConversationId = (await firstAnswer.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid();

        var secondAnswer = await tenant.Analyst.PostAsJsonAsync("/api/answers", new
        {
            question = "revenue 10 percent",
            conversationId = firstConversationId,
        });
        Assert.Equal(HttpStatusCode.OK, secondAnswer.StatusCode);
        Assert.Equal(firstConversationId, (await secondAnswer.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid());

        var newAnswer = await tenant.Analyst.PostAsJsonAsync("/api/answers", new { question = "revenue increased" });
        var newestConversationId = (await newAnswer.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid();
        Assert.NotEqual(firstConversationId, newestConversationId);

        var conversationResponse = await tenant.Analyst.GetAsync($"/api/conversations/{firstConversationId}");
        Assert.Equal(HttpStatusCode.OK, conversationResponse.StatusCode);
        var conversation = await conversationResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, conversation.GetProperty("messages").GetArrayLength());

        var listResponse = await tenant.Analyst.GetAsync("/api/conversations?page=1&pageSize=1");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal(newestConversationId, list.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Single(list.GetProperty("items").EnumerateArray());
        var conversationAudits = await WithDbAsync(db => db.AuditEvents
            .Where(item => item.TenantId == tenant.Id && item.Action.StartsWith("conversation."))
            .ToListAsync());
        Assert.All(conversationAudits, item =>
        {
            Assert.Null(item.Detail);
            Assert.Null(item.DocumentId);
            Assert.Null(item.TargetUserId);
        });

        Assert.Equal(HttpStatusCode.NotFound, (await tenant.Admin.GetAsync($"/api/conversations/{firstConversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await CreateTenantAsync()).Analyst
            .GetAsync($"/api/conversations/{firstConversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await tenant.Analyst
            .DeleteAsync($"/api/conversations/{firstConversationId}")).StatusCode);
    }

    [Fact]
    public async Task Deleted_source_documents_hide_citations_and_redact_saved_answers()
    {
        var tenant = await CreateTenantAsync();
        var documentId = await UploadCsvAsync(tenant.Admin, "revenue.csv", "revenue,increased,10,percent");
        await DrainProcessingAsync();
        await DrainIndexingAsync();
        _fixture.AnswerProvider.Reset();

        var answerResponse = await tenant.Analyst.PostAsJsonAsync("/api/answers", new { question = "revenue increased" });
        var conversationId = (await answerResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid();
        var beforeDelete = await (await tenant.Analyst.GetAsync($"/api/conversations/{conversationId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(beforeDelete.GetProperty("messages")[1].GetProperty("citations").EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await tenant.Admin.DeleteAsync($"/api/documents/{documentId}")).StatusCode);
        var afterDelete = await (await tenant.Analyst.GetAsync($"/api/conversations/{conversationId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var assistantMessage = afterDelete.GetProperty("messages")[1];
        Assert.Empty(assistantMessage.GetProperty("citations").EnumerateArray());
        Assert.Contains("hidden", assistantMessage.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Removing_a_member_revokes_conversation_history_access()
    {
        var tenant = await CreateTenantAsync();
        var memberResponse = await tenant.Admin.PostAsJsonAsync("/api/members", new
        {
            email = $"history-{Guid.NewGuid():N}@test.local",
            password = Password,
            role = "Analyst",
        });
        var member = await memberResponse.Content.ReadFromJsonAsync<JsonElement>();
        var user = await LoginAsync(member.GetProperty("email").GetString()!);
        var answer = await user.PostAsJsonAsync("/api/answers", new { question = "no evidence question" });
        var conversationId = (await answer.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent,
            (await tenant.Admin.DeleteAsync($"/api/members/{member.GetProperty("userId").GetString()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/conversations/{conversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/conversations")).StatusCode);
    }

    [Fact]
    public async Task Expired_conversations_are_hidden_and_cleanup_hard_deletes_their_contents()
    {
        var tenant = await CreateTenantAsync();
        await UploadCsvAsync(tenant.Admin, "revenue.csv", "revenue,increased,10,percent");
        await DrainProcessingAsync();
        await DrainIndexingAsync();
        _fixture.AnswerProvider.Reset();
        var answer = await tenant.Analyst.PostAsJsonAsync("/api/answers", new { question = "revenue increased" });
        var conversationId = (await answer.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid();
        await WithDbAsync(async db =>
        {
            var conversation = await db.Conversations.SingleAsync(item => item.Id == conversationId);
            conversation.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.Equal(HttpStatusCode.NotFound,
            (await tenant.Analyst.GetAsync($"/api/conversations/{conversationId}")).StatusCode);
        var list = await (await tenant.Analyst.GetAsync("/api/conversations")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == conversationId);

        using (var scope = _factory.Services.CreateScope())
        {
            var cleanup = scope.ServiceProvider.GetRequiredService<ConversationCleanupService>();
            Assert.Equal(1, await cleanup.CleanupExpiredAsync(default));
        }
        Assert.Equal(0, await WithDbAsync(db => db.Conversations.CountAsync(item => item.Id == conversationId)));
        Assert.Equal(0, await WithDbAsync(db => db.ConversationMessages.CountAsync(message =>
            message.ConversationId == conversationId)));
        Assert.Equal(0, await WithDbAsync(async db =>
        {
            var messageIds = db.ConversationMessages
                .Where(message => message.ConversationId == conversationId)
                .Select(message => message.Id);
            return await db.ConversationCitations.CountAsync(citation => messageIds.Contains(citation.MessageId));
        }));
    }

    [Fact]
    public async Task Search_over_ten_thousand_passages_completes_under_two_seconds()
    {
        var tenant = await CreateTenantAsync();
        var docId = Guid.NewGuid();
        var passages = Enumerable.Range(0, 10_000)
            .Select(i => i == 5_000
                ? "distinctive performance needle"
                : $"generic financial record {i}")
            .ToArray();
        var vectors = await new FakeEmbeddingProvider(new EmbeddingOptions()).EmbedAsync(passages, default);
        var document = new Document
        {
            Id = docId,
            TenantId = tenant.Id,
            Title = "Performance corpus",
            OriginalFileName = "performance.csv",
            Extension = ".csv",
            SizeBytes = 1,
            Sha256 = new string('A', 64),
            StorageKey = $"{tenant.Id:N}/{docId:N}",
            UploadedByUserId = "system",
            Status = DocumentStatus.Completed,
            IndexStatus = DocumentIndexStatus.Indexed,
        };

        await WithDbAsync(async db =>
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync();
            for (var start = 0; start < passages.Length; start += 500)
            {
                var count = Math.Min(500, passages.Length - start);
                var chunks = new List<DocumentChunk>(count);
                var embeddings = new List<ChunkEmbedding>(count);
                for (var i = start; i < start + count; i++)
                {
                    var chunkId = Guid.NewGuid();
                    chunks.Add(new DocumentChunk
                    {
                        Id = chunkId,
                        TenantId = tenant.Id,
                        DocumentId = docId,
                        Ordinal = i,
                        Text = passages[i],
                        RowStart = i + 1,
                        RowEnd = i + 1,
                    });
                    embeddings.Add(new ChunkEmbedding
                    {
                        ChunkId = chunkId,
                        TenantId = tenant.Id,
                        DocumentId = docId,
                        Vector = new Pgvector.Vector(vectors[i]),
                        Model = "fake-token-hash-v1",
                    });
                }
                db.Chunks.AddRange(chunks);
                db.ChunkEmbeddings.AddRange(embeddings);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
            return 0;
        });

        const string query = "distinctive performance needle";
        var warmup = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query, limit = 1000 });
        Assert.Equal(HttpStatusCode.OK, warmup.StatusCode);
        var timer = Stopwatch.StartNew();
        var response = await tenant.Analyst.PostAsJsonAsync("/api/search", new { query, limit = 1000 });
        timer.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2), $"Search took {timer.Elapsed.TotalMilliseconds:F0} ms.");
        var results = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray().ToList();
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 20, $"The result cap was not applied: {results.Count} results.");
        Assert.Equal(5_000, results[0].GetProperty("ordinal").GetInt32());
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

        var analystEmail = $"analyst-{Guid.NewGuid():N}@tenant.local";
        var analystCreated = await admin.PostAsJsonAsync("/api/members", new
        {
            email = analystEmail,
            password = Password,
            role = "Analyst",
        });
        Assert.Equal(HttpStatusCode.Created, analystCreated.StatusCode);
        var auditorEmail = $"auditor-{Guid.NewGuid():N}@tenant.local";
        var auditorCreated = await admin.PostAsJsonAsync("/api/members", new
        {
            email = auditorEmail,
            password = Password,
            role = "Auditor",
        });
        Assert.Equal(HttpStatusCode.Created, auditorCreated.StatusCode);
        return new Tenant(admin, await LoginAsync(analystEmail), await LoginAsync(auditorEmail), tenantId);
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

    private static async Task<Guid> UploadCsvAsync(HttpClient client, string filename, string csv)
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "file", filename },
        };
        var response = await client.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task DrainProcessingAsync()
    {
        while (true)
        {
            using var scope = _factory.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<FinDoc.Application.Processing.ProcessingService>().ProcessNextAsync(default))
                return;
        }
    }

    private async Task DrainIndexingAsync()
    {
        while (true)
        {
            using var scope = _factory.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<IndexingService>().ProcessNextAsync(default))
                return;
        }
    }

    private Task<List<Document>> DocumentsAsync(Guid[] ids) => WithDbAsync(db =>
        db.Documents.Where(d => ids.Contains(d.Id)).ToListAsync());
    private Task<List<DocumentChunk>> ChunksAsync(Guid[] ids) => WithDbAsync(db =>
        db.Chunks.Where(c => ids.Contains(c.DocumentId)).ToListAsync());
    private Task<List<ChunkEmbedding>> EmbeddingsAsync(Guid[] ids) => WithDbAsync(db =>
        db.ChunkEmbeddings.Where(e => ids.Contains(e.DocumentId)).ToListAsync());
    private Task<List<AuditEvent>> AuditEventsAsync(Guid documentId) => WithDbAsync(db =>
        db.AuditEvents.Where(e => e.DocumentId == documentId).ToListAsync());
    private Task<List<AuditEvent>> SearchAuditEventsAsync(Guid tenantId) => WithDbAsync(db =>
        db.AuditEvents.Where(e => e.TenantId == tenantId && e.Action == "search.query").ToListAsync());
    private Task<List<AuditEvent>> AnswerAuditEventsAsync(Guid tenantId) => WithDbAsync(db =>
        db.AuditEvents.Where(e => e.TenantId == tenantId && e.Action == "answer.question").ToListAsync());

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
