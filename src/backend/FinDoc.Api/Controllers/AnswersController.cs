using FinDoc.Application.Answers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/answers")]
public sealed class AnswersController(AnswerService answers) : ControllerBase
{
    [HttpPost]
    public Task<AnswerResponse> Ask(AskRequest request, CancellationToken ct) =>
        answers.AnswerAsync(request, ct);
}
