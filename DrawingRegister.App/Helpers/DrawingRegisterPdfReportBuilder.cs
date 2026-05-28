using DrawingRegister.App.Models;

namespace DrawingRegister.App.Helpers;

internal static class DrawingRegisterPdfReportBuilder
{
    internal static IReadOnlyList<DrawingRegisterPdfReportRow> BuildFullRegisterRows(IEnumerable<DocumentMetadata> documents)
    {
        return documents
            .OrderBy(document => document.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .Select(CreateRow)
            .ToList();
    }

    private static DrawingRegisterPdfReportRow CreateRow(DocumentMetadata document)
    {
        var orderedHistory = document.RevisionHistory
            .OrderByDescending(entry => entry.Key)
            .ToList();

        // Latest revision (excluding superseded entries) drives the register's current-state columns.
        var latestEntry = orderedHistory.FirstOrDefault(e => !e.Value.IsSuperseded);
        var latestRevision = latestEntry.Value?.Revision ?? string.Empty;
        var latestIssueDate = latestEntry.Value == null
            ? (DateTime?)null
            : latestEntry.Key.Date;

        return new DrawingRegisterPdfReportRow(
            document.DocumentNumber,
            document.Description,
            document.Package,
            document.DocumentType,
            document.Size,
            latestRevision,
            latestIssueDate);
    }

    internal sealed record DrawingRegisterPdfReportRow(
        string DocumentNumber,
        string Description,
        string Package,
        string DocumentType,
        string Size,
        string LatestRevision,
        DateTime? LatestIssueDate);
}
