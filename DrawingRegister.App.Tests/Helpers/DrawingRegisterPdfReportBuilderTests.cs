using DrawingRegister.App.Helpers;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Helpers;

public sealed class DrawingRegisterPdfReportBuilderTests
{
    [Fact]
    public void BuildFullRegisterRows_formats_revision_trail_newest_first()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-001",
                ("1", "Information", new DateTime(2026, 3, 12)),
                ("2", "Tender", new DateTime(2026, 4, 1)),
                ("3", "Construction", new DateTime(2026, 4, 14)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();

        Assert.Equal("3", row.LatestRevision);
        Assert.Equal(new DateTime(2026, 4, 14), row.LatestIssueDate);
        Assert.Equal(
            string.Join(Environment.NewLine, "REV 3 - 14/04/2026", "REV 2 - 01/04/2026", "REV 1 - 12/03/2026"),
            row.RevisionTrail);
        Assert.Equal("Construction", row.Status);
    }

    [Fact]
    public void BuildFullRegisterRows_uses_latest_issued_revision_when_history_reverts()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-002",
                ("3", "Construction", new DateTime(2026, 4, 1)),
                ("1", "Construction", new DateTime(2026, 4, 14)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();

        Assert.Equal("1", row.LatestRevision);
        Assert.Equal("Construction", row.Status);
    }

    [Fact]
    public void BuildFullRegisterRows_uses_latest_purpose_as_status_when_same_revision_is_reissued()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-003",
                ("C01", "Information", new DateTime(2026, 4, 1)),
                ("C01", "Construction", new DateTime(2026, 4, 14)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();

        Assert.Equal("Construction", row.Status);
    }

    [Fact]
    public void BuildFullRegisterRows_preserves_missing_revision_codes_in_trail()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-004",
                ("-", "Information", new DateTime(2026, 4, 14)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();

        Assert.Equal("Information", row.Status);
        Assert.Equal("REV - - 14/04/2026", row.RevisionTrail);
    }

    [Fact]
    public void BuildFullRegisterRows_expands_coded_purpose_to_full_status_name()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-005",
                ("1", "I", new DateTime(2026, 4, 14)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();

        Assert.Equal("Information", row.Status);
    }

    [Fact]
    public void GetFullRegisterSummaryNote_mentions_revision_trail_order()
    {
        var note = DrawingRegisterPdfReportBuilder.GetFullRegisterSummaryNote();

        Assert.Contains("newest to oldest", note);
        Assert.Contains("REVISION TRAIL", note);
    }

    private static DocumentMetadata CreateDocument(string documentNumber, params (string Revision, string Purpose, DateTime Date)[] history)
    {
        var document = new DocumentMetadata
        {
            DocumentNumber = documentNumber,
            Description = "Test drawing",
            Package = "01",
            DocumentType = "DR",
            Size = "A1",
            FilePath = $@"C:\Project\latest\{documentNumber}.pdf"
        };

        foreach (var entry in history)
        {
            document.RevisionHistory[entry.Date] = new RevisionInfo
            {
                Revision = entry.Revision,
                Purpose = entry.Purpose,
                FilePath = $@"C:\Project\{entry.Date:yyyyMMdd}\{documentNumber}.pdf"
            };
        }

        return document;
    }
}
