using FinDoc.Application.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api.Controllers;

[ApiController]
[Authorize(Roles = PlatformRoles.PlatformAdmin)]
[Route("api/platform/diagnostics")]
public sealed class DiagnosticsController(DiagnosticsService diagnostics) : ControllerBase
{
    [HttpGet]
    public Task<DiagnosticsDto> Get(CancellationToken ct) => diagnostics.GetAsync(ct);
}
