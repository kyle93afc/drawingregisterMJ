using System.IO;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public enum DocumentSearchType
{
    DocumentNumber,
    Description,
    Package,
    Type
}

public sealed class DocumentFilterCriteria
{
    public string SearchText { get; set; } = string.Empty;
    public DocumentSearchType SearchType { get; set; } = DocumentSearchType.DocumentNumber;
    public DateTime? SelectedIssueDate { get; set; }
    public string? SelectedSubfolderPath { get; set; }
    public string PurposeCode { get; set; } = string.Empty;
    public string MethodCode { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
}

public static class DocumentFilterService
{
    public static IReadOnlyList<DocumentMetadata> Filter(IEnumerable<DocumentMetadata> documents, DocumentFilterCriteria criteria)
    {
        var filtered = documents.ToList();

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            filtered = criteria.SearchType switch
            {
                DocumentSearchType.DocumentNumber => filtered
                    .Where(document => document.DocumentNumber.Contains(criteria.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                DocumentSearchType.Description => filtered
                    .Where(document => document.Description.Contains(criteria.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                DocumentSearchType.Package => filtered
                    .Where(document => document.Package.Contains(criteria.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                DocumentSearchType.Type => filtered
                    .Where(document => document.DocumentType.Contains(criteria.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                _ => filtered
            };
        }

        if (criteria.SelectedIssueDate.HasValue)
        {
            var selectedDate = criteria.SelectedIssueDate.Value.Date;

            filtered = filtered
                .Where(document => document.RevisionHistory.Keys.Any(issueDate => issueDate.Date == selectedDate))
                .ToList();

            if (!string.IsNullOrWhiteSpace(criteria.SelectedSubfolderPath))
            {
                filtered = filtered
                    .Where(document => document.RevisionHistory.Any(entry =>
                        entry.Key.Date == selectedDate &&
                        !string.IsNullOrEmpty(entry.Value.FilePath) &&
                        string.Equals(
                            Path.GetDirectoryName(entry.Value.FilePath),
                            criteria.SelectedSubfolderPath,
                            StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(criteria.PurposeCode))
        {
            filtered = ApplyRevisionFilter(
                filtered,
                criteria.SelectedIssueDate,
                revision => revision.Purpose.StartsWith(criteria.PurposeCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.MethodCode))
        {
            filtered = ApplyRevisionFilter(
                filtered,
                criteria.SelectedIssueDate,
                revision => revision.Method.StartsWith(criteria.MethodCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.IssuedBy))
        {
            filtered = ApplyRevisionFilter(
                filtered,
                criteria.SelectedIssueDate,
                revision => revision.IssuedBy.Contains(criteria.IssuedBy, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderBy(document => document.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<DocumentMetadata> ApplyRevisionFilter(
        IEnumerable<DocumentMetadata> documents,
        DateTime? selectedIssueDate,
        Func<RevisionInfo, bool> predicate)
    {
        if (selectedIssueDate.HasValue)
        {
            var selectedDate = selectedIssueDate.Value.Date;

            return documents
                .Where(document => document.RevisionHistory.Any(entry =>
                    entry.Key.Date == selectedDate &&
                    predicate(entry.Value)))
                .ToList();
        }

        return documents
            .Where(document => document.RevisionHistory.Values.Any(predicate))
            .ToList();
    }
}
