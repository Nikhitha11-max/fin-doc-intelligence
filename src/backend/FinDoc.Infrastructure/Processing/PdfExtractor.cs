using FinDoc.Application.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace FinDoc.Infrastructure.Processing;

public class PdfExtractor : IDocumentExtractor
{
    public string Extension => ".pdf";

    public IReadOnlyList<ExtractedUnit> Extract(Stream content, ProcessingOptions options, CancellationToken ct)
    {
        var units = new List<ExtractedUnit>();
        long characters = 0;
        try
        {
            using var pdf = PdfDocument.Open(content);
            if (pdf.NumberOfPages == 0)
                throw new ExtractionException(FailureCodes.Empty);
            if (pdf.NumberOfPages > options.MaxPages)
                throw new ExtractionException(FailureCodes.TooLarge);

            foreach (var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var text = ContentOrderTextExtractor.GetText(page);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                characters += text.Length;
                if (characters > options.MaxExtractedCharacters)
                    throw new ExtractionException(FailureCodes.TooLarge);
                units.Add(new ExtractedUnit(text, Page: page.Number));
            }
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new ExtractionException(FailureCodes.Encrypted);
        }
        catch (Exception e) when (e is not ExtractionException and not OperationCanceledException)
        {
            throw new ExtractionException(FailureCodes.Corrupted);
        }

        if (units.Count == 0)
            throw new ExtractionException(FailureCodes.NoExtractableText);
        return units;
    }
}
