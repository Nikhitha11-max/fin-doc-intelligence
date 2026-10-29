using FinDoc.Application.Abstractions;
using FinDoc.Application.Access;
using FinDoc.Domain.Entities;

namespace FinDoc.Tests.Access;

public class MemberServiceTests
{
    private readonly FakeAccessStore _store = new();
    private readonly FakeUser _user = new();
    private readonly Tenant _acme = new() { Name = "Acme" };
    private readonly Tenant _globex = new() { Name = "Globex" };

    public MemberServiceTests()
    {
        _store.Tenants.AddRange([_acme, _globex]);
    }

    private MemberService Members => new(_store, new FakeUserDirectory(_store), _user);
    private PlatformTenantService Platform => new(_store, Members, _user);

    private void Add(string id, Tenant tenant, TenantRole role)
    {
        _store.Memberships.Add(new TenantMembership { UserId = id, TenantId = tenant.Id, Role = role });
        _store.Emails[id] = id + "@test";
    }

    [Theory]
    [InlineData(TenantRole.DocumentManager)]
    [InlineData(TenantRole.Analyst)]
    [InlineData(TenantRole.Auditor)]
    public async Task Non_admins_cannot_manage_members(TenantRole role)
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        Add("u", _acme, role);
        _user.UserId = "u";
        await Assert.ThrowsAsync<ForbiddenException>(() => Members.ListAsync(1, 20, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Members.AddAsync("n@test.com", "Password1!", TenantRole.Analyst, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Members.ChangeRoleAsync("boss", TenantRole.Analyst, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Members.RemoveAsync("boss", default));
    }

    [Fact]
    public async Task Admin_adds_promotes_and_removes_a_member_with_audit()
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        _user.UserId = "boss";

        var added = await Members.AddAsync("new@test.com", "Password1!", TenantRole.Analyst, default);
        Assert.Equal(TenantRole.Analyst, added.Role);
        Assert.Equal(_acme.Id, _store.Memberships.Single(m => m.UserId == added.UserId).TenantId);

        await Members.ChangeRoleAsync(added.UserId, TenantRole.DocumentManager, default);
        Assert.Equal(TenantRole.DocumentManager, _store.Memberships.Single(m => m.UserId == added.UserId).Role);

        await Members.RemoveAsync(added.UserId, default);
        Assert.DoesNotContain(_store.Memberships, m => m.UserId == added.UserId);

        Assert.Equal(["member.add", "member.role_change", "member.remove"], _store.Audits.Select(a => a.Action));
        Assert.All(_store.Audits, a => Assert.Equal(_acme.Id, a.TenantId));
        Assert.DoesNotContain(_store.Audits, a => (a.Detail ?? "").Contains("Password"));
    }

    [Fact]
    public async Task Other_tenants_members_look_nonexistent()
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        Add("other", _globex, TenantRole.Analyst);
        _user.UserId = "boss";
        await Assert.ThrowsAsync<NotFoundException>(() => Members.ChangeRoleAsync("other", TenantRole.Auditor, default));
        await Assert.ThrowsAsync<NotFoundException>(() => Members.RemoveAsync("other", default));
        var list = await Members.ListAsync(1, 20, default);
        Assert.DoesNotContain(list.Items, m => m.UserId == "other");
    }

