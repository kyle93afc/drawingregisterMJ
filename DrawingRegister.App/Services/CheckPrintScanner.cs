using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DrawingRegister.App.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Tokens;
using UglyToad.PdfPig.Util;

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
            var stamps = document.GetPages()
                .SelectMany(page => page.GetAnnotations())
                .Where(annotation => annotation.Type == AnnotationType.Stamp)
                .ToList();

            var verdicts = stamps
                .Select(GetVerdict)
                .OfType<CheckStatus>()
                .Distinct()
                .ToList();

            entry.Status = stamps.Count == 0
                ? CheckStatus.FC
                : verdicts.Count switch
                {
                    0 => CheckStatus.UNKNOWN,
                    1 => verdicts[0],
                    _ => CheckStatus.CONFLICT
                };
            entry.BackDrafted = stamps.Any(stamp =>
                GetSubject(stamp).Equals("BACK DRAFTED", StringComparison.OrdinalIgnoreCase));

            if (stamps.Count > 0)
            {
                var attribution = stamps
                    .Select(stamp => new { Stamp = stamp, Date = GetStampDate(stamp) })
                    .OrderByDescending(item => item.Date)
                    .First();
                entry.StampAuthor = NormalizeAuthor(GetString(attribution.Stamp, "T"));
                entry.StampDate = attribution.Date;
            }

            if (entry.Status == CheckStatus.UNKNOWN)
                issues.Add("Stamp annotations found, but no recognised verdict.");
            else if (entry.Status == CheckStatus.CONFLICT)
                issues.Add("Conflicting verdict stamps require review.");
        }
        catch (Exception ex)
        {
            issues.Add($"PDF could not be read: {ex.Message}");
        }

        entry.Issue = string.Join(" ", issues);
        return entry;
    }

    private static CheckStatus? GetVerdict(Annotation annotation)
    {
        var subject = GetSubject(annotation);
        return subject switch
        {
            var value when value.Equals("Approved with Comments", StringComparison.OrdinalIgnoreCase) => CheckStatus.AWC,
            var value when value.Equals("Approved", StringComparison.OrdinalIgnoreCase) => CheckStatus.APPD,
            _ => null
        };
    }

    private static string GetSubject(Annotation annotation) => GetString(annotation, "Subj");

    private static string GetString(Annotation annotation, string key) =>
        annotation.AnnotationDictionary.TryGet(NameToken.Create(key), out StringToken value)
            ? value.Data.Trim()
            : string.Empty;

    private static DateTime? GetStampDate(Annotation annotation) =>
        annotation.ModifiedDate is { } modifiedDate
        && DateFormatHelper.TryParseDateTimeOffset(modifiedDate, out var date)
            ? date.UtcDateTime
            : null;

    private static string NormalizeAuthor(string author) => author switch
    {
        var value when value.Equals("W.Bonfim", StringComparison.OrdinalIgnoreCase) => "W. Bonfim",
        var value when value.Equals("h.mcarthur", StringComparison.OrdinalIgnoreCase) => "H. McArthur",
        _ => author
    };

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
