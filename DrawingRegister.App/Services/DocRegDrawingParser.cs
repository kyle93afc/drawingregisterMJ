using System.IO;
using System.Text.RegularExpressions;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public static class DocRegDrawingParser
{
    private static readonly Regex ExplicitDashRevision = new(
        @"^(?<head>.+?)\s+-\s+(?:Rev(?:ision)?)\s+(?<rev>[A-Za-z0-9]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExplicitUnderscoreRevision = new(
        @"^(?<head>.+)_(?:Rev(?:ision)?)_(?<rev>[A-Za-z0-9]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SheetUnderscoreHead = new(
        @"^(?<ref>[A-Za-z]{1,3}-\d+[A-Za-z]?)_(?<title>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex AmbiguousReferenceSuffix = new(
        @"\d+[A-Za-z]$",
        RegexOptions.Compiled);

    private static readonly Regex ProjectNumberToken = new(
        @"^(?:\d{4,6}|[A-Z]{1,4}\d{1,4}[-_]\d{2,6})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static DocRegDrawing ParseFile(string filePath, OrganizationProfile organization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(organization);

        var fileName = Path.GetFileName(filePath);
        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(stem))
            return Unknown(filePath, fileName, stem);

        if (DrawingFilenameParser.TryParse(stem, out var identity))
        {
            return new DocRegDrawing
            {
                FilePath = filePath,
                FileName = fileName,
                Include = true,
                Section = IsInHouseOriginator(identity.Originator, organization)
                    ? DocRegSection.InHouse
                    : DocRegSection.ArchitectExternal,
                DocumentNumber = identity.DocumentCode,
                Description = identity.Description ?? string.Empty,
                Revision = identity.Revision ?? string.Empty,
                Package = identity.Package ?? string.Empty,
                DocumentType = identity.DocumentType ?? string.Empty,
                ParsedProjectNumber = string.IsNullOrWhiteSpace(identity.ProjectNumber)
                    ? null
                    : identity.ProjectNumber
            };
        }

        var dashRevision = ExplicitDashRevision.Match(stem);
        if (dashRevision.Success)
        {
            var parsed = SplitDashParts(dashRevision.Groups["head"].Value);
            return ArchitectDrawing(
                filePath,
                fileName,
                parsed.DocumentNumber,
                parsed.Description,
                dashRevision.Groups["rev"].Value,
                parsed.ProjectNumber,
                AmbiguousNote(parsed.DocumentNumber, hasExplicitRevision: true));
        }

        var underscoreRevision = ExplicitUnderscoreRevision.Match(stem);
        if (underscoreRevision.Success)
        {
            var parsed = SplitUnderscoreHead(underscoreRevision.Groups["head"].Value);
            return ArchitectDrawing(
                filePath,
                fileName,
                parsed.DocumentNumber,
                parsed.Description,
                underscoreRevision.Groups["rev"].Value,
                parsed.ProjectNumber,
                AmbiguousNote(parsed.DocumentNumber, hasExplicitRevision: true));
        }

        var dashParts = stem.Split([" - "], StringSplitOptions.None)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();
        if (dashParts.Length >= 2)
        {
            var parsed = SplitDashParts(stem);
            return ArchitectDrawing(
                filePath,
                fileName,
                parsed.DocumentNumber,
                parsed.Description,
                string.Empty,
                parsed.ProjectNumber,
                AmbiguousNote(parsed.DocumentNumber, hasExplicitRevision: false));
        }

        var sheetOnly = SheetUnderscoreHead.Match(stem);
        if (sheetOnly.Success)
        {
            var documentNumber = sheetOnly.Groups["ref"].Value;
            var description = sheetOnly.Groups["title"].Value.Replace('_', ' ').Trim();
            return ArchitectDrawing(
                filePath,
                fileName,
                documentNumber,
                description,
                string.Empty,
                null,
                AmbiguousNote(documentNumber, hasExplicitRevision: false));
        }

        return Unknown(filePath, fileName, stem);
    }

    private static bool IsInHouseOriginator(string? originator, OrganizationProfile organization)
    {
        if (string.IsNullOrWhiteSpace(originator))
            return false;

        var value = originator.Trim();
        if (value.Equals(organization.OriginatorCode, StringComparison.OrdinalIgnoreCase))
            return true;

        return organization.Id.Equals("MJ", StringComparison.OrdinalIgnoreCase)
            && (value.Equals("MJ", StringComparison.OrdinalIgnoreCase)
                || value.Equals("M+J", StringComparison.OrdinalIgnoreCase));
    }

    private static DocRegDrawing ArchitectDrawing(
        string filePath,
        string fileName,
        string documentNumber,
        string description,
        string revision,
        string? projectNumber,
        string parseNote)
    {
        return new DocRegDrawing
        {
            FilePath = filePath,
            FileName = fileName,
            Include = true,
            Section = DocRegSection.ArchitectExternal,
            DocumentNumber = documentNumber,
            Description = description,
            Revision = revision,
            ParsedProjectNumber = projectNumber,
            ParseNote = parseNote
        };
    }

    private static DocRegDrawing Unknown(string filePath, string fileName, string stem)
    {
        return new DocRegDrawing
        {
            FilePath = filePath,
            FileName = fileName,
            Include = true,
            Section = DocRegSection.ArchitectExternal,
            DocumentNumber = stem,
            ParseNote = "Filename did not match a recognised drawing convention. Review reference and title."
        };
    }

    private static (string DocumentNumber, string Description, string? ProjectNumber) SplitDashParts(string text)
    {
        var parts = text.Split([" - "], StringSplitOptions.None)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();

        if (parts.Length == 0)
            return (text.Trim(), string.Empty, null);

        if (parts.Length == 1)
            return (parts[0], string.Empty, TryProjectNumber(parts[0]));

        if (parts.Length == 2)
            return (parts[0], parts[1], TryProjectNumber(parts[0]));

        var leadingProject = TryProjectNumber(parts[0]);
        if (leadingProject != null)
        {
            var documentNumber = parts.Length == 3 ? parts[1] : parts[2];
            var descriptionStart = parts.Length == 3 ? 2 : 3;
            return (documentNumber, string.Join(" - ", parts.Skip(descriptionStart)), leadingProject);
        }

        return (parts[0], string.Join(" - ", parts.Skip(1)), null);
    }

    private static (string DocumentNumber, string Description, string? ProjectNumber) SplitUnderscoreHead(string head)
    {
        var sheet = SheetUnderscoreHead.Match(head);
        if (sheet.Success)
        {
            return (
                sheet.Groups["ref"].Value,
                sheet.Groups["title"].Value.Replace('_', ' ').Trim(),
                null);
        }

        var parts = head.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
            return (parts[0], string.Join(' ', parts.Skip(1)), TryProjectNumber(parts[0]));

        return (head.Trim(), string.Empty, TryProjectNumber(head));
    }

    private static string? TryProjectNumber(string token)
    {
        return ProjectNumberToken.IsMatch(token) ? token : null;
    }

    private static string AmbiguousNote(string documentNumber, bool hasExplicitRevision)
    {
        if (hasExplicitRevision || !AmbiguousReferenceSuffix.IsMatch(documentNumber))
            return string.Empty;

        return "Trailing letter on the reference may be a revision. The full reference was kept.";
    }
}
