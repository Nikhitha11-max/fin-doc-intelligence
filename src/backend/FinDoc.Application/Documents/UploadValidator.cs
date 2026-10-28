using FinDoc.Application.Abstractions;

namespace FinDoc.Application.Documents;

public static class UploadValidator
{
    public const long MaxSizeBytes = 25L * 1024 * 1024;
    private static readonly HashSet<string> Allowed = [".pdf", ".csv", ".xlsx"];

    /// <summary>Validates the upload by extension and content, ignoring any client-supplied content type.</summary>
    public static async Task<string> ValidateAsync(string fileName, Stream content, CancellationToken ct)
    {
        if (!content.CanSeek)
            throw new ValidationFailedException("Unsupported upload stream.");
        if (content.Length == 0)
            throw new ValidationFailedException("The file is empty.");
        if (content.Length > MaxSizeBytes)
            throw new ValidationFailedException("The file exceeds the 25 MB limit.");

        var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty)).ToLowerInvariant();
        if (!Allowed.Contains(extension))
            throw new ValidationFailedException("Only PDF, CSV and XLSX files are supported.");

        var buffer = new byte[(int)Math.Min(4096, content.Length)];
        content.Position = 0;
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await content.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0) break;
            read += n;
        }
        content.Position = 0;
        if (!ContentMatches(extension, buffer.AsSpan(0, read)))
            throw new ValidationFailedException("The file content does not match its extension.");

        return extension;
    }

    private static bool ContentMatches(string extension, ReadOnlySpan<byte> head) => extension switch
    {
        ".pdf" => head.StartsWith("%PDF-"u8),
        ".xlsx" => head.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
        _ => !head.Contains((byte)0),
    };
}
