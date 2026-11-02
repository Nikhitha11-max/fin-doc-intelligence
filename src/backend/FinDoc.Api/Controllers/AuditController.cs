using FinDoc.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api.Controllers;

// Read-only on purpose: audit records have no update or delete endpoint.
[ApiController]
[Authorize]
[Route("api/audit")]
public sealed class AuditController(AuditQueryService audit) : ControllerBase
{
    [HttpGet]
    public Task<AuditPage> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? actor,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        audit.ListAsync(new AuditQuery(from, to, actor, action), page, pageSize, ct);
}
