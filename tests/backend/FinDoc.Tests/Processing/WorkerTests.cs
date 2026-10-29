using FinDoc.Tests.Processing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinDoc.Tests.Processing;

public class WorkerTests(WorkerProcessingFixture fixture) : ProcessingTestBase(fixture.Factory), IClassFixture<WorkerProcessingFixture>
{
    [Fact]
    public async Task The_background_worker_processes_uploads_after_the_response()
    {
        var tenant = await NewTenantAsync();
        var created = await UploadAsync(tenant.Admin, "a.csv", TestFiles.Csv("a,b", "c,d"));
        var id = created.GetProperty("id").GetGuid();

        string? status = null;
        for (var i = 0; i < 100 && status != "Completed"; i++)
        {
            status = (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString();
            if (status != "Completed") await Task.Delay(100);
        }

        Assert.Equal("Completed", status);
        Assert.Single(await ChunksAsync(id));
    }

    [Fact]
    public async Task The_background_worker_reports_failures_and_keeps_running()
    {
        var tenant = await NewTenantAsync();
        var bad = (await UploadAsync(tenant.Admin, "bad.pdf", "%PDF-1.7 nope"u8.ToArray())).GetProperty("id").GetGuid();
        var good = (await UploadAsync(tenant.Admin, "ok.csv", TestFiles.Csv("a,b"))).GetProperty("id").GetGuid();

        async Task<string?> WaitAsync(Guid id)
        {
            string? status = "Pending";
            for (var i = 0; i < 100 && status is "Pending" or "Processing"; i++)
            {
                status = (await GetDocAsync(tenant.Admin, id)).GetProperty("status").GetString();
                if (status is "Pending" or "Processing") await Task.Delay(100);
            }
            return status;
        }

        Assert.Equal("Failed", await WaitAsync(bad));
        Assert.Equal("Completed", await WaitAsync(good));
    }
}
