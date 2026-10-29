using System.Text;
using FinDoc.Application.Processing;
using FinDoc.Infrastructure.Processing;

namespace FinDoc.Tests.Processing;

public class ExtractorTests
{
    private static readonly ProcessingOptions Options = new();
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Doc = Guid.NewGuid();

    private static IReadOnlyList<ExtractedUnit> Extract(IDocumentExtractor extractor, byte[] file, ProcessingOptions? options = null) =>
        extractor.Extract(new MemoryStream(file), options ?? Options, default);

    private static string CodeOf(IDocumentExtractor extractor, byte[] file, ProcessingOptions? options = null) =>
        Assert.Throws<ExtractionException>(() => Extract(extractor, file, options)).Code;

    [Fact]
    public void Pdf_pages_keep_their_numbers()
    {
        var units = Extract(new PdfExtractor(), TestFiles.Pdf("First page text", "", "Third page text"));

        Assert.Equal([1, 3], units.Select(u => u.Page));
        Assert.Contains("First page text", units[0].Text);
        Assert.Contains("Third page text", units[1].Text);
    }

    [Fact]
    public void Csv_rows_keep_their_numbers_and_handle_quotes()
    {
        var units = Extract(new CsvExtractor(), TestFiles.Csv("name,amount", "\"Smith, J\",100", "", "\"multi", "line\",\"say \"\"hi\"\"\""));

        Assert.Equal("name | amount", units[0].Text);
        Assert.Equal("Smith, J | 100", units[1].Text);
        Assert.Equal(2, units[1].Row);
        Assert.Equal("multi\nline | say \"hi\"", units[2].Text);
        Assert.Equal(4, units[2].Row);
    }

    [Fact]
    public void Xlsx_rows_keep_sheet_and_row_numbers()
    {
        var units = Extract(new XlsxExtractor(), TestFiles.Xlsx());

        Assert.Equal(
            [("Balance", 1, "Account | Revenue"), ("Balance", 2, "Cash | 1500"), ("Balance", 3, "Debt | 700"), ("Notes", 1, "Reviewed | yes")],
            units.Select(u => (u.Sheet!, u.Row!.Value, u.Text)));
    }

    [Fact]
    public void Chunks_are_ordered_and_carry_source_metadata()
    {
        var options = new ProcessingOptions { ChunkSize = 40, ChunkOverlap = 5 };
        var units = new List<ExtractedUnit>
        {
            new("aaaaaaaaaaaaaaa", Page: 1),
            new("bbbbbbbbbbbbbbb", Page: 2),
            new(new string('c', 90), Page: 3),
        };

        var chunks = ChunkBuilder.Build(Tenant, Doc, units, options);

        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Ordinal));
        Assert.All(chunks, c => { Assert.Equal(Tenant, c.TenantId); Assert.Equal(Doc, c.DocumentId); Assert.True(c.Text.Length <= 40); });
        Assert.Equal((1, 2), (chunks[0].PageStart, chunks[0].PageEnd));
        Assert.All(chunks.Skip(1), c => Assert.Equal((3, 3), (c.PageStart, c.PageEnd)));
        Assert.True(chunks.Count >= 3);
    }

    [Fact]
    public void Chunks_never_mix_sheets_and_keep_row_ranges()
    {
        var units = new List<ExtractedUnit>
        {
            new("one", Sheet: "A", Row: 1), new("two", Sheet: "A", Row: 2), new("three", Sheet: "B", Row: 1),
        };

        var chunks = ChunkBuilder.Build(Tenant, Doc, units, Options);

        Assert.Equal([("A", 1, 2), ("B", 1, 1)], chunks.Select(c => (c.SheetName!, c.RowStart!.Value, c.RowEnd!.Value)));
    }

    [Fact]
    public void Too_many_chunks_is_a_limit_failure()
    {
        var units = Enumerable.Range(1, 50).Select(i => new ExtractedUnit(new string('x', 30), Row: i));
        var ex = Assert.Throws<ExtractionException>(() => ChunkBuilder.Build(Tenant, Doc, units, new ProcessingOptions { ChunkSize = 30, MaxChunks = 10 }));
        Assert.Equal(FailureCodes.TooLarge, ex.Code);
    }

    [Fact]
    public void Corrupted_files_are_reported_not_thrown_as_crashes()
    {
        Assert.Equal(FailureCodes.Corrupted, CodeOf(new PdfExtractor(), "%PDF-1.7 this is not really a pdf"u8.ToArray()));
        Assert.Equal(FailureCodes.Corrupted, CodeOf(new XlsxExtractor(), "PK\u0003\u0004 garbage"u8.ToArray()));
        Assert.Equal(FailureCodes.Corrupted, CodeOf(new CsvExtractor(), [(byte)'a', 0, (byte)'b']));
    }

    [Fact]
    public void Password_protected_files_are_reported_as_encrypted()
    {
        Assert.Equal(FailureCodes.Encrypted, CodeOf(new PdfExtractor(), TestFiles.EncryptedPdf()));
        Assert.Equal(FailureCodes.Encrypted, CodeOf(new XlsxExtractor(), TestFiles.OleEncryptedXlsx()));
    }

    [Fact]
    public void Empty_files_are_reported_as_empty()
    {
        Assert.Equal(FailureCodes.Empty, CodeOf(new CsvExtractor(), TestFiles.Csv(" ", ",,", "")));
        Assert.Equal(FailureCodes.Empty, CodeOf(new XlsxExtractor(), TestFiles.EmptyXlsx()));
    }

    [Fact]
    public void Scanned_pdf_without_text_is_reported_as_no_extractable_text()
    {
        Assert.Equal(FailureCodes.NoExtractableText, CodeOf(new PdfExtractor(), TestFiles.Pdf("", "")));
    }

    [Fact]
    public void Content_over_the_limits_is_too_large()
    {
        var small = new ProcessingOptions { MaxExtractedCharacters = 50, MaxRows = 3, MaxPages = 1 };
        Assert.Equal(FailureCodes.TooLarge, CodeOf(new CsvExtractor(), Encoding.UTF8.GetBytes(new string('x', 500)), small));
        Assert.Equal(FailureCodes.TooLarge, CodeOf(new CsvExtractor(), TestFiles.Csv("a", "b", "c", "d", "e"), new ProcessingOptions { MaxRows = 3 }));
        Assert.Equal(FailureCodes.TooLarge, CodeOf(new PdfExtractor(), TestFiles.Pdf("one", "two"), small));
        Assert.Equal(FailureCodes.TooLarge, CodeOf(new XlsxExtractor(), TestFiles.Xlsx(), new ProcessingOptions { MaxSheets = 1 }));
    }

    [Fact]
    public void Xlsx_decompression_bomb_is_rejected_before_parsing()
    {
        var bomb = TestFiles.XlsxBomb();
        Assert.True(bomb.Length < 1024 * 1024);
        Assert.Equal(FailureCodes.TooLarge, CodeOf(new XlsxExtractor(), bomb));
    }
}
