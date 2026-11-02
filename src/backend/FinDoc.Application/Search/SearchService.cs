using FinDoc.Application.Abstractions;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Search;

public sealed class SearchService(
    IDocumentStore documents,
    ISearchStore search,
    IEmbeddingProvider provider,
    EmbeddingOptions options,
    ICurrentUser user)
{
    private static readonly TenantRole[] Readers =
        [TenantRole.TenantAdmin, TenantRole.DocumentManager, TenantRole.Analyst];

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var (tenantId, actor) = await AuthorizeAsync(ct);
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new ValidationFailedException("A search query is required.");
        var query = request.Query.Trim();
        if (query.Length > options.MaxQueryLength)
            throw new ValidationFailedException($"The search query must be {options.MaxQueryLength} characters or fewer.");
        var limit = request.Limit ?? 10;
        if (limit < 1)
            throw new ValidationFailedException("The result limit must be at least 1.");
        limit = Math.Min(limit, options.MaxResults);

        var vectors = await provider.EmbedAsync([query], ct);
        if (vectors.Count != 1 || vectors[0].Length != options.Dimensions)
            throw new ValidationFailedException("The search provider returned an invalid embedding.");

        var hits = await search.SearchAsync(tenantId, vectors[0], limit, ct);
        await search.AddAuditAsync(tenantId, actor, ct);
        return new SearchResponse(hits.Select(hit => new SearchResult(
            hit.DocumentId, hit.DocumentTitle, hit.ChunkId, hit.Ordinal,
            hit.PageStart, hit.PageEnd, hit.SheetName, hit.RowStart, hit.RowEnd,
            hit.Score, hit.Text.Length <= 300 ? hit.Text : hit.Text[..300])).ToList());
    }

    private async Task<(Guid TenantId, string Actor)> AuthorizeAsync(CancellationToken ct)
    {
        if (user.UserId is null)
            throw new ForbiddenException();
        var membership = await documents.GetMembershipAsync(user.UserId, ct);
        if (membership is null || !Readers.Contains(membership.Role))
            throw new ForbiddenException();
        return (membership.TenantId, user.UserId);
    }
}
