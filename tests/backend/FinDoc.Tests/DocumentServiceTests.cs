using FinDoc.Application.Abstractions;
using FinDoc.Application.Documents;
using FinDoc.Domain.Entities;

namespace FinDoc.Tests;

public class DocumentServiceTests
{
    private readonly FakeStore _store = new();
    private readonly FakeFiles _files = new();
    private readonly FakeUser _user = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    private DocumentService Service => new(_store, _files, _user);

    private void AddMember(string userId, Guid tenant, TenantRole role) =>
        _store.Memberships.Add(new TenantMembership { UserId = userId, TenantId = tenant, Role = role });

    private static MemoryStream Pdf() => new("%PDF-1.7 content"u8.ToArray());

    private async Task<DocumentDto> UploadAs(string userId)
    {
        _user.UserId = userId;
        return await Service.UploadAsync("Report", "report.pdf", Pdf(), default);
    }

    [Theory]
    [InlineData(TenantRole.TenantAdmin, true, true)]
    [InlineData(TenantRole.DocumentManager, true, true)]
    [InlineData(TenantRole.Analyst, false, true)]
    [InlineData(TenantRole.Auditor, false, false)]
    public async Task Permission_matrix(TenantRole role, bool canWrite, bool canRead)
    {
        AddMember("owner", _tenantA, TenantRole.TenantAdmin);
        var doc = await UploadAs("owner");
        AddMember("u", _tenantA, role);
        _user.UserId = "u";

        async Task Check(bool allowed, Func<Task> action)
        {
            if (allowed) await action();
            else await Assert.ThrowsAsync<ForbiddenException>(action);
        }

        await Check(canRead, () => Service.ListAsync(1, 20, default));
        await Check(canRead, () => Service.GetAsync(doc.Id, default));
        await Check(canRead, () => Service.DownloadAsync(doc.Id, default));
        await Check(canWrite, () => Service.UploadAsync(null, "x.pdf", Pdf(), default));
        await Check(canWrite, () => Service.UpdateAsync(doc.Id, "New", default));
        await Check(canWrite, () => Service.DeleteAsync(doc.Id, default));
    }

    [Fact]
    public async Task Anonymous_and_non_members_are_forbidden()
    {
        _user.UserId = null;
        await Assert.ThrowsAsync<ForbiddenException>(() => Service.ListAsync(1, 20, default));
        _user.UserId = "stranger";
        await Assert.ThrowsAsync<ForbiddenException>(() => Service.ListAsync(1, 20, default));
    }

    [Fact]
    public async Task Other_tenants_documents_are_not_found_for_every_operation()
    {
        AddMember("a", _tenantA, TenantRole.TenantAdmin);
        AddMember("b", _tenantB, TenantRole.TenantAdmin);
        var doc = await UploadAs("a");
        _user.UserId = "b";

        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetAsync(doc.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.DownloadAsync(doc.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.UpdateAsync(doc.Id, "Hacked", default));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.DeleteAsync(doc.Id, default));
        Assert.Empty((await Service.ListAsync(1, 20, default)).Items);
        Assert.Null(_store.Documents.Single().DeletedAt);
        Assert.Equal("Report", _store.Documents.Single().Title);
    }

    [Fact]
    public async Task Soft_deleted_documents_disappear_but_file_is_retained()
    {
        AddMember("a", _tenantA, TenantRole.TenantAdmin);
        var doc = await UploadAs("a");
        await Service.DeleteAsync(doc.Id, default);

        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetAsync(doc.Id, default));
        Assert.Empty((await Service.ListAsync(1, 20, default)).Items);
        Assert.Single(_files.Saved);
    }

    [Fact]
    public async Task Upload_uses_generated_storage_key_not_the_file_name()
    {
        AddMember("a", _tenantA, TenantRole.TenantAdmin);
        _user.UserId = "a";
        await Service.UploadAsync(null, "../../evil.pdf", Pdf(), default);

        var doc = _store.Documents.Single();
        Assert.Equal("evil.pdf", doc.OriginalFileName);
        Assert.Equal($"{_tenantA:N}/{doc.Id:N}", doc.StorageKey);
        Assert.DoesNotContain("evil", doc.StorageKey);
    }

    [Fact]
    public async Task Writes_audit_events_without_titles_or_contents()
    {
        AddMember("a", _tenantA, TenantRole.TenantAdmin);
        var doc = await UploadAs("a");
        await Service.GetAsync(doc.Id, default);
        await (await Service.DownloadAsync(doc.Id, default)).Content.DisposeAsync();
        await Service.UpdateAsync(doc.Id, "Renamed", default);
        await Service.DeleteAsync(doc.Id, default);

        Assert.Equal(
            ["document.upload", "document.download", "document.update", "document.delete"],
            _store.Audits.Select(a => a.Action));
        Assert.All(_store.Audits, a =>
        {
            Assert.Equal(_tenantA, a.TenantId);
            Assert.Equal("a", a.ActorUserId);
            Assert.Equal(doc.Id, a.DocumentId);
        });
    }

    [Fact]
    public async Task Invalid_uploads_store_nothing()
    {
        AddMember("a", _tenantA, TenantRole.TenantAdmin);
        _user.UserId = "a";
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Service.UploadAsync(null, "x.pdf", new MemoryStream("nope"u8.ToArray()), default));

        Assert.Empty(_files.Saved);
        Assert.Empty(_store.Documents);
        Assert.Empty(_store.Audits);
    }

    [Fact]
    public async Task Listing_is_paginated_and_clamped()
    {
        AddMember("a", _tenantA, TenantRole.TenantAdmin);
        for (var i = 0; i < 5; i++) await UploadAs("a");

        var page = await Service.ListAsync(2, 2, default);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.Total);
        Assert.Equal(100, (await Service.ListAsync(1, 1000, default)).PageSize);
        Assert.Equal(1, (await Service.ListAsync(-3, 20, default)).Page);
    }
}
