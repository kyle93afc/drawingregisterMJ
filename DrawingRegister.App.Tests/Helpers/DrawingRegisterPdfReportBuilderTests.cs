using DrawingRegister.App.Helpers;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Helpers;

public sealed class DrawingRegisterPdfReportBuilderTests
{
    [Fact]
    public void Full_register_rows_do_not_expose_revision_trail_or_status_columns()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-000",
                ("1", "Information", new DateTime(2026, 4, 1)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();
        var propertyNames = row.GetType().GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain("RevisionTrail", propertyNames);
        Assert.DoesNotContain("Status", propertyNames);
        Assert.Null(typeof(DrawingRegisterPdfReportBuilder).GetMethod(
            "GetFullRegisterSummaryNote",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void BuildFullRegisterRows_uses_latest_revision_and_issue_date()
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
        Assert.Equal(new DateTime(2026, 4, 14), row.LatestIssueDate);
    }

    [Fact]
    public void BuildFullRegisterRows_uses_latest_issue_date_when_same_revision_is_reissued()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-003",
                ("C01", "Information", new DateTime(2026, 4, 1)),
                ("C01", "Construction", new DateTime(2026, 4, 14)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();

        Assert.Equal("C01", row.LatestRevision);
        Assert.Equal(new DateTime(2026, 4, 14), row.LatestIssueDate);
    }

    [Fact]
    public void BuildFullRegisterRows_preserves_missing_revision_codes()
    {
        var documents = new[]
        {
            CreateDocument(
                "DR-004",
                ("-", "Information", new DateTime(2026, 4, 14)))
        };

        var row = DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documents).Single();

        Assert.Equal("-", row.LatestRevision);
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
