using System.Text;
using FinDoc.Application.Abstractions;
using FinDoc.Application.Documents;

namespace FinDoc.Tests;

public class UploadValidatorTests
{
    private static MemoryStream Pdf() => new("%PDF-1.7 body"u8.ToArray());

    [Fact]
    public async Task Accepts_valid_pdf_csv_and_xlsx()
    {
        Assert.Equal(".pdf", await UploadValidator.ValidateAsync("a.PDF", Pdf(), default));
        Assert.Equal(".csv", await UploadValidator.ValidateAsync("a.csv", new MemoryStream(Encoding.UTF8.GetBytes("a,b\n1,2")), default));
        Assert.Equal(".xlsx", await UploadValidator.ValidateAsync("a.xlsx", new MemoryStream([0x50, 0x4B, 0x03, 0x04, 1]), default));
    }

    [Fact]
    public async Task Rejects_empty_file() =>
        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadValidator.ValidateAsync("a.pdf", new MemoryStream(), default));

    [Fact]
    public async Task Rejects_oversized_file() =>
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            UploadValidator.ValidateAsync("a.csv", new MemoryStream(new byte[UploadValidator.MaxSizeBytes + 1]), default));

    [Theory]
    [InlineData("a.exe")]
    [InlineData("a")]
    [InlineData("a.pdf.exe")]
    public async Task Rejects_disallowed_extension(string name) =>
        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadValidator.ValidateAsync(name, Pdf(), default));

    [Fact]
    public async Task Rejects_content_that_does_not_match_extension()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadValidator.ValidateAsync("a.pdf", new MemoryStream("not a pdf"u8.ToArray()), default));
        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadValidator.ValidateAsync("a.xlsx", Pdf(), default));
        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadValidator.ValidateAsync("a.csv", new MemoryStream([0x41, 0x00, 0x42]), default));
    }

    [Fact]
    public async Task Ignores_directory_parts_in_file_name() =>
        Assert.Equal(".pdf", await UploadValidator.ValidateAsync("../../etc/x.pdf", Pdf(), default));
}
