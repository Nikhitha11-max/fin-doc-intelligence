using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace FinDoc.Tests.Processing;

// Builds small, valid test files in memory so no real documents are needed.
public static class TestFiles
{
    public static byte[] Pdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            if (text.Length > 0)
                page.AddText(text, 12, new PdfPoint(25, 700), font);
        }
        return builder.Build();
    }

    public static byte[] EncryptedPdf() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Processing", "Fixtures", "encrypted.pdf"));

    public static byte[] Csv(params string[] lines) => Encoding.UTF8.GetBytes(string.Join("\n", lines));

    // First sheet stores text as shared strings, second as inline strings plus a number.
    public static byte[] Xlsx()
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var workbook = doc.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var sheets = workbook.Workbook.AppendChild(new Sheets());

            var sharedPart = workbook.AddNewPart<SharedStringTablePart>();
            sharedPart.SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text("Account")), new SharedStringItem(new Text("Revenue")),
                new SharedStringItem(new Text("Cash")), new SharedStringItem(new Text("Debt")));
            sharedPart.SharedStringTable.Save();

            AddSheet(workbook, sheets, 1, "Balance", [
                [Shared(0), Shared(1)],
                [Shared(2), Number("1500")],
                [Shared(3), Number("700")],
            ]);
            AddSheet(workbook, sheets, 2, "Notes", [
                [Inline("Reviewed"), Inline("yes")],
            ]);
            workbook.Workbook.Save();
        }
        return ms.ToArray();
    }

    public static byte[] EmptyXlsx()
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var workbook = doc.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var sheets = workbook.Workbook.AppendChild(new Sheets());
            AddSheet(workbook, sheets, 1, "Empty", []);
            workbook.Workbook.Save();
        }
        return ms.ToArray();
    }

    // A tiny ZIP whose single entry inflates to far more than the allowed size.
    public static byte[] XlsxBomb(int megabytes = 60)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize).Open();
            var zeros = new byte[1024 * 1024];
            for (var i = 0; i < megabytes; i++)
                entry.Write(zeros, 0, zeros.Length);
        }
        return ms.ToArray();
    }

    public static byte[] OleEncryptedXlsx() => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];

    private static Cell Shared(int index) => new() { DataType = CellValues.SharedString, CellValue = new CellValue(index.ToString()) };
    private static Cell Inline(string text) => new() { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) };
    private static Cell Number(string value) => new() { CellValue = new CellValue(value) };

    private static void AddSheet(WorkbookPart workbook, Sheets sheets, uint id, string name, Cell[][] rows)
    {
        var part = workbook.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        part.Worksheet = new Worksheet(data);
        uint index = 1;
        foreach (var cells in rows)
        {
            var row = new Row { RowIndex = index++ };
            foreach (var cell in cells) row.Append(cell);
            data.Append(row);
        }
        part.Worksheet.Save();
        sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = id, Name = name });
    }
}
