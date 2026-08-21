using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DrawingRegister.App.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;

namespace DrawingRegister.App.Services;

public static partial class CheckPrintScanner
{
    public static CheckPlan Plan(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Checking folder not found: {folderPath}");

        var entries = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(Scan)
            .ToList();

        return new CheckPlan(entries);
    }

    private static CheckPrint Scan(string filePath)
    {
        var entry = new CheckPrint { FilePath = filePath };
        var issues = new List<string>();
        var fileStem = Path.GetFileNameWithoutExtension(filePath);

        if (DrawingFilenameParser.TryParse(fileStem, out var identity)
            && !string.IsNullOrEmpty(identity.Revision)
            && CheckPrintToken().Match(identity.Description) is { Success: true } cpMatch
            && int.TryParse(cpMatch.Groups["cp"].Value, out var cp))
        {
            entry.DocumentCode = identity.DocumentCode;
            entry.Revision = identity.Revision;
            entry.Cp = cp;
        }
        else
        {
            issues.Add("Filename does not match the check-print convention.");
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            entry.SourceHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

            using var document = PdfDocument.Open(filePath);
            var hasStamp = document.GetPages()
                .SelectMany(page => page.GetAnnotations())
                .Any(annotation => annotation.Type == AnnotationType.Stamp);

            if (hasStamp)
                issues.Add("Stamp verdict analysis is not available in this scan.");
            else
                entry.Status = CheckStatus.FC;
        }
        catch (Exception ex)
        {
            issues.Add($"PDF could not be read: {ex.Message}");
        }

        entry.Issue = string.Join(" ", issues);
        return entry;
    }

    [GeneratedRegex(@"^CP[-_\s]?(?<cp>\d+)(?:[-_\s]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex CheckPrintToken();
}

public static class CheckPrintApplier
{
    public static ApplyResult Apply(CheckPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ApplyResult(plan.Entries);
    }
}
