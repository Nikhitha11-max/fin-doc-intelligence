using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FinDoc.Application.Search;

/// <summary>
/// Deterministic local lexical vectors for development and tests. This is not a language model
/// and does not provide semantic understanding.
/// </summary>
public sealed partial class FakeEmbeddingProvider(EmbeddingOptions options) : IEmbeddingProvider
{
    public string Model => "fake-token-hash-v1";

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (options.Dimensions != EmbeddingOptions.SchemaDimensions)
            throw new EmbeddingException(IndexFailureCodes.InvalidConfig);

        IReadOnlyList<float[]> result = texts.Select(text =>
        {
            ct.ThrowIfCancellationRequested();
            var vector = new float[options.Dimensions];
            foreach (Match match in TokenPattern().Matches(text.ToLowerInvariant()))
            {
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(match.Value));
                var index = BitConverter.ToUInt32(bytes, 0) % (uint)vector.Length;
                var sign = (bytes[4] & 1) == 0 ? 1f : -1f;
                vector[index] += sign;
            }

            var magnitude = MathF.Sqrt(vector.Sum(value => value * value));
            if (magnitude > 0)
            {
                for (var i = 0; i < vector.Length; i++)
                    vector[i] /= magnitude;
            }
            return vector;
        }).ToList();

        return Task.FromResult(result);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
