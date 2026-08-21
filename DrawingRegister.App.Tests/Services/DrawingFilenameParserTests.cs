using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class DrawingFilenameParserTests
{
    [Fact]
    public void TryParse_returns_drawing_identity_for_subletter_revision()
    {
        var parsed = DrawingFilenameParser.TryParse(
            "124660-M+J-V1-XX-DR-A-01-02-1A-GROUND FLOOR PLAN",
            out var identity);

        Assert.True(parsed);
        Assert.Equal("124660-M+J-V1-XX-DR-A-01-02", identity.DocumentCode);
        Assert.Equal("124660", identity.ProjectNumber);
        Assert.Equal("1A", identity.Revision);
        Assert.Equal("DR", identity.DocumentType);
        Assert.Equal("A", identity.Discipline);
        Assert.Equal("01", identity.Package);
        Assert.Equal("GROUND FLOOR PLAN", identity.Description);
    }

    [Theory]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-1B-GROUND FLOOR PLAN", "1B")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-2C-GROUND FLOOR PLAN", "2C")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-1-GROUND FLOOR PLAN", "1")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-C01-GROUND FLOOR PLAN", "C01")]
    [InlineData("124660-M+J-V1-XX-DR-A-01-02-A-GROUND FLOOR PLAN", "A")]
    public void TryParse_supports_existing_revision_grammars(string fileStem, string expectedRevision)
    {
        var parsed = DrawingFilenameParser.TryParse(fileStem, out var identity);

        Assert.True(parsed);
        Assert.Equal(expectedRevision, identity.Revision);
    }

    [Theory]
    [InlineData("124660 - M+J - V1 - XX - DR - A - 01 - 02 - 1A - PLAN")]
    [InlineData("124660--M+J-V1-XX-DR-A-01-02-1A-PLAN")]
    public void TryParse_preserves_existing_filename_sanitization(string fileStem)
    {
        var parsed = DrawingFilenameParser.TryParse(fileStem, out var identity);

        Assert.True(parsed);
        Assert.Equal("124660-M+J-V1-XX-DR-A-01-02", identity.DocumentCode);
        Assert.Equal("1A", identity.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-drawing")]
    [InlineData("124660-M+J-V1-XX-DR-A-NOTNUMERIC-02-1A-PLAN")]
    public void TryParse_rejects_unrecognised_filename(string fileStem)
    {
        var parsed = DrawingFilenameParser.TryParse(fileStem, out var identity);

        Assert.False(parsed);
        Assert.Equal(default, identity);
    }
}
