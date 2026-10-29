using FinDoc.Application.Abstractions;
using FinDoc.Application.Access;
using FinDoc.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api.Controllers;

public record AddMemberRequest(string? Email, string? Password, TenantRole Role);

public record ChangeRoleRequest(TenantRole Role);

public record CreateTenantRequest(string? Name, string? AdminEmail, string? AdminPassword);

public record RenameTenantRequest(string? Name);

public record CreateAdminRequest(string? Email, string? Password);

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(MemberService members) : ControllerBase
{
    [HttpGet]
    public Task<MeDto> Get(CancellationToken ct) => members.GetMeAsync(ct);
}

// The organization is never read from the request; it comes from the signed-in user's membership.
[ApiController]
[Authorize]
[Route("api/members")]
public class MembersController(MemberService members) : ControllerBase
{
    [HttpGet]
    public Task<PagedResultDto<MemberInfo>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        members.ListAsync(page, pageSize, ct);

    [HttpPost]
    public async Task<ActionResult<MemberInfo>> Add(AddMemberRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await members.AddAsync(request.Email, request.Password, request.Role, ct));

    [HttpPut("{userId}/role")]
    public Task<MemberInfo> ChangeRole(string userId, ChangeRoleRequest request, CancellationToken ct) =>
        members.ChangeRoleAsync(userId, request.Role, ct);

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(string userId, CancellationToken ct)
    {
        await members.RemoveAsync(userId, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize(Roles = PlatformRoles.PlatformAdmin)]
[Route("api/platform/tenants")]
public class PlatformTenantsController(PlatformTenantService tenants) : ControllerBase
{
    [HttpGet]
    public Task<PagedResultDto<TenantSummary>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        tenants.ListAsync(page, pageSize, ct);

    [HttpPost]
    public async Task<ActionResult<TenantSummary>> Create(CreateTenantRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await tenants.CreateAsync(request.Name, request.AdminEmail, request.AdminPassword, ct));

    [HttpPut("{id:guid}")]
    public Task<TenantSummary> Rename(Guid id, RenameTenantRequest request, CancellationToken ct) =>
        tenants.RenameAsync(id, request.Name, ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<TenantSummary> Deactivate(Guid id, CancellationToken ct) => tenants.SetActiveAsync(id, false, ct);

    [HttpPost("{id:guid}/reactivate")]
    public Task<TenantSummary> Reactivate(Guid id, CancellationToken ct) => tenants.SetActiveAsync(id, true, ct);

    [HttpPost("{id:guid}/admins")]
    public async Task<ActionResult<MemberInfo>> CreateAdmin(Guid id, CreateAdminRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await tenants.CreateAdminAsync(id, request.Email, request.Password, ct));
}
