using System.Text;
using FinDoc.Application.Processing;

namespace FinDoc.Infrastructure.Processing;

/// <summary>Streams a CSV row by row (quoted fields and embedded newlines supported) without loading the whole file.</summary>
public class CsvExtractor : IDocumentExtractor
{
    public string Extension => ".csv";

    public IReadOnlyList<ExtractedUnit> Extract(Stream content, ProcessingOptions options, CancellationToken ct)
    {
        var units = new List<ExtractedUnit>();
        long characters = 0;
        var row = 0;
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var sawData = false;

        using var reader = new StreamReader(content, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[8192];
        int read;
        var previous = '\0';

        void EndRow()
        {
            fields.Add(field.ToString());
            field.Clear();
            row++;
            if (fields.Any(f => f.Trim().Length > 0))
            {
                sawData = true;
                var text = string.Join(" | ", fields.Select(f => f.Trim()));
                characters += text.Length;
                if (characters > options.MaxExtractedCharacters || row > options.MaxRows)
                    throw new ExtractionException(FailureCodes.TooLarge);
                units.Add(new ExtractedUnit(text, Row: row));
            }
            fields.Clear();
            ct.ThrowIfCancellationRequested();
        }

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (c == '\0')
                    throw new ExtractionException(FailureCodes.Corrupted);

                if (inQuotes)
                {
                    if (c == '"')
                        inQuotes = false;
                    else
                        field.Append(c);
                }
                else if (c == '"')
                {
                    // A doubled quote inside a quoted field is a literal quote.
                    if (previous == '"') field.Append('"');
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\n' || c == '\r')
                {
                    if (!(c == '\n' && previous == '\r'))
                        EndRow();
                }
                else
                {
                    field.Append(c);
                }

                if (field.Length > options.MaxExtractedCharacters)
                    throw new ExtractionException(FailureCodes.TooLarge);
                previous = c;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
            EndRow();

        if (!sawData)
            throw new ExtractionException(FailureCodes.Empty);
        return units;
    }
}
