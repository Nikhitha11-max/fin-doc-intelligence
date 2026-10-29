using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FinDoc.Application.Processing;

namespace FinDoc.Infrastructure.Processing;

public class XlsxExtractor : IDocumentExtractor
{
    private const int MaxEntries = 10_000;
    private const long RatioCheckThreshold = 1024 * 1024;
    private static readonly byte[] OleHeader = [0xD0, 0xCF, 0x11, 0xE0];

    public string Extension => ".xlsx";

    public IReadOnlyList<ExtractedUnit> Extract(Stream content, ProcessingOptions options, CancellationToken ct)
    {
        try
        {
            RejectEncrypted(content);
            GuardAgainstDecompressionBombs(content, options, ct);
            content.Position = 0;
            return Read(content, options, ct);
        }
        catch (Exception e) when (e is not ExtractionException and not OperationCanceledException)
        {
            throw new ExtractionException(FailureCodes.Corrupted);
        }
    }

    // Password-protected workbooks are stored as an OLE container instead of a ZIP package.
    private static void RejectEncrypted(Stream content)
    {
        var head = new byte[4];
        content.Position = 0;
        if (content.Read(head, 0, 4) == 4 && head.AsSpan().SequenceEqual(OleHeader))
            throw new ExtractionException(FailureCodes.Encrypted);
        content.Position = 0;
    }

    // Declared sizes can be forged, so the real decompressed size is counted as well.
    private static void GuardAgainstDecompressionBombs(Stream content, ProcessingOptions options, CancellationToken ct)
    {
        using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > MaxEntries)
            throw new ExtractionException(FailureCodes.TooLarge);

        long declared = 0;
        foreach (var entry in zip.Entries)
        {
            declared += entry.Length;
            if (declared > options.MaxXlsxUncompressedBytes)
                throw new ExtractionException(FailureCodes.TooLarge);
            if (entry.Length > RatioCheckThreshold && entry.Length > entry.CompressedLength * (long)options.MaxXlsxCompressionRatio)
                throw new ExtractionException(FailureCodes.TooLarge);
        }

        long actual = 0;
        var buffer = new byte[81920];
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            int n;
            while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                actual += n;
                if (actual > options.MaxXlsxUncompressedBytes)
                    throw new ExtractionException(FailureCodes.TooLarge);
                ct.ThrowIfCancellationRequested();
            }
        }
    }

    private static IReadOnlyList<ExtractedUnit> Read(Stream content, ProcessingOptions options, CancellationToken ct)
    {
        using var document = SpreadsheetDocument.Open(content, false);
        var workbook = document.WorkbookPart ?? throw new ExtractionException(FailureCodes.Corrupted);
        var sheets = workbook.Workbook.Sheets?.Elements<Sheet>().ToList() ?? [];
        if (sheets.Count > options.MaxSheets)
            throw new ExtractionException(FailureCodes.TooLarge);

        long characters = 0;
        var shared = ReadSharedStrings(workbook, options, ct);
        var units = new List<ExtractedUnit>();
        var rows = 0;

        foreach (var sheet in sheets)
        {
            if (sheet.Id?.Value is null || workbook.GetPartById(sheet.Id.Value) is not WorksheetPart part)
                continue;

            using var reader = OpenXmlReader.Create(part);
            while (reader.Read())
            {
                if (reader.ElementType != typeof(Row) || !reader.IsStartElement)
                    continue;
                ct.ThrowIfCancellationRequested();

                var row = (Row)reader.LoadCurrentElement()!;
                var values = row.Elements<Cell>().Select(c => CellText(c, shared).Trim()).ToList();
                if (values.All(v => v.Length == 0))
                    continue;

                var text = string.Join(" | ", values);
                characters += text.Length;
                if (characters > options.MaxExtractedCharacters || ++rows > options.MaxRows)
                    throw new ExtractionException(FailureCodes.TooLarge);
                units.Add(new ExtractedUnit(text, Sheet: sheet.Name?.Value, Row: (int?)row.RowIndex?.Value));
            }
        }

        if (units.Count == 0)
            throw new ExtractionException(FailureCodes.Empty);
        return units;
    }

    private static List<string> ReadSharedStrings(WorkbookPart workbook, ProcessingOptions options, CancellationToken ct)
    {
        var strings = new List<string>();
        if (workbook.SharedStringTablePart is null)
            return strings;

        long characters = 0;
        using var reader = OpenXmlReader.Create(workbook.SharedStringTablePart);
        while (reader.Read())
        {
            if (reader.ElementType != typeof(SharedStringItem) || !reader.IsStartElement)
                continue;
            ct.ThrowIfCancellationRequested();
            var text = ((SharedStringItem)reader.LoadCurrentElement()!).InnerText;
            characters += text.Length;
            if (characters > options.MaxExtractedCharacters)
                throw new ExtractionException(FailureCodes.TooLarge);
            strings.Add(text);
        }
        return strings;
    }

    private static string CellText(Cell cell, List<string> shared)
    {
        var type = cell.DataType?.Value;
        if (type == CellValues.InlineString)
            return cell.InlineString?.InnerText ?? string.Empty;

        var raw = cell.CellValue?.Text ?? string.Empty;
        if (type == CellValues.SharedString)
            return int.TryParse(raw, out var index) && index >= 0 && index < shared.Count ? shared[index] : string.Empty;
        if (type == CellValues.Boolean)
            return raw == "1" ? "TRUE" : "FALSE";
        return raw;
    }
}
