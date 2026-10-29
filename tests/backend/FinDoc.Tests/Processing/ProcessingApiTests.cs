using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinDoc.Application.Processing;
using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinDoc.Tests.Processing;

public class ProcessingApiTests(ManualProcessingFixture fixture) : ProcessingTestBase(fixture.Factory), IClassFixture<ManualProcessingFixture>
{
    private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

    private async Task<(Tenant Tenant, Guid DocId)> UploadAndProcessAsync(string fileName, byte[] content)
    {
        var tenant = await NewTenantAsync();
        var doc = await UploadAsync(tenant.Admin, fileName, content);
        await DrainAsync();
        return (tenant, doc.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Upload_returns_pending_without_waiting_for_processing()
    {
        var tenant = await NewTenantAsync();
        var doc = await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b"));
        Assert.Equal("Pending", doc.GetProperty("status").GetString());
        Assert.Empty(await ChunksAsync(doc.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Valid_csv_completes_with_ordered_chunks_and_row_ranges()
    {
        var lines = new[] { "name,amount" }.Concat(Enumerable.Range(1, 200).Select(i => $"Customer number {i},{i * 10}")).ToArray();
        var (tenant, id) = await UploadAndProcessAsync("big.csv", TestFiles.Csv(lines));

        Assert.Equal("Completed", (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString());
        var chunks = await ChunksAsync(id);
        Assert.True(chunks.Count > 3);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Ordinal));
        Assert.All(chunks, c => Assert.Equal(tenant.Id, c.TenantId));
        Assert.Equal(1, chunks[0].RowStart);
        Assert.Equal(201, chunks[^1].RowEnd);
        for (var i = 1; i < chunks.Count; i++)
            Assert.Equal(chunks[i - 1].RowEnd + 1, chunks[i].RowStart);
    }

    [Fact]
    public async Task Valid_pdf_completes_with_page_numbers()
    {
        var (tenant, id) = await UploadAndProcessAsync("r.pdf", TestFiles.Pdf("Quarterly revenue grew", "Operating costs fell"));

        Assert.Equal("Completed", (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString());
        var chunks = await ChunksAsync(id);
        Assert.Equal((1, 2), (chunks[0].PageStart, chunks[0].PageEnd));
        Assert.Contains("Quarterly revenue grew", chunks[0].Text);
    }

    [Fact]
    public async Task Valid_xlsx_completes_with_sheet_and_row_ranges()
    {
        var (tenant, id) = await UploadAndProcessAsync("b.xlsx", TestFiles.Xlsx());

        Assert.Equal("Completed", (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString());
        var chunks = await ChunksAsync(id);
        Assert.Equal([("Balance", 1, 3), ("Notes", 1, 1)], chunks.Select(c => (c.SheetName!, c.RowStart!.Value, c.RowEnd!.Value)));
    }

    public static IEnumerable<object[]> BadFiles()
    {
        yield return ["corrupt.pdf", "%PDF-1.7 not really"u8.ToArray(), "corrupted"];
        yield return ["corrupt.xlsx", "PK\u0003\u0004 not really"u8.ToArray(), "corrupted"];
        yield return ["locked.pdf", TestFiles.EncryptedPdf(), "encrypted"];
        yield return ["blank.csv", TestFiles.Csv(" ,, ", ""), "empty"];
        yield return ["blank.xlsx", TestFiles.EmptyXlsx(), "empty"];
        yield return ["scan.pdf", TestFiles.Pdf(""), "no_extractable_text"];
        yield return ["bomb.xlsx", TestFiles.XlsxBomb(), "too_large"];
    }

    [Theory]
    [MemberData(nameof(BadFiles))]
    public async Task Bad_files_fail_with_a_safe_reason(string fileName, byte[] content, string code)
    {
        var (tenant, id) = await UploadAndProcessAsync(fileName, content);

        var doc = await GetDocAsync(tenant.Analyst, id);
        Assert.Equal("Failed", doc.GetProperty("status").GetString());
        Assert.Equal(code, doc.GetProperty("failureCode").GetString());
        Assert.Equal(FailureCodes.MessageFor(code), doc.GetProperty("failureMessage").GetString());
        Assert.Empty(await ChunksAsync(id, ignoreFilter: true));
    }

    [Fact]
    public async Task Status_is_visible_to_analysts_in_the_list()
    {
        var (tenant, id) = await UploadAndProcessAsync("a.csv", TestFiles.Csv("a,b"));

        var list = await tenant.Analyst.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/documents");
        var item = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        Assert.Equal("Completed", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Another_tenant_cannot_see_status_or_retry()
    {
        var (_, id) = await UploadAndProcessAsync("bad.pdf", "%PDF-1.7 nope"u8.ToArray());
        var other = await NewTenantAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.PostAsync($"/api/documents/{id}/retry", null)).StatusCode);
        var list = await other.Admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/documents");
        Assert.Equal(0, list.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Chunks_are_not_exposed_by_any_document_response()
    {
        var (tenant, id) = await UploadAndProcessAsync("a.csv", TestFiles.Csv("secret-marker-123,b"));

        var body = await (await tenant.Admin.GetAsync($"/api/documents/{id}")).Content.ReadAsStringAsync();
        var list = await (await tenant.Admin.GetAsync("/api/documents")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-marker-123", body + list);
        Assert.DoesNotContain("chunk", (body + list).ToLowerInvariant());
    }

    [Fact]
    public async Task Retry_follows_role_rules_and_is_only_for_failed_documents()
    {
        var (tenant, id) = await UploadAndProcessAsync("bad.pdf", "%PDF-1.7 nope"u8.ToArray());

        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Analyst.PostAsync($"/api/documents/{id}/retry", null)).StatusCode);

        var retried = await tenant.Manager.PostAsync($"/api/documents/{id}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        var dto = await retried.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Pending", dto.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("failureCode").ValueKind);

        // A second retry while Pending is rejected rather than queued twice.
        Assert.Equal(HttpStatusCode.Conflict, (await tenant.Admin.PostAsync($"/api/documents/{id}/retry", null)).StatusCode);

        await DrainAsync();
        Assert.Equal("Failed", (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Analyst.PostAsync($"/api/documents/{id}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task Completed_documents_cannot_be_retried()
    {
        var (tenant, id) = await UploadAndProcessAsync("a.csv", TestFiles.Csv("a,b"));
        Assert.Equal(HttpStatusCode.Conflict, (await tenant.Admin.PostAsync($"/api/documents/{id}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task Retry_after_a_failure_leaves_no_stale_or_duplicate_chunks()
    {
        var (tenant, id) = await UploadAndProcessAsync("a.csv", TestFiles.Csv("a,b", "c,d"));
        var first = await ChunksAsync(id);

        // Simulate leftovers from an earlier run, then force a failed state and retry.
        await WithDbAsync(async db =>
        {
            db.Chunks.Add(new DocumentChunk { TenantId = tenant.Id, DocumentId = id, Ordinal = 99, Text = "stale" });
            await db.SaveChangesAsync();
            await db.Documents.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DocumentStatus.Failed));
            return 0;
        });
        Assert.Equal(HttpStatusCode.OK, (await tenant.Admin.PostAsync($"/api/documents/{id}/retry", null)).StatusCode);
        await DrainAsync();

        var again = await ChunksAsync(id);
        Assert.Equal(first.Select(c => (c.Ordinal, c.Text)), again.Select(c => (c.Ordinal, c.Text)));
        Assert.DoesNotContain(again, c => c.Text == "stale");
    }

    [Fact]
    public async Task A_crashed_worker_is_recovered_without_duplicate_chunks()
    {
        var tenant = await NewTenantAsync();
        var id = (await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b", "c,d"))).GetProperty("id").GetGuid();

        // The "crashed" worker claims the document with a lease that is already over, and leaves partial chunks behind.
        await DrainUntilClaimedAsync(id);
        await WithDbAsync(async db =>
        {
            db.Chunks.Add(new DocumentChunk { TenantId = tenant.Id, DocumentId = id, Ordinal = 0, Text = "partial" });
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal("Processing", (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString());

        await DrainAsync();

        Assert.Equal("Completed", (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString());
        var chunks = await ChunksAsync(id);
        Assert.Single(chunks);
        Assert.Contains("a | b", chunks[0].Text);
        var audits = (await AuditsAsync(id)).Select(a => a.Action).ToList();
        Assert.Equal(2, audits.Count(a => a == "document.processing_started"));
    }

    [Fact]
    public async Task A_live_lease_is_not_taken_over()
    {
        var tenant = await NewTenantAsync();
        var id = (await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b"))).GetProperty("id").GetGuid();
        await DrainUntilClaimedAsync(id, lease: TimeSpan.FromMinutes(5));

        await DrainAsync();

        Assert.Equal("Processing", (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_stale_worker_cannot_overwrite_a_newer_claim()
    {
        var tenant = await NewTenantAsync();
        var id = (await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b"))).GetProperty("id").GetGuid();
        var stale = await DrainUntilClaimedAsync(id);
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessingStore>();
        var fresh = (await store.ClaimNextAsync(DateTimeOffset.UtcNow, new ProcessingOptions(), default))!;

        var chunk = new DocumentChunk { TenantId = tenant.Id, DocumentId = id, Ordinal = 0, Text = "stale result" };
        Assert.False(await store.CompleteAsync(stale, [chunk], default));
        Assert.False(await store.FailAsync(stale, FailureCodes.Internal, default));
        Assert.True(await store.CompleteAsync(fresh, [new DocumentChunk { TenantId = tenant.Id, DocumentId = id, Ordinal = 0, Text = "fresh" }], default));

        Assert.Equal(["fresh"], (await ChunksAsync(id)).Select(c => c.Text));
    }

    [Fact]
    public async Task A_document_that_keeps_crashing_workers_eventually_fails()
    {
        var tenant = await NewTenantAsync();
        var id = (await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b"))).GetProperty("id").GetGuid();
        var options = new ProcessingOptions { MaxAttempts = 2, Lease = TimeSpan.FromSeconds(-1) };

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessingStore>();
        await DrainOthersAsync(id);
        Assert.NotNull(await store.ClaimNextAsync(DateTimeOffset.UtcNow, options, default));
        Assert.NotNull(await store.ClaimNextAsync(DateTimeOffset.UtcNow, options, default));
        Assert.Null(await store.ClaimNextAsync(DateTimeOffset.UtcNow, options, default));

        var doc = await GetDocAsync(tenant.Admin, id);
        Assert.Equal("Failed", doc.GetProperty("status").GetString());
        Assert.Equal("internal", doc.GetProperty("failureCode").GetString());
        Assert.Contains("document.processing_failed", (await AuditsAsync(id)).Select(a => a.Action));
    }

    [Fact]
    public async Task Deleted_documents_are_never_processed()
    {
        var tenant = await NewTenantAsync();
        var id = (await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b"))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await tenant.Admin.DeleteAsync($"/api/documents/{id}")).StatusCode);

        await DrainAsync();

        var row = await WithDbAsync(db => db.Documents.SingleAsync(d => d.Id == id));
        Assert.Equal(DocumentStatus.Pending, row.Status);
        Assert.Empty(await ChunksAsync(id, ignoreFilter: true));
        Assert.DoesNotContain("document.processing_started", (await AuditsAsync(id)).Select(a => a.Action));
    }

    [Fact]
    public async Task Deleting_during_processing_discards_the_result_and_hides_chunks()
    {
        var tenant = await NewTenantAsync();
        var id = (await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b"))).GetProperty("id").GetGuid();
        var claim = await DrainUntilClaimedAsync(id, lease: TimeSpan.FromMinutes(5));
        await tenant.Admin.DeleteAsync($"/api/documents/{id}");

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessingStore>();
        Assert.False(await store.CompleteAsync(claim, [new DocumentChunk { TenantId = tenant.Id, DocumentId = id, Text = "x" }], default));
        Assert.Empty(await ChunksAsync(id, ignoreFilter: true));
        Assert.Equal(HttpStatusCode.NotFound, (await tenant.Admin.PostAsync($"/api/documents/{id}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task Chunks_of_a_deleted_document_become_inaccessible()
    {
        var (tenant, id) = await UploadAndProcessAsync("a.csv", TestFiles.Csv("a,b"));
        Assert.Single(await ChunksAsync(id));

        await tenant.Admin.DeleteAsync($"/api/documents/{id}");

        Assert.Empty(await ChunksAsync(id));
        Assert.Single(await ChunksAsync(id, ignoreFilter: true));
    }

    [Fact]
    public async Task Every_state_change_is_audited_without_content()
    {
        var tenant = await NewTenantAsync();
        var id = (await UploadAsync(tenant.Admin, "bad.pdf", "%PDF-1.7 secret-body"u8.ToArray())).GetProperty("id").GetGuid();
        await DrainAsync();
        await tenant.Manager.PostAsync($"/api/documents/{id}/retry", null);
        await DrainAsync();

        var audits = await AuditsAsync(id);
        Assert.Equal(
            ["document.upload", "document.processing_started", "document.processing_failed", "document.retry", "document.processing_started", "document.processing_failed"],
            audits.Select(a => a.Action));
        Assert.All(audits, a => Assert.Equal(tenant.Id, a.TenantId));
        Assert.All(audits.Where(a => a.Action.StartsWith("document.processing")), a => Assert.Equal("system", a.ActorUserId));
        Assert.NotEqual("system", audits.Single(a => a.Action == "document.retry").ActorUserId);
        Assert.All(audits, a => Assert.DoesNotContain("secret-body", a.Detail ?? ""));
        Assert.Equal("corrupted", audits.First(a => a.Action == "document.processing_failed").Detail);
    }

    // Claims the given document the way a worker would, leaving it in Processing.
    private async Task<ClaimedDocument> DrainUntilClaimedAsync(Guid id, TimeSpan? lease = null)
    {
        await DrainOthersAsync(id);
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessingStore>();
        var claim = await store.ClaimNextAsync(DateTimeOffset.UtcNow, new ProcessingOptions { Lease = lease ?? TimeSpan.FromSeconds(-1) }, default);
        Assert.Equal(id, claim!.Id);
        return claim;
    }

    // Processes everything except the target so it is the next document in the queue.
    private async Task DrainOthersAsync(Guid keep)
    {
        await DrainExceptAsync(keep);
    }

    private async Task DrainExceptAsync(Guid keep)
    {
        // The queue is oldest-first and the target is the newest upload in its test, so draining older work first is enough.
        while (true)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var anyOlder = await db.Documents.AnyAsync(d => d.Id != keep && d.DeletedAt == null &&
                (d.Status == DocumentStatus.Pending || (d.Status == DocumentStatus.Processing && d.ProcessingLeaseUntil < DateTimeOffset.UtcNow)) &&
                d.CreatedAt < db.Documents.Where(k => k.Id == keep).Select(k => k.CreatedAt).First());
            if (!anyOlder) return;
            await scope.ServiceProvider.GetRequiredService<ProcessingService>().ProcessNextAsync(default);
        }
    }
}
