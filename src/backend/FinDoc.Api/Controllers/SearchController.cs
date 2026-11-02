using FinDoc.Application.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public sealed class SearchController(SearchService search) : ControllerBase
{
    [HttpPost]
    public Task<SearchResponse> Search(SearchRequest request, CancellationToken ct) =>
        search.SearchAsync(request, ct);
}
