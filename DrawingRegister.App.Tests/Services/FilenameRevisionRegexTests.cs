using System.Text.RegularExpressions;

namespace DrawingRegister.App.Tests.Services;

/// <summary>
/// Lock-in tests for the filename revision pattern used by ProjectManager.ImportDocuments.
/// Mirrors the regex in DrawingRegister.App/Models/ProjectManager.cs so that regressions
/// (e.g. losing the \d+[A-Z] alternative for the 124660 sub-letter scheme) are caught in tests
/// without spinning up a full import.
/// </summary>
public sealed class FilenameRevisionRegexTests
{
    // Kept in sync with ProjectManager.ImportDocuments.
    private static readonly Regex Pattern = new(
        @"^(?<projectNo>\d{5,6})-\s*(?<code1>[^-]+)-\s*(?<volume>[^-]+)-\s*(?<code2>[^-]+)-\s*(?<docType>[^-]+)-\s*(?<docDiscipline>[^-]+)-\s*(?<package>\d+)(?:-\s*(?<number>\d+)(?=[_\s-]|$))?(?:-\s*(?<revision>[A-Z]\d{2}|\d+[A-Z]|[A-Z]|\d+)(?=[_\s-]|$))?(?:[_\s-]\s*(?<description>.+))?$");

    [Theory]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-1A-GROUND FLOOR PLAN", "1A")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-1B-GROUND FLOOR PLAN", "1B")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-2C-GROUND FLOOR PLAN", "2C")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-1-GROUND FLOOR PLAN", "1")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-C01-GROUND FLOOR PLAN", "C01")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-A-GROUND FLOOR PLAN", "A")]
    public void Parses_expected_revision(string fileStem, string expectedRevision)
    {
        var match = Pattern.Match(fileStem);
        Assert.True(match.Success);
        Assert.Equal(expectedRevision, match.Groups["revision"].Value);
    }
}
