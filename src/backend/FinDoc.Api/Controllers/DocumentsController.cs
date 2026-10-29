using FinDoc.Application.Abstractions;
using FinDoc.Application.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api.Controllers;

public record UpdateDocumentRequest(string? Title);

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController(DocumentService documents) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(UploadValidator.MaxSizeBytes + 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> Upload(IFormFile file, [FromForm] string? title, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var created = await documents.UploadAsync(title, file.FileName, stream, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public Task<PagedResult<DocumentDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        documents.ListAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<DocumentDto> Get(Guid id, CancellationToken ct) => documents.GetAsync(id, ct);

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await documents.DownloadAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Content, "application/octet-stream", result.FileName);
    }

    [HttpPut("{id:guid}")]
    public Task<DocumentDto> Update(Guid id, UpdateDocumentRequest request, CancellationToken ct) =>
        documents.UpdateAsync(id, request.Title, ct);

    [HttpPost("{id:guid}/retry")]
    public Task<DocumentDto> Retry(Guid id, CancellationToken ct) => documents.RetryAsync(id, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return NoContent();
    }
}
