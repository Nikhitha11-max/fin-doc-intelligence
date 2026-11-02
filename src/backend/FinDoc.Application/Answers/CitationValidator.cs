using FinDoc.Application.Search;

namespace FinDoc.Application.Answers;

public sealed class CitationValidator
{
    public IReadOnlyList<AnswerCitation> Validate(
        IReadOnlyList<int> citationNumbers,
        IReadOnlyList<SearchResult> retrieved)
    {
        var validNumbers = citationNumbers
            .Where(number => number >= 1 && number <= retrieved.Count)
            .Distinct()
            .ToHashSet();

        return retrieved.Select((source, index) => (source, Number: index + 1))
            .Where(item => validNumbers.Contains(item.Number))
            .Select(item => new AnswerCitation(
                item.source.DocumentId,
                item.source.DocumentTitle,
                item.source.ChunkId,
                item.source.PageStart,
                item.source.PageEnd,
                item.source.SheetName,
                item.source.RowStart,
                item.source.RowEnd))
            .ToList();
    }
}
