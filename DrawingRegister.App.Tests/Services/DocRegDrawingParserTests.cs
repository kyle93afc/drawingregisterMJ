using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class DocRegDrawingParserTests
{
    private static readonly OrganizationProfile Mj = OrganizationRegistry.MJ;
    private static readonly OrganizationProfile Dcf = OrganizationRegistry.DCF;

    [Fact]
    public void ParseFile_standard_mj_filename_is_in_house_with_identity_fields()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\12499-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf",
            Mj);

        Assert.Equal(DocRegSection.InHouse, drawing.Section);
        Assert.Equal("12499-M+J-V1-XX-DR-S-16-01", drawing.DocumentNumber);
        Assert.Equal("FOUNDATION PLAN", drawing.Description);
        Assert.Equal("T01", drawing.Revision);
        Assert.Equal("DR", drawing.DocumentType);
        Assert.Equal("16", drawing.Package);
        Assert.Equal("12499", drawing.ParsedProjectNumber);
        Assert.Equal("12499-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf", drawing.FileName);
        Assert.True(drawing.Include);
    }

    [Fact]
    public void ParseFile_mj_alias_originator_is_in_house_for_mj_organization()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\12499-MJ-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf",
            Mj);

        Assert.Equal(DocRegSection.InHouse, drawing.Section);
        Assert.Equal("12499-MJ-V1-XX-DR-S-16-01", drawing.DocumentNumber);
    }

    [Fact]
    public void ParseFile_third_party_iso_name_is_architect_external()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\24012-ABC-00-XX-DR-A-01-0001-P01_GROUND_FLOOR.pdf",
            Mj);

        Assert.Equal(DocRegSection.ArchitectExternal, drawing.Section);
        Assert.Equal("24012-ABC-00-XX-DR-A-01-0001", drawing.DocumentNumber);
        Assert.Equal("P01", drawing.Revision);
        Assert.Equal("24012", drawing.ParsedProjectNumber);
        Assert.NotEqual(DocRegSection.InHouse, drawing.Section);
    }

    [Fact]
    public void ParseFile_dcf_iso_name_is_in_house_only_for_dcf_organization()
    {
        var asDcf = DocRegDrawingParser.ParseFile(
            @"C:\inbox\12345-DCF-00-XX-DR-C-01-0001-P01_FOUNDATION_PLAN.pdf",
            Dcf);
        var asMj = DocRegDrawingParser.ParseFile(
            @"C:\inbox\12345-DCF-00-XX-DR-C-01-0001-P01_FOUNDATION_PLAN.pdf",
            Mj);

        Assert.Equal(DocRegSection.InHouse, asDcf.Section);
        Assert.Equal(DocRegSection.ArchitectExternal, asMj.Section);
    }

    [Theory]
    [InlineData(@"C:\inbox\A-101 - Ground Floor Plan - Rev B.pdf", "A-101", "Ground Floor Plan", "B")]
    [InlineData(@"C:\inbox\A-101 - Ground Floor Plan - Revision B.pdf", "A-101", "Ground Floor Plan", "B")]
    [InlineData(@"C:\inbox\A-101 - Section A-A - Rev B.pdf", "A-101", "Section A-A", "B")]
    [InlineData(@"C:\inbox\A-101 - Ground Floor Plan.pdf", "A-101", "Ground Floor Plan", "")]
    public void ParseFile_architect_dashed_name_keeps_title_punctuation(
        string filePath,
        string expectedNumber,
        string expectedTitle,
        string expectedRevision)
    {
        var drawing = DocRegDrawingParser.ParseFile(filePath, Mj);

        Assert.Equal(DocRegSection.ArchitectExternal, drawing.Section);
        Assert.Equal(expectedNumber, drawing.DocumentNumber);
        Assert.Equal(expectedTitle, drawing.Description);
        Assert.Equal(expectedRevision, drawing.Revision);
        Assert.Null(drawing.ParsedProjectNumber);
    }

    [Fact]
    public void ParseFile_architect_underscore_name_splits_explicit_revision()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\A-101_Ground_Floor_Plan_Rev_B.pdf",
            Mj);

        Assert.Equal("A-101", drawing.DocumentNumber);
        Assert.Equal("Ground Floor Plan", drawing.Description);
        Assert.Equal("B", drawing.Revision);
        Assert.Equal(DocRegSection.ArchitectExternal, drawing.Section);
    }

    [Fact]
    public void ParseFile_architect_underscore_name_without_rev_keeps_title_punctuation()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\A-101_Section_A-A.pdf",
            Mj);

        Assert.Equal("A-101", drawing.DocumentNumber);
        Assert.Equal("Section A-A", drawing.Description);
        Assert.Equal(string.Empty, drawing.Revision);
        Assert.Equal(DocRegSection.ArchitectExternal, drawing.Section);
    }

    [Fact]
    public void ParseFile_architect_project_style_keeps_explicit_revision_and_title()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\24012 - Site Works - BW_01A - Foundation Plan - Rev C.pdf",
            Mj);

        Assert.Equal("BW_01A", drawing.DocumentNumber);
        Assert.NotEqual("BW_01", drawing.DocumentNumber);
        Assert.Equal("Foundation Plan", drawing.Description);
        Assert.Equal("C", drawing.Revision);
        Assert.Equal("24012", drawing.ParsedProjectNumber);
        Assert.Equal(string.Empty, drawing.ParseNote);
    }

    [Theory]
    [InlineData("24012")]
    [InlineData("P26-999")]
    public void ParseFile_architect_project_style_keeps_ambiguous_reference_suffix(string projectReference)
    {
        var drawing = DocRegDrawingParser.ParseFile(
            $@"C:\inbox\{projectReference} - Site Works - BW_01A - Foundation Plan.pdf",
            Mj);

        Assert.Equal("BW_01A", drawing.DocumentNumber);
        Assert.NotEqual("BW_01", drawing.DocumentNumber);
        Assert.Equal("Foundation Plan", drawing.Description);
        Assert.Equal(string.Empty, drawing.Revision);
        Assert.Equal(projectReference, drawing.ParsedProjectNumber);
        Assert.Contains("full reference", drawing.ParseNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseFile_dashed_name_without_project_number_keeps_first_token_as_reference()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\A-101 - Ground Floor - Extra Notes - Appendix.pdf",
            Mj);

        Assert.Equal("A-101", drawing.DocumentNumber);
        Assert.Equal("Ground Floor - Extra Notes - Appendix", drawing.Description);
        Assert.Equal(string.Empty, drawing.Revision);
        Assert.Null(drawing.ParsedProjectNumber);
        Assert.Equal(string.Empty, drawing.Package);
        Assert.Equal(string.Empty, drawing.DocumentType);
        Assert.Equal(string.Empty, drawing.Size);
        Assert.Null(drawing.IssueDate);
    }

    [Fact]
    public void ParseFile_architect_project_style_keeps_extra_title_parts()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\24012 - Site Works - BW_01A - Foundation Plan - Extra Notes.pdf",
            Mj);

        Assert.Equal("BW_01A", drawing.DocumentNumber);
        Assert.Equal("Foundation Plan - Extra Notes", drawing.Description);
        Assert.Equal("24012", drawing.ParsedProjectNumber);
        Assert.Equal(string.Empty, drawing.Revision);
        Assert.Contains("full reference", drawing.ParseNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseFile_unknown_stem_is_kept_as_reference_without_invented_title()
    {
        var drawing = DocRegDrawingParser.ParseFile(
            @"C:\inbox\meeting-notes-final.pdf",
            Mj);

        Assert.Equal("meeting-notes-final", drawing.DocumentNumber);
        Assert.Equal(string.Empty, drawing.Description);
        Assert.Equal(string.Empty, drawing.Revision);
        Assert.Equal(DocRegSection.ArchitectExternal, drawing.Section);
        Assert.Contains("recognised", drawing.ParseNote, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GENERAL ARRANGEMENT", drawing.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GENERAL ARRANGEMENT", drawing.ParseNote, StringComparison.OrdinalIgnoreCase);
    }
}
