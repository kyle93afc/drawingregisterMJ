using System.Text.RegularExpressions;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Helpers;

internal static class DrawingRegisterPdfReportBuilder
{
    private static readonly IReadOnlyDictionary<string, string> PurposeDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["S"] = "Concept Issue",
        ["P"] = "Planning Issue",
        ["T"] = "Warrant / Tender",
        ["C"] = "Construction Issue",
        ["A"] = "Approval",
        ["I"] = "Information",
        ["W"] = "Warrant"
    };

    internal static IReadOnlyList<DrawingRegisterPdfReportRow> BuildFullRegisterRows(IEnumerable<DocumentMetadata> documents)
    {
        return documents
            .OrderBy(document => document.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .Select(CreateRow)
            .ToList();
    }

    internal static string GetFullRegisterSummaryNote()
    {
        return "This document is a comprehensive Drawing Register containing all project drawings. For specific drawing distributions, please refer to Transmittals. REVISION TRAIL: ordered newest to oldest by issue date.";
    }

    private static DrawingRegisterPdfReportRow CreateRow(DocumentMetadata document)
    {
        var orderedHistory = document.RevisionHistory
            .OrderByDescending(entry => entry.Key)
            .ToList();

        var latestEntry = orderedHistory.FirstOrDefault();
        var latestRevision = latestEntry.Value?.Revision ?? string.Empty;
        var latestIssueDate = latestEntry.Equals(default(KeyValuePair<DateTime, RevisionInfo>))
            ? (DateTime?)null
            : latestEntry.Key.Date;
        var latestPurpose = string.IsNullOrWhiteSpace(latestEntry.Value?.Purpose)
            ? "Not Specified"
            : GetPurposeDisplayName(latestEntry.Value.Purpose);

        return new DrawingRegisterPdfReportRow(
            document.DocumentNumber,
            document.Description,
            document.Package,
            document.DocumentType,
            document.Size,
            latestRevision,
            latestIssueDate,
            FormatRevisionTrail(orderedHistory),
            latestPurpose);
    }

    private static string FormatRevisionTrail(IEnumerable<KeyValuePair<DateTime, RevisionInfo>> orderedHistory)
    {
        return string.Join(Environment.NewLine,
            orderedHistory.Select(entry => $"REV {GetRevisionCode(entry.Value)} - {entry.Key:dd/MM/yyyy}"));
    }

    private static string GetRevisionCode(RevisionInfo? revisionInfo)
    {
        return revisionInfo?.Revision?.Trim() ?? string.Empty;
    }

    private static string GetPurposeDisplayName(string purpose)
    {
        var normalizedPurpose = purpose.Trim();
        return PurposeDisplayNames.TryGetValue(normalizedPurpose, out var displayName)
            ? displayName
            : normalizedPurpose;
    }

    internal sealed record DrawingRegisterPdfReportRow(
        string DocumentNumber,
        string Description,
        string Package,
        string DocumentType,
        string Size,
        string LatestRevision,
        DateTime? LatestIssueDate,
        string RevisionTrail,
        string Status);
}
