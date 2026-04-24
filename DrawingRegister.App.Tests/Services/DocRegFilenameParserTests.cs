using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

/// <summary>
/// Lock-in tests for the SER DocReg filename grammar
/// (DocReg-&lt;projectNo&gt;-&lt;yyyyMMdd&gt;.pdf).
/// </summary>
public sealed class DocRegFilenameParserTests
{
    [Theory]
    [InlineData("DocReg-124379-20260422", "124379", "20260422")]
    [InlineData("DOCREG-124379-20260422", "124379", "20260422")]
    [InlineData("docreg-124379-20260422", "124379", "20260422")]
    [InlineData("DocReg-99999-20260422", "99999", "20260422")]     // 5-digit project
    [InlineData("DocReg-124379-20261231", "124379", "20261231")]
    public void TryParse_valid_filename_returns_true(
        string fileStem,
        string expectedProject,
        string expectedDate)
    {
        var ok = DocRegFilenameParser.TryParse(fileStem, out var match);

        Assert.True(ok);
        Assert.Equal(expectedProject, match.ProjectNumber);
        Assert.Equal(expectedDate, match.FileDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DocReg-124379-2026042")]          // 7-digit date, too short
    [InlineData("DocReg-124379-202604221")]        // 9-digit date, too long
    [InlineData("DocReg-12-20260422")]             // project too short
    [InlineData("DocReg-1234567-20260422")]        // project too long
    [InlineData("DocReg-124379")]                  // missing date
    [InlineData("124379-DocReg-20260422")]         // wrong order
    [InlineData("DocRegistry-124379-20260422")]    // wrong literal
    [InlineData("DocReg_124379_20260422")]         // underscores instead of hyphens
    [InlineData("DocReg-124379-20260422-extra")]   // trailing content
    [InlineData("124379-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN & DETAILS")] // standard drawing
    public void TryParse_invalid_filename_returns_false(string fileStem)
    {
        var ok = DocRegFilenameParser.TryParse(fileStem, out var match);

        Assert.False(ok);
        Assert.Equal(default, match);
    }
}