    [Fact]
    public async Task Last_admin_cannot_be_removed_or_demoted()
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        _user.UserId = "boss";
        await Assert.ThrowsAsync<ValidationFailedException>(() => Members.ChangeRoleAsync("boss", TenantRole.Analyst, default));
        await Assert.ThrowsAsync<ConflictException>(() => Members.RemoveAsync("boss", default));
        Assert.Equal(TenantRole.TenantAdmin, _store.Memberships.Single().Role);
    }

    [Fact]
    public async Task Admin_can_remove_another_admin_but_not_the_last_one()
    {
        Add("a", _acme, TenantRole.TenantAdmin);
        Add("b", _acme, TenantRole.TenantAdmin);
        _user.UserId = "a";
        await Members.RemoveAsync("b", default);
        await Assert.ThrowsAsync<ConflictException>(() => Members.RemoveAsync("a", default));
    }

    [Fact]
    public async Task Admin_cannot_change_own_role()
    {
        Add("a", _acme, TenantRole.TenantAdmin);
        Add("b", _acme, TenantRole.TenantAdmin);
        _user.UserId = "a";
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => Members.ChangeRoleAsync("a", TenantRole.Analyst, default));
        Assert.Contains("own role", ex.Message);
    }

    [Theory]
    [InlineData(TenantRole.DocumentManager, TenantRole.TenantAdmin, true)]
    [InlineData(TenantRole.Analyst, TenantRole.DocumentManager, true)]
    [InlineData(TenantRole.TenantAdmin, TenantRole.TenantAdmin, false)]
    [InlineData(TenantRole.Analyst, TenantRole.Auditor, false)]
    public void Role_rules_block_granting_above_own_role(TenantRole actor, TenantRole target, bool blocked) =>
        Assert.Equal(blocked, !RoleRules.CanGrant(actor, target));

    [Fact]
    public async Task Invalid_input_and_duplicate_emails_are_refused_generically()
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        Add("other", _globex, TenantRole.Analyst);
        _user.UserId = "boss";
        await Assert.ThrowsAsync<ValidationFailedException>(() => Members.AddAsync("not-an-email", "Password1!", TenantRole.Analyst, default));
        await Assert.ThrowsAsync<ValidationFailedException>(() => Members.AddAsync("x@test.com", "short", TenantRole.Analyst, default));
        await Assert.ThrowsAsync<ValidationFailedException>(() => Members.AddAsync("x@test.com", "Password1!", (TenantRole)99, default));

        var inOther = await Assert.ThrowsAsync<ValidationFailedException>(() => Members.AddAsync("other@test", "Password1!", TenantRole.Analyst, default));
        var inOwn = await Assert.ThrowsAsync<ValidationFailedException>(() => Members.AddAsync("boss@test", "Password1!", TenantRole.Analyst, default));
        Assert.Equal(inOwn.Message, inOther.Message);
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        for (var i = 0; i < 120; i++) Add($"m{i}", _acme, TenantRole.Analyst);
        _user.UserId = "boss";
        var page = await Members.ListAsync(1, 5000, default);
        Assert.Equal(MemberService.MaxPageSize, page.Items.Count);
        Assert.Equal(121, page.Total);
    }

    [Fact]
    public async Task Me_reports_organization_role_or_no_access()
    {
        Add("a", _acme, TenantRole.Auditor);
        _user.UserId = "a";
        var me = await Members.GetMeAsync(default);
        Assert.True(me.HasAccess);
        Assert.Equal(("Acme", "Auditor"), (me.TenantName, me.Role));

        _user.UserId = "nobody";
        Assert.False((await Members.GetMeAsync(default)).HasAccess);

        _acme.IsActive = false;
        _user.UserId = "a";
        Assert.False((await Members.GetMeAsync(default)).HasAccess);
    }

    [Fact]
    public async Task Deactivated_tenant_admin_cannot_manage_members()
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        _acme.IsActive = false;
        _user.UserId = "boss";
        await Assert.ThrowsAsync<ForbiddenException>(() => Members.ListAsync(1, 20, default));
    }

    [Fact]
    public async Task Platform_admin_creates_renames_deactivates_and_reactivates_tenants()
    {
        _user.UserId = "root";
        _user.IsPlatformAdmin = true;
        var created = await Platform.CreateAsync("  Initech ", "admin@initech.test", "Password1!", default);
        Assert.Equal("Initech", created.Name);
        var first = _store.Memberships.Single(m => m.TenantId == created.Id);
        Assert.Equal(TenantRole.TenantAdmin, first.Role);

        await Platform.RenameAsync(created.Id, "Initech Corp", default);
        Assert.False((await Platform.SetActiveAsync(created.Id, false, default)).IsActive);
        Assert.True((await Platform.SetActiveAsync(created.Id, true, default)).IsActive);
        Assert.Equal(TenantRole.TenantAdmin, _store.Memberships.Single(m => m.UserId == first.UserId).Role);

        Assert.Equal(["tenant.create", "member.add", "tenant.rename", "tenant.deactivate", "tenant.reactivate"], _store.Audits.Select(a => a.Action));
    }

    [Fact]
    public async Task Failed_first_admin_leaves_no_tenant_behind()
    {
        _user.UserId = "root";
        _user.IsPlatformAdmin = true;
        var before = _store.Tenants.Count;
        await Assert.ThrowsAsync<ValidationFailedException>(() => Platform.CreateAsync("Initech", "bad", "Password1!", default));
        Assert.Equal(before, _store.Tenants.Count);
    }

    [Fact]
    public async Task Only_platform_admins_use_platform_actions_and_they_have_no_tenant_powers()
    {
        Add("boss", _acme, TenantRole.TenantAdmin);
        _user.UserId = "boss";
        await Assert.ThrowsAsync<ForbiddenException>(() => Platform.ListAsync(1, 20, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Platform.CreateAsync("X", "a@test.com", "Password1!", default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Platform.SetActiveAsync(_acme.Id, false, default));

        _user.UserId = "root";
        _user.IsPlatformAdmin = true;
        await Assert.ThrowsAsync<ForbiddenException>(() => Members.ListAsync(1, 20, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Members.RemoveAsync("boss", default));
    }
}
