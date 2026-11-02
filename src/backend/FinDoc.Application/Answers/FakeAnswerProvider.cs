using System.Text.RegularExpressions;

namespace FinDoc.Application.Answers;

public sealed partial class FakeAnswerProvider(AnswerOptions options) : IAnswerProvider
{
    public Task<AnswerProviderResult> AnswerAsync(AnswerProviderRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var questionWords = TokenPattern().Matches(request.Question)
            .Select(match => match.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var best = request.Passages
            .Select(passage => (Passage: passage, Overlap: TokenPattern().Matches(passage.Source.Snippet)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(questionWords.Contains)))
            .OrderByDescending(item => item.Overlap)
            .ThenBy(item => item.Passage.Number)
            .FirstOrDefault();

        if (best.Passage is null || best.Overlap == 0)
            return Task.FromResult(new AnswerProviderResult(string.Empty, []));

        var sentence = SplitSentences(best.Passage.Source.Snippet)
            .Select(text => (Text: text.Trim(), Overlap: TokenPattern().Matches(text)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(questionWords.Contains)))
            .OrderByDescending(item => item.Overlap)
            .FirstOrDefault(item => item.Overlap > 0);

        if (string.IsNullOrWhiteSpace(sentence.Text))
            return Task.FromResult(new AnswerProviderResult(string.Empty, []));

        var answer = sentence.Text.Length <= options.MaxAnswerLength
            ? sentence.Text
            : sentence.Text[..options.MaxAnswerLength];
        return Task.FromResult(new AnswerProviderResult(answer, [best.Passage.Number]));
    }

    private static IEnumerable<string> SplitSentences(string text) =>
        text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
