using System.Text.RegularExpressions;

namespace DrawingRegister.App.Services;

/// <summary>
/// Parses SER-mandated Document Register filenames of the form
/// <c>DocReg-&lt;projectNo&gt;-&lt;yyyyMMdd&gt;</c> (extension already stripped).
/// The grammar is strict because the Scottish SER process requires the exact
/// format on the file that appears in the warrant bundle.
/// </summary>
public static class DocRegFilenameParser
{
    private static readonly Regex Pattern = new(
        @"^DocReg-(?<projectNo>\d{5,6})-(?<fileDate>\d{8})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParse(string fileNameWithoutExtension, out DocRegMatch result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return false;

        var m = Pattern.Match(fileNameWithoutExtension);
        if (!m.Success)
            return false;

        result = new DocRegMatch(
            ProjectNumber: m.Groups["projectNo"].Value,
            FileDate: m.Groups["fileDate"].Value);
        return true;
    }
}

public readonly record struct DocRegMatch(string ProjectNumber, string FileDate);
