using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DrawingRegister.App.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Filters;
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

            var subjects = stamps.Select(stamp => ResolveSubject(document, stamp)).ToList();
            var verdicts = subjects.Select(GetVerdict).OfType<CheckStatus>().Distinct().ToList();
            // A CHECKED stamp (architect's or the M+J custom one) only says someone checked it. Alone it counts as AWC,
            // never APPD, so it cannot produce a false approval; beside a real verdict stamp, the verdict wins.
            if (verdicts.Count == 0 && subjects.Any(subject => subject.Equals("CHECKED", StringComparison.OrdinalIgnoreCase)))
                verdicts.Add(CheckStatus.AWC);

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

    private static readonly string[] KnownSubjects =
    [
        "Approved with Comments", "Revise and Resubmit", "Not Approved", "For Comment",
        "Approved", "Comments", "Revise", "Rejected", "CHECKED", "BACK DRAFTED"
    ];

    // Custom stamps (e.g. the M+J CHECKED stamp) carry the generic subject "Stamp"; their verdict is the text drawn in the appearance.
    private static string ResolveSubject(PdfDocument document, Annotation annotation)
    {
        var subject = GetSubject(annotation);
        if (!subject.Equals("Stamp", StringComparison.OrdinalIgnoreCase) && subject.Length > 0)
            return subject;

        var text = string.Join(" ", AppearanceText(document, annotation));
        return KnownSubjects.FirstOrDefault(known => text.Contains(known, StringComparison.OrdinalIgnoreCase)) ?? subject;
    }

    private static IEnumerable<string> AppearanceText(PdfDocument document, Annotation annotation)
    {
        if (!annotation.AnnotationDictionary.TryGet(NameToken.Create("AP"), out IToken apToken)
            || Resolve(document, apToken) is not DictionaryToken ap
            || !ap.TryGet(NameToken.Create("N"), out IToken normal))
            return [];
        return FormText(document, normal, depth: 0);
    }

    // ponytail: literal-string Tj/TJ operators only; hex strings and deeper nesting than 4 are ignored.
    private static IEnumerable<string> FormText(PdfDocument document, IToken token, int depth)
    {
        if (depth > 4 || Resolve(document, token) is not StreamToken stream)
            yield break;

        var data = stream.Data;
        var filters = DefaultFilterProvider.Instance.GetFilters(stream.StreamDictionary);
        for (var i = 0; i < filters.Count; i++)
            data = filters[i].Decode(data, stream.StreamDictionary, DefaultFilterProvider.Instance, i);

        foreach (Match match in ShownText().Matches(System.Text.Encoding.Latin1.GetString(data.Span)))
            yield return match.Groups["t"].Value;

        if (stream.StreamDictionary.TryGet(NameToken.Create("Resources"), out IToken resources)
            && Resolve(document, resources) is DictionaryToken resourceDict
            && resourceDict.TryGet(NameToken.Create("XObject"), out IToken xObjects)
            && Resolve(document, xObjects) is DictionaryToken xObjectDict)
        {
            foreach (var child in xObjectDict.Data.Values)
                foreach (var text in FormText(document, child, depth + 1))
                    yield return text;
        }
    }

    private static IToken? Resolve(PdfDocument document, IToken token) =>
        token is IndirectReferenceToken reference ? document.Structure.GetObject(reference.Data)?.Data : token;

    private static CheckStatus? GetVerdict(string subject)
    {
        return subject switch
        {
            var value when value.Equals("Approved with Comments", StringComparison.OrdinalIgnoreCase) => CheckStatus.AWC,
            var value when value.Equals("Approved", StringComparison.OrdinalIgnoreCase) => CheckStatus.APPD,
            // Checker has marked it up but not approved it; a technician must action the comments and re-issue a CP.
            var value when value.Equals("Comments", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("For Comment", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("Not Approved", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("Revise", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("Revise and Resubmit", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("Rejected", StringComparison.OrdinalIgnoreCase) => CheckStatus.COMMENTS,
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

    [GeneratedRegex(@"\((?<t>[^()\\]*)\)\s*Tj|\[(?<t>[^\]]*)\]\s*TJ")]
    private static partial Regex ShownText();

    // CP token at the start of the description ("CP01-PLAN") or after it ("GENERAL NOTES-CP01").
    [GeneratedRegex(@"^CP[-_\s]?(?<cp>\d+)(?:[-_\s]|$)|[-_\s]CP[-_\s]?(?<cp>\d+)$", RegexOptions.IgnoreCase)]
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
