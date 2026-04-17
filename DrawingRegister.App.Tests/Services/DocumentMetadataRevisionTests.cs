using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Services;

public sealed class DocumentMetadataRevisionTests
{
    private static Dictionary<DateTime, RevisionInfo> History(params string[] revisions)
    {
        var dict = new Dictionary<DateTime, RevisionInfo>();
        for (int i = 0; i < revisions.Length; i++)
        {
            dict[new DateTime(2026, 4, 1).AddDays(i)] = new RevisionInfo { Revision = revisions[i] };
        }
        return dict;
    }

    // ---- SubLetterNumeric ----

    [Fact]
    public void SubLetter_empty_history_internal_returns_1A()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History(), RevisionScheme.SubLetterNumeric);
        Assert.Equal("1A", result);
    }

    [Fact]
    public void SubLetter_after_1A_internal_returns_1B()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History("1A"), RevisionScheme.SubLetterNumeric);
        Assert.Equal("1B", result);
    }

    [Fact]
    public void SubLetter_after_1A_1B_formal_returns_1()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History("1A", "1B"), RevisionScheme.SubLetterNumeric, isFormalIssue: true);
        Assert.Equal("1", result);
    }

    [Fact]
    public void SubLetter_after_formal_1_internal_returns_2A()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History("1A", "1B", "1"), RevisionScheme.SubLetterNumeric);
        Assert.Equal("2A", result);
    }

    [Fact]
    public void SubLetter_after_2A_formal_returns_2()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History("1A", "1B", "1", "2A"), RevisionScheme.SubLetterNumeric, isFormalIssue: true);
        Assert.Equal("2", result);
    }

    [Fact]
    public void SubLetter_legacy_numeric_history_internal_picks_next_cycle_with_A()
    {
        // 124660 cutover: existing plain 1, 2, 3 are treated as formal issues; next internal is 4A.
        var result = DocumentMetadata.GenerateRevisionCode("", History("1", "2", "3"), RevisionScheme.SubLetterNumeric);
        Assert.Equal("4A", result);
    }

    [Fact]
    public void SubLetter_legacy_numeric_history_formal_picks_next_whole_number()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History("1", "2", "3"), RevisionScheme.SubLetterNumeric, isFormalIssue: true);
        Assert.Equal("4", result);
    }

    [Fact]
    public void SubLetter_ignores_legacy_prefix_revisions()
    {
        // C01 / P01 entries from an earlier life of the project should not influence the sub-letter cycle.
        var result = DocumentMetadata.GenerateRevisionCode("", History("C01", "P01"), RevisionScheme.SubLetterNumeric);
        Assert.Equal("1A", result);
    }

    // ---- Numeric (unchanged behaviour) ----

    [Fact]
    public void Numeric_empty_history_returns_1()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History(), RevisionScheme.Numeric);
        Assert.Equal("1", result);
    }

    [Fact]
    public void Numeric_after_1_2_returns_3()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History("1", "2"), RevisionScheme.Numeric);
        Assert.Equal("3", result);
    }

    // ---- Legacy (unchanged behaviour) ----

    [Fact]
    public void Legacy_construction_empty_returns_C01()
    {
        var result = DocumentMetadata.GenerateRevisionCode("Construction", History(), RevisionScheme.Legacy);
        Assert.Equal("C01", result);
    }

    [Fact]
    public void Legacy_construction_after_C01_returns_C02()
    {
        var history = new Dictionary<DateTime, RevisionInfo>
        {
            [new DateTime(2026, 1, 1)] = new RevisionInfo { Revision = "C01", Purpose = "Construction" }
        };
        var result = DocumentMetadata.GenerateRevisionCode("Construction", history, RevisionScheme.Legacy);
        Assert.Equal("C02", result);
    }

    [Fact]
    public void Legacy_no_purpose_empty_returns_A()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History(), RevisionScheme.Legacy);
        Assert.Equal("A", result);
    }

    // ---- Backwards-compatible bool overload ----

    [Fact]
    public void Bool_overload_true_is_numeric_scheme()
    {
        var result = DocumentMetadata.GenerateRevisionCode("", History("1", "2"), useNumericRevisions: true);
        Assert.Equal("3", result);
    }

    [Fact]
    public void Bool_overload_false_is_legacy_scheme()
    {
        var result = DocumentMetadata.GenerateRevisionCode("Construction", History(), useNumericRevisions: false);
        Assert.Equal("C01", result);
    }
}
