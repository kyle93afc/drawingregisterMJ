using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public sealed class DocRegScanResult
{
    public required string FolderPath { get; init; }
    public required IReadOnlyList<DocRegDrawing> Drawings { get; init; }
    public int ExcludedGeneratedCount { get; init; }
    public string? ExclusionNote { get; init; }
}

public static class DocRegFolderScanner
{
    public static DocRegScanResult Scan(
        string folderPath,
        OrganizationProfile? organization = null,
        IEnumerable<DocumentMetadata>? registerDocuments = null,
        string? currentProjectNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        var org = organization ?? OrganizationRegistry.Default;
        var register = registerDocuments?.ToList() ?? [];

        var drawings = new List<DocRegDrawing>();
        var excludedGenerated = 0;

        var scanFolders = Directory.EnumerateDirectories(folderPath)
            .Where(IsDatedIssueFolder)
            .Prepend(folderPath);
        foreach (var filePath in scanFolders.SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
                     .Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            var stem = Path.GetFileNameWithoutExtension(filePath);
            var registerStem = Regex.Replace(stem, @" \([1-9]\d*\)$", string.Empty);
            if (DocRegFilenameParser.TryParse(registerStem, out _))
            {
                excludedGenerated++;
                continue;
            }

            var drawing = DocRegDrawingParser.ParseFile(filePath, org);
            EnrichFromRegister(drawing, register);
            NoteProjectMismatch(drawing, currentProjectNumber);
            drawings.Add(drawing);
        }

        return new DocRegScanResult
        {
            FolderPath = folderPath,
            Drawings = drawings,
            ExcludedGeneratedCount = excludedGenerated,
            ExclusionNote = excludedGenerated == 0
                ? null
                : $"Excluded {excludedGenerated} generated DocReg PDF(s) from the review list."
        };
    }

    private static bool IsDatedIssueFolder(string folderPath)
    {
        var match = Regex.Match(Path.GetFileName(folderPath), @"^(?<date>\d{8})(?:$|[-_\s])");
        return match.Success && DateTime.TryParseExact(match.Groups["date"].Value,
            "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static void EnrichFromRegister(DocRegDrawing drawing, IReadOnlyList<DocumentMetadata> register)
    {
        if (string.IsNullOrWhiteSpace(drawing.DocumentNumber) || string.IsNullOrWhiteSpace(drawing.Revision))
            return;

        var document = register.FirstOrDefault(candidate =>
            string.Equals(candidate.DocumentNumber, drawing.DocumentNumber, StringComparison.OrdinalIgnoreCase));
        if (document == null)
            return;

        var matchedRevision = document.RevisionHistory
            .FirstOrDefault(entry =>
                string.Equals(entry.Value.Revision, drawing.Revision, StringComparison.OrdinalIgnoreCase));
        if (matchedRevision.Value == null)
            return;

        drawing.IssueDate = matchedRevision.Key.Date;
        if (string.IsNullOrWhiteSpace(drawing.Package))
            drawing.Package = document.Package ?? string.Empty;
        if (string.IsNullOrWhiteSpace(drawing.DocumentType))
            drawing.DocumentType = document.DocumentType ?? string.Empty;
        if (string.IsNullOrWhiteSpace(drawing.Size))
            drawing.Size = document.Size ?? string.Empty;
    }

    private static void NoteProjectMismatch(DocRegDrawing drawing, string? currentProjectNumber)
    {
        if (drawing.Section != DocRegSection.InHouse)
            return;
        if (string.IsNullOrWhiteSpace(drawing.ParsedProjectNumber) || string.IsNullOrWhiteSpace(currentProjectNumber))
            return;
        if (drawing.ParsedProjectNumber.Equals(currentProjectNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        var mismatch = "In-house project number does not match the export project number.";
        drawing.ParseNote = string.IsNullOrWhiteSpace(drawing.ParseNote)
            ? mismatch
            : $"{drawing.ParseNote} {mismatch}";
    }
}
