using FinDoc.Application.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/conversations")]
public sealed class ConversationsController(ConversationService conversations) : ControllerBase
{
    [HttpGet]
    public Task<ConversationPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        conversations.ListAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<ConversationDto> Get(Guid id, CancellationToken ct) =>
        conversations.GetAsync(id, ct);
}
