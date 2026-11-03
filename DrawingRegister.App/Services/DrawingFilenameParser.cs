using System.Text.RegularExpressions;

namespace DrawingRegister.App.Services;

public static class DrawingFilenameParser
{
    private static readonly Regex Pattern = new(
        @"^(?<projectNo>\d{5,6})-\s*(?<code1>[^-]+)-\s*(?<volume>[^-]+)-\s*(?<code2>[^-]+)-\s*(?<docType>[^-]+)-\s*(?<docDiscipline>[^-]+)-\s*(?<package>\d+)(?:-\s*(?<number>\d+)(?=[_\s-]|$))?(?:-\s*(?<revision>[A-Z]\d{2}(?:\.\d{2})?|\d+[A-Z]|[A-Z]|\d+)(?=[_\s-]|$))?(?:[_\s-]\s*(?<description>.+))?$",
        RegexOptions.Compiled);

    public static bool TryParse(string fileNameWithoutExtension, out DrawingFilenameIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return false;

        var sanitized = Regex.Replace(fileNameWithoutExtension, @"-{2,}", "-");
        sanitized = Regex.Replace(sanitized, @"\s*-\s*", "-");
        var match = Pattern.Match(sanitized);
        if (!match.Success)
            return false;

        var documentCode = $"{match.Groups["projectNo"].Value.Trim()}-{match.Groups["code1"].Value.Trim()}-{match.Groups["volume"].Value.Trim()}-{match.Groups["code2"].Value.Trim()}-{match.Groups["docType"].Value.Trim()}-{match.Groups["docDiscipline"].Value.Trim()}-{match.Groups["package"].Value.Trim()}";
        if (match.Groups["number"].Success)
            documentCode += $"-{match.Groups["number"].Value.Trim()}";

        // Network Rail's 7-segment format ends in a 6-digit document number and has no package.
        var package = match.Groups["package"].Value;
        if (!match.Groups["number"].Success && package.Length == 6)
            package = string.Empty;

        identity = new DrawingFilenameIdentity(
            DocumentCode: documentCode,
            ProjectNumber: match.Groups["projectNo"].Value,
            Revision: match.Groups["revision"].Value,
            DocumentType: match.Groups["docType"].Value,
            Discipline: match.Groups["docDiscipline"].Value,
            Package: package,
            Description: match.Groups["description"].Value,
            Originator: match.Groups["code1"].Value.Trim());
        return true;
    }
}

public readonly record struct DrawingFilenameIdentity(
    string DocumentCode,
    string ProjectNumber,
    string Revision,
    string DocumentType,
    string Discipline,
    string Package,
    string Description,
    string Originator = "");
