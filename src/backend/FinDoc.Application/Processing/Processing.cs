using FinDoc.Domain.Entities;

namespace FinDoc.Application.Processing;

public class ProcessingOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public int MaxAttempts { get; set; } = 5;
    public long MaxExtractedCharacters { get; set; } = 10_000_000;
    public int MaxPages { get; set; } = 2000;
    public int MaxSheets { get; set; } = 50;
    public int MaxRows { get; set; } = 500_000;
    public int MaxChunks { get; set; } = 20_000;
    public long MaxXlsxUncompressedBytes { get; set; } = 100L * 1024 * 1024;
    public int MaxXlsxCompressionRatio { get; set; } = 100;
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 100;
}

public static class FailureCodes
{
    public const string Corrupted = "corrupted";
    public const string Encrypted = "encrypted";
    public const string Empty = "empty";
    public const string NoExtractableText = "no_extractable_text";
    public const string TooLarge = "too_large";
    public const string Timeout = "timeout";
    public const string Internal = "internal";

    // Messages are fixed per code so they can never contain file contents or exception details.
    public static string MessageFor(string code) => code switch
    {
        Corrupted => "The file is damaged or not a valid document.",
        Encrypted => "The file is password-protected.",
        Empty => "The file contains no data.",
        NoExtractableText => "The file contains no extractable text (scanned documents are not supported).",
        TooLarge => "The file content exceeds the processing limits.",
        Timeout => "Processing took too long.",
        _ => "Processing failed unexpectedly.",
    };
}

public class ExtractionException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>A piece of source text with its location. PDF units are pages; CSV/XLSX units are rows.</summary>
public record ExtractedUnit(string Text, int? Page = null, string? Sheet = null, int? Row = null);

public interface IDocumentExtractor
{
    string Extension { get; }
    IReadOnlyList<ExtractedUnit> Extract(Stream content, ProcessingOptions options, CancellationToken ct);
}

public record ClaimedDocument(Guid Id, Guid TenantId, string StorageKey, string Extension, int Attempt);

public interface IProcessingStore
{
    Task<ClaimedDocument?> ClaimNextAsync(DateTimeOffset now, ProcessingOptions options, CancellationToken ct);
    Task<bool> CompleteAsync(ClaimedDocument claim, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct);
    Task<bool> FailAsync(ClaimedDocument claim, string code, CancellationToken ct);
}
