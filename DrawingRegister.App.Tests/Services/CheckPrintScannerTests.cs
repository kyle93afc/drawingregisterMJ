using System.Security.Cryptography;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace DrawingRegister.App.Tests.Services;

public sealed class CheckPrintScannerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"dr-check-print-test-{Guid.NewGuid():N}");

    public CheckPrintScannerTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch { }
    }

    [Fact]
    public void Plan_returns_unannotated_check_print_as_FC()
    {
        var path = Path.Combine(_folder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP01-FC-GROUND FLOOR PLAN.pdf");
        WriteMinimalPdf(path);

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal("124660-M+J-V1-XX-DR-A-01-02", fact.DocumentCode);
        Assert.Equal("1A", fact.Revision);
        Assert.Equal(1, fact.Cp);
        Assert.Equal(CheckStatus.FC, fact.Status);
        Assert.Equal("FC — no stamp annotation found", fact.StatusText);
        Assert.Equal(path, fact.FilePath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), fact.SourceHash);
        Assert.False(fact.IsFlagged);
    }

    [Fact]
    public void Plan_keeps_nonconforming_and_malformed_pdfs_as_flagged_rows()
    {
        WriteMinimalPdf(Path.Combine(_folder, "not-a-check-print.pdf"));
        File.WriteAllText(
            Path.Combine(_folder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP02-FC-BROKEN.pdf"),
            "not a pdf");

        var facts = CheckPrintScanner.Plan(_folder).Entries;

        Assert.Equal(2, facts.Count);
        Assert.All(facts, fact => Assert.True(fact.IsFlagged));
        Assert.Contains(facts, fact => fact.Issue.Contains("filename", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(facts, fact => fact.Issue.Contains("PDF", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_is_read_only_and_Apply_is_a_no_op_pass_through()
    {
        var path = Path.Combine(_folder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP01-FC-PLAN.pdf");
        WriteMinimalPdf(path);
        var before = File.ReadAllBytes(path);
        var lastWrite = File.GetLastWriteTimeUtc(path);

        var plan = CheckPrintScanner.Plan(_folder);
        var result = CheckPrintApplier.Apply(plan);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(path));
        Assert.Same(plan.Entries, result.Facts);
    }

    private static void WriteMinimalPdf(string path)
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4, true);
        File.WriteAllBytes(path, builder.Build());
    }
}
