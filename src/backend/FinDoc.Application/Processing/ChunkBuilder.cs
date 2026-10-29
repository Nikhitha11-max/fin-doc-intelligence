using System.Text;
using FinDoc.Domain.Entities;

namespace FinDoc.Application.Processing;

public static class ChunkBuilder
{
    /// <summary>Groups units into ordered chunks of about <c>ChunkSize</c> characters, keeping page or sheet/row ranges.</summary>
    public static List<DocumentChunk> Build(Guid tenantId, Guid documentId, IEnumerable<ExtractedUnit> units, ProcessingOptions options)
    {
        var chunks = new List<DocumentChunk>();
        var text = new StringBuilder();
        string? sheet = null;
        int? pageStart = null, pageEnd = null, rowStart = null, rowEnd = null;

        void Flush()
        {
            if (text.Length == 0) return;
            chunks.Add(new DocumentChunk
            {
                TenantId = tenantId, DocumentId = documentId, Ordinal = chunks.Count, Text = text.ToString(),
                PageStart = pageStart, PageEnd = pageEnd, SheetName = sheet, RowStart = rowStart, RowEnd = rowEnd,
            });
            text.Clear();
            pageStart = pageEnd = rowStart = rowEnd = null;
            if (chunks.Count > options.MaxChunks) throw new ExtractionException(FailureCodes.TooLarge);
        }

        void Append(string piece, ExtractedUnit unit)
        {
            if (text.Length > 0 && (unit.Sheet != sheet || text.Length + piece.Length + 1 > options.ChunkSize))
                Flush();
            if (text.Length > 0) text.Append('\n');
            text.Append(piece);
            sheet = unit.Sheet;
            pageStart ??= unit.Page;
            pageEnd = unit.Page ?? pageEnd;
            rowStart ??= unit.Row;
            rowEnd = unit.Row ?? rowEnd;
        }

        foreach (var unit in units)
        {
            var value = unit.Text.Trim();
            if (value.Length == 0) continue;

            if (value.Length <= options.ChunkSize)
            {
                Append(value, unit);
                continue;
            }

            // Oversized unit: split into overlapping pieces that all keep the unit's location.
            Flush();
            var step = Math.Max(1, options.ChunkSize - options.ChunkOverlap);
            for (var i = 0; i < value.Length; i += step)
            {
                Append(value.Substring(i, Math.Min(options.ChunkSize, value.Length - i)), unit);
                Flush();
                if (i + options.ChunkSize >= value.Length) break;
            }
        }
        Flush();
        return chunks;
    }
}
