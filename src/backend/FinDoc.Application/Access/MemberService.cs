using System.Net.Mail;
using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Access;

public record MeDto(bool HasAccess, string? TenantName, string? Role, bool IsPlatformAdmin);

public class MemberService(IAccessStore store, IUserDirectory users, ICurrentUser user)
{
    public const int MaxPageSize = 100;
    private const string UnusableEmail = "That email address cannot be used.";

    public async Task<MeDto> GetMeAsync(CancellationToken ct)
    {
        if (user.UserId is null)
            throw new ForbiddenException();
        var caller = await store.GetCallerAsync(user.UserId, ct);
        if (caller is null || !caller.Value.Tenant.IsActive)
            return new MeDto(false, null, null, user.IsPlatformAdmin);
        return new MeDto(true, caller.Value.Tenant.Name, caller.Value.Membership.Role.ToString(), user.IsPlatformAdmin);
    }

    public async Task<PagedResultDto<MemberInfo>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var caller = await AuthorizeAdminAsync(ct);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await store.ListMembersAsync(caller.TenantId, (page - 1) * pageSize, pageSize, ct);
        return new PagedResultDto<MemberInfo>(items, total, page, pageSize);
    }

    public async Task<MemberInfo> AddAsync(string? email, string? password, TenantRole role, CancellationToken ct)
    {
        var caller = await AuthorizeAdminAsync(ct);
        if (!Enum.IsDefined(role))
            throw new ValidationFailedException("The role is not valid.");
        if (!RoleRules.CanGrant(caller.Role, role))
            throw new ForbiddenException();
        return await CreateMemberAsync(caller.UserId, caller.TenantId, email, password, role, ct);
    }

    public async Task<MemberInfo> ChangeRoleAsync(string userId, TenantRole newRole, CancellationToken ct)
    {
        var caller = await AuthorizeAdminAsync(ct);
        if (!Enum.IsDefined(newRole))
            throw new ValidationFailedException("The role is not valid.");
        if (userId == caller.UserId)
            throw new ValidationFailedException("You cannot change your own role.");

        return await store.InTenantLockAsync(caller.TenantId, async () =>
        {
            // Re-check under the lock: the caller may have been demoted or removed by a concurrent request.
            var current = await AuthorizeAdminAsync(ct);
            var target = await store.GetMemberAsync(current.TenantId, userId, ct)
                ?? throw new NotFoundException("Member not found.");
            if (!RoleRules.CanGrant(current.Role, newRole) || !RoleRules.CanManage(current.Role, target.Role))
                throw new ForbiddenException();
            if (target.Role == TenantRole.TenantAdmin && newRole != TenantRole.TenantAdmin)
                await EnsureAnotherAdminAsync(current.TenantId, ct);

            target.Role = newRole;
            Audit(current.TenantId, current.UserId, "member.role_change", userId, newRole.ToString());
            await store.SaveChangesAsync(ct);
            return new MemberInfo(target.UserId, string.Empty, target.Role, target.CreatedAt);
        }, ct);
    }

    public async Task RemoveAsync(string userId, CancellationToken ct)
    {
        var caller = await AuthorizeAdminAsync(ct);
        await store.InTenantLockAsync(caller.TenantId, async () =>
        {
            var current = await AuthorizeAdminAsync(ct);
            var target = await store.GetMemberAsync(current.TenantId, userId, ct)
                ?? throw new NotFoundException("Member not found.");
            if (!RoleRules.CanManage(current.Role, target.Role))
                throw new ForbiddenException();
            if (target.Role == TenantRole.TenantAdmin)
                await EnsureAnotherAdminAsync(current.TenantId, ct);

            store.RemoveMembership(target);
            Audit(current.TenantId, current.UserId, "member.remove", userId, null);
            await store.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    internal async Task<MemberInfo> CreateMemberAsync(string actorUserId, Guid tenantId, string? email, string? password, TenantRole role, CancellationToken ct, Tenant? newTenant = null)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !MailAddress.TryCreate(email.Trim(), out var address) || address.Address != email.Trim())
            throw new ValidationFailedException("A valid email address is required.");
        if (string.IsNullOrEmpty(password))
            throw new ValidationFailedException("A password is required.");
        email = email.Trim();

        if (await store.EmailInUseAsync(email, ct))
            throw new ValidationFailedException(UnusableEmail);

        var (userId, error) = await users.CreateUserAsync(email, password, ct);
        if (userId is null)
            throw new ValidationFailedException(error ?? UnusableEmail);

        try
        {
            var membership = new TenantMembership { TenantId = tenantId, UserId = userId, Role = role };
            if (newTenant is not null)
            {
                store.AddTenant(newTenant);
                store.AddAudit(new AuditEvent { TenantId = tenantId, ActorUserId = actorUserId, Action = "tenant.create" });
            }
            store.AddMembership(membership);
            Audit(tenantId, actorUserId, "member.add", userId, role.ToString());
            await store.SaveChangesAsync(ct);
            return new MemberInfo(userId, email, role, membership.CreatedAt);
        }
        catch
        {
            await users.DeleteUserAsync(userId, CancellationToken.None);
            throw;
        }
    }

    private async Task EnsureAnotherAdminAsync(Guid tenantId, CancellationToken ct)
    {
        if (await store.CountAdminsAsync(tenantId, ct) <= 1)
            throw new ConflictException("An organization must keep at least one Tenant Administrator.");
    }

    private record Caller(string UserId, Guid TenantId, TenantRole Role);

    private async Task<Caller> AuthorizeAdminAsync(CancellationToken ct)
    {
        if (user.UserId is null)
            throw new ForbiddenException();
        var found = await store.GetCallerAsync(user.UserId, ct);
        if (found is null || !found.Value.Tenant.IsActive || found.Value.Membership.Role != TenantRole.TenantAdmin)
            throw new ForbiddenException();
        return new Caller(user.UserId, found.Value.Membership.TenantId, found.Value.Membership.Role);
    }

    private void Audit(Guid tenantId, string actor, string action, string target, string? detail) =>
        store.AddAudit(new AuditEvent { TenantId = tenantId, ActorUserId = actor, Action = action, TargetUserId = target, Detail = detail });
}

public record PagedResultDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
