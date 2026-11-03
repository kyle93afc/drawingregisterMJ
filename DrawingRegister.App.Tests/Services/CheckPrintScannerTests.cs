using System.Security.Cryptography;
using System.Text;
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
        var path = CopyFixture("no-stamp.pdf");

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

    [Theory]
    [InlineData("controlled-approved-with-comments.pdf", CheckStatus.AWC, "AWC — approved with comments", null)]
    [InlineData("legacy-approved-with-comments.pdf", CheckStatus.AWC, "AWC — approved with comments", null)]
    [InlineData("controlled-approved.pdf", CheckStatus.APPD, "APPD — approved", null)]
    [InlineData("legacy-approved.pdf", CheckStatus.APPD, "APPD — approved", null)]
    [InlineData("checked.pdf", CheckStatus.AWC, "AWC — approved with comments", "Steve Walker")]
    [InlineData("generic-stamp.pdf", CheckStatus.AWC, "AWC — approved with comments", null)]
    public void Plan_derives_controlled_and_legacy_verdict_stamps(
        string fixture, CheckStatus expected, string expectedText, string? expectedAuthor)
    {
        CopyFixture(fixture);

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(expected, fact.Status);
        Assert.Equal(expectedText, fact.StatusText);
        Assert.False(fact.IsFlagged);
        if (expectedAuthor != null)
            Assert.Equal(expectedAuthor, fact.StampAuthor);
    }

    [Fact]
    public void Plan_keeps_backdraft_independent_from_a_recognised_verdict()
    {
        CopyFixture("approved-with-comments-back-drafted.pdf");

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(CheckStatus.AWC, fact.Status);
        Assert.True(fact.BackDrafted);
        Assert.False(fact.IsFlagged);
    }

    [Theory]
    [InlineData("no-comments.pdf", false)]
    [InlineData("back-drafted.pdf", true)]
    public void Plan_keeps_non_verdict_stamps_visible_and_backdraft_independent(string fixture, bool backDrafted)
    {
        CopyFixture(fixture);

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(CheckStatus.UNKNOWN, fact.Status);
        Assert.Equal("UNKNOWN — review required", fact.StatusText);
        Assert.Equal(backDrafted, fact.BackDrafted);
        Assert.True(fact.IsFlagged);
    }

    [Fact]
    public void Plan_lets_verdict_stamp_win_over_CHECKED()
    {
        WritePdfWithStamps(
            Path.Combine(_folder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP01-PLAN.pdf"),
            new StampAnnotation("CHECKED", "k.greig", "D:20260902155833Z"),
            new StampAnnotation("Approved", "k.greig", "D:20260902155948Z"));

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(CheckStatus.APPD, fact.Status);
        Assert.False(fact.IsFlagged);
    }

    [Theory]
    [InlineData("Comments")]
    [InlineData("Not Approved")]
    [InlineData("Revise")]
    public void Plan_treats_revise_style_stamps_as_COMMENTS(string subject)
    {
        WritePdfWithStamps(
            Path.Combine(_folder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP01-PLAN.pdf"),
            new StampAnnotation(subject, "Michael", "D:20260825113747Z"));

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(CheckStatus.COMMENTS, fact.Status);
        Assert.Equal("COMMENTS — checked, technician action required", fact.StatusText);
        Assert.False(fact.IsFlagged);
    }

    [Fact]
    public void Plan_reports_conflicting_verdict_annotations()
    {
        WritePdfWithStamps(
            Path.Combine(_folder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP01-PLAN.pdf"),
            new StampAnnotation("Approved", "Michael", "D:20241031124921Z"),
            new StampAnnotation("Approved with Comments", "Mark", "D:20241031125000Z"));

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(CheckStatus.CONFLICT, fact.Status);
        Assert.Equal("CONFLICT — review required", fact.StatusText);
        Assert.True(fact.IsFlagged);
    }

    [Theory]
    [InlineData("controlled-approved-with-comments.pdf", "W. Bonfim", 2024, 11, 27, 16, 40, 57)]
    [InlineData("back-drafted.pdf", "H. McArthur", 2024, 11, 20, 11, 52, 46)]
    public void Plan_reads_self_asserted_attribution_and_normalises_observed_aliases(
        string fixture, string expectedAuthor, int year, int month, int day, int hour, int minute, int second)
    {
        CopyFixture(fixture);

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(expectedAuthor, fact.StampAuthor);
        Assert.Equal(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc), fact.StampDate);
    }

    [Theory]
    [InlineData("124997-M+J-S1-XX-DR-S-00-01-T01-GENERAL NOTES-CP01.pdf", "124997-M+J-S1-XX-DR-S-00-01", "T01", 1)]
    [InlineData("124997-M+J-V1-XX-DR-S-20-01-P01-PROPOSED ROOF ALTERATIONS – GENERAL ARRANGEMENT SECTIONS & DETAILS-CP03.pdf", "124997-M+J-V1-XX-DR-S-20-01", "P01", 3)]
    [InlineData("124997-M+J-S1-XX-DR-S-00-01-T01-GENERAL NOTES CP 2.pdf", "124997-M+J-S1-XX-DR-S-00-01", "T01", 2)]
    [InlineData("124997-M+J-S1-XX-DR-S-00-01-T01-GENERAL NOTES_CP_04.pdf", "124997-M+J-S1-XX-DR-S-00-01", "T01", 4)]
    public void Plan_reads_cp_token_after_description(string fileName, string documentCode, string revision, int cp)
    {
        WriteMinimalPdf(Path.Combine(_folder, fileName));

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.Equal(documentCode, fact.DocumentCode);
        Assert.Equal(revision, fact.Revision);
        Assert.Equal(cp, fact.Cp);
        Assert.False(fact.IsFlagged);
    }

    [Theory]
    [InlineData("124997-M+J-S1-XX-DR-S-00-01-T01-GENERAL NOTES.pdf")]
    [InlineData("124997-M+J-S1-XX-DR-S-00-01-GENERAL NOTES-CP01.pdf")]
    [InlineData("124997-M+J-S1-XX-DR-S-00-01-T01-SCOPE OF CP01 WORKS.pdf")]
    public void Plan_warns_without_inventing_values_when_revision_or_cp_is_missing(string fileName)
    {
        WriteMinimalPdf(Path.Combine(_folder, fileName));

        var fact = Assert.Single(CheckPrintScanner.Plan(_folder).Entries);

        Assert.True(string.IsNullOrEmpty(fact.DocumentCode));
        Assert.Equal(0, fact.Cp);
        Assert.Equal("Filename does not match the check-print convention.", fact.Issue);
    }

    private string CopyFixture(string fixture)
    {
        var destination = Path.Combine(_folder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP01-PLAN.pdf");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CheckPrints", fixture), destination);
        return destination;
    }

    private static void WriteMinimalPdf(string path)
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4, true);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WritePdfWithStamps(string path, params StampAnnotation[] stamps)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [{string.Join(" ", Enumerable.Range(4, stamps.Length).Select(number => $"{number} 0 R"))}] >>"
        };
        objects.AddRange(stamps.Select(stamp =>
            $"<< /Type /Annot /Subtype /Stamp /Rect [0 0 10 10] /Subj ({Escape(stamp.Subject)}) /T ({Escape(stamp.Author)}) /M ({Escape(stamp.ModifiedDate)}) >>"));

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            pdf.Append($"{offset:0000000000} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private sealed record StampAnnotation(string Subject, string Author, string ModifiedDate);
}
