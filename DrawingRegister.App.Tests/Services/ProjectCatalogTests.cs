using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

/// <summary>
/// Lock-in tests for the office-wide CMap project catalogue published by the
/// timesheet tooling (project_aliases.json).
/// </summary>
public sealed class ProjectCatalogTests
{
    private static readonly IReadOnlyList<CrmProject> Sample = ProjectCatalog.Parse("""
        {
            "125150-": "Ardgay Train Station Active Travel Upgrades",
            "124832-": "Newton Station - Waiting Shelter",
            "17373a-": "Altens Waste and Recycling Centre - 8322",
            "A5982":   "Aberdeen Harbour Survey",
            "Legacy Project": "Assorted historic drawings"
        }
        """);

    [Fact]
    public void Parse_strips_bom_and_trailing_dash_and_keeps_file_order()
    {
        var projects = ProjectCatalog.Parse("﻿{\"124832-\": \"Newton\", \"125150-\": \"Ardgay\"}");

        Assert.Equal(new[] { "124832", "125150" }, projects.Select(p => p.Code));
        Assert.Equal(new[] { "Newton", "Ardgay" }, projects.Select(p => p.Title));
    }

    [Fact]
    public void Parse_skips_blank_titles_and_blank_codes()
    {
        var projects = ProjectCatalog.Parse("""
            {"124832-": "Newton", "125150-": "   ", "-": "Orphan", "17373a-": "Altens"}
            """);

        Assert.Equal(new[] { "124832", "17373a" }, projects.Select(p => p.Code));
    }

    [Fact]
    public void Parse_keeps_odd_codes()
    {
        Assert.Contains(Sample, p => p.Code == "A5982");
        Assert.Contains(Sample, p => p.Code == "Legacy Project");
    }

    [Fact]
    public void Load_on_a_missing_path_returns_empty()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"project_aliases_{Guid.NewGuid():N}.json");

        Assert.Empty(ProjectCatalog.Load(missing));
    }

    [Fact]
    public void Load_on_invalid_json_returns_empty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project_aliases_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json at all ");

        try
        {
            Assert.Empty(ProjectCatalog.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Search_ranks_code_hits_above_title_hits()
    {
        var results = ProjectCatalog.Search(Sample, "station");

        // "Station" only appears in titles here, so ranking falls to the numeric code.
        Assert.Equal(new[] { "125150", "124832" }, results.Select(p => p.Code));
    }

    [Fact]
    public void Search_puts_a_code_match_first()
    {
        var projects = ProjectCatalog.Parse("""
            {"124832-": "Newton Station", "125150-": "Ardgay 1248 Upgrades", "001248-": "Tiny Job"}
            """);

        var results = ProjectCatalog.Search(projects, "1248");

        Assert.Equal(new[] { "124832", "001248", "125150" }, results.Select(p => p.Code));
    }

    [Fact]
    public void Search_orders_a_lettered_code_by_its_leading_digits()
    {
        var results = ProjectCatalog.Search(Sample, "a");

        // Code hits first ("17373a", "A5982", "Legacy Project"), ordered by leading
        // digits descending, then the title-only hits.
        Assert.Equal(
            new[] { "17373a", "A5982", "Legacy Project", "125150", "124832" },
            results.Select(p => p.Code));
    }

    [Fact]
    public void Search_requires_every_token_to_match_somewhere()
    {
        Assert.Equal(new[] { "124832" }, ProjectCatalog.Search(Sample, "1248 shelter").Select(p => p.Code));
        Assert.Empty(ProjectCatalog.Search(Sample, "1248 ardgay"));
    }

    [Fact]
    public void Search_is_case_insensitive()
    {
        Assert.Equal(new[] { "17373a" }, ProjectCatalog.Search(Sample, "ALTENS").Select(p => p.Code));
        Assert.Equal(new[] { "17373a" }, ProjectCatalog.Search(Sample, "17373A").Select(p => p.Code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_with_a_blank_query_returns_empty(string query)
    {
        Assert.Empty(ProjectCatalog.Search(Sample, query));
    }

    [Fact]
    public void Search_honours_the_limit()
    {
        var results = ProjectCatalog.Search(Sample, "a", limit: 2);

        Assert.Equal(new[] { "17373a", "A5982" }, results.Select(p => p.Code));
    }
}
