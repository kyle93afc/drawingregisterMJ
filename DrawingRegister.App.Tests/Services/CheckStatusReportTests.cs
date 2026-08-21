using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class CheckStatusReportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dr-check-report-test-{Guid.NewGuid():N}");

    public CheckStatusReportTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { }
    }

    [Fact]
    public void WriteCsv_exports_the_rendered_status_fields_and_escapes_values()
    {
        var check = new CheckPrint
        {
            DocumentCode = "DOC-01",
            Revision = "A",
            Cp = 2,
            Status = CheckStatus.AWC,
            BackDrafted = true,
            StampAuthor = "Greig, \"Kyle\"",
            StampDate = new DateTime(2026, 8, 21, 10, 30, 0, DateTimeKind.Utc),
            FilePath = @"C:\Checks\DOC-01-A-CP02.pdf",
            Issue = "Review, then retry"
        };
        var revision = new RevisionInfo
        {
            IsDistributed = false,
            IssuedBy = "MJ",
            Purpose = "Construction",
            Method = "Email"
        };
        var row = new CheckPrintQueueRow(check, new DateTime(2026, 8, 20), revision, true, "AWC — approved with comments");
        using var writer = new StringWriter();

        CheckStatusReport.WriteCsv([row], writer);

        Assert.Equal(
            "Document Code,Revision,CP,Verdict,Back-Drafted,Markup Author,Markup Date UTC,Current Revision,Distribution,Issue Date,Issued By,Purpose,Method,Queue Reason,File Path,Scan Warning\r\n" +
            "DOC-01,A,2,AWC — approved with comments,True,\"Greig, \"\"Kyle\"\"\",2026-08-21 10:30:00,True,Not distributed,2026-08-20,MJ,Construction,Email,AWC — approved with comments,C:\\Checks\\DOC-01-A-CP02.pdf,\"Review, then retry\"\r\n",
            writer.ToString());
    }

    [Fact]
    public void Run_matches_the_desktop_pipeline_and_does_not_mutate_inputs()
    {
        var projectFolder = Path.Combine(_root, "project");
        var checkingFolder = Path.Combine(_root, "checking");
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(checkingFolder);

        var document = new DocumentMetadata { DocumentNumber = "124660-M+J-V1-XX-DR-A-01-02" };
        document.RevisionHistory[new DateTime(2026, 8, 20)] = new RevisionInfo
        {
            Revision = "1A",
            Purpose = "Construction",
            Method = "Email",
            IssuedBy = "MJ"
        };
        var storage = new ProjectStorage
        {
            Documents =
            [
                new DocumentStorageInfo
                {
                    DocumentNumber = document.DocumentNumber,
                    RevisionHistory = document.RevisionHistory.ToDictionary(
                        entry => entry.Key,
                        entry => new RevisionStorageInfo
                        {
                            Revision = entry.Value.Revision,
                            Purpose = entry.Value.Purpose,
                            Method = entry.Value.Method,
                            IssuedBy = entry.Value.IssuedBy
                        })
                }
            ]
        };
        var storagePath = Path.Combine(projectFolder, "project_data.json");
        storage.Save(storagePath);

        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "CheckPrints", "no-stamp.pdf"),
            Path.Combine(checkingFolder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP01-PLAN.pdf"));
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "CheckPrints", "no-stamp.pdf"),
            Path.Combine(checkingFolder, "not-a-check-print.pdf"));
        File.WriteAllText(
            Path.Combine(checkingFolder, "124660-M+J-V1-XX-DR-A-01-02-1A-CP02-BROKEN.pdf"),
            "not a pdf");

        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        var before = files.ToDictionary(path => path, File.ReadAllBytes);
        var direct = CheckStatusReport.Render(
            CheckPrintApplier.Apply(CheckPrintScanner.Plan(checkingFolder)),
            [document]);

        var headless = CheckStatusReport.Run(projectFolder, checkingFolder);

        using var directCsv = new StringWriter();
        using var headlessCsv = new StringWriter();
        CheckStatusReport.WriteCsv(direct, directCsv);
        CheckStatusReport.WriteCsv(headless, headlessCsv);
        Assert.Equal(directCsv.ToString(), headlessCsv.ToString());
        Assert.Equal(3, headless.Count);
        Assert.Contains(headless, row => row.CheckPrint.Issue.Contains("filename", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headless, row => row.CheckPrint.Issue.Contains("PDF", StringComparison.OrdinalIgnoreCase));
        Assert.All(before, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
    }
}
