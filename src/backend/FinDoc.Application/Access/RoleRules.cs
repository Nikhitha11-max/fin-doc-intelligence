using FinDoc.Domain.Entities;

namespace FinDoc.Application.Access;

// TenantRole values are ordered from most to least privileged, so a smaller number is a higher role.
public static class RoleRules
{
    public static bool CanGrant(TenantRole actor, TenantRole target) => target >= actor;

    public static bool CanManage(TenantRole actor, TenantRole current) => current >= actor;
}
