using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Services;

public sealed class SupersededRevisionTests
{
    private static DocumentMetadata DocWithHistory(params (DateTime date, string rev, bool superseded)[] entries)
    {
        var doc = new DocumentMetadata
        {
            DocumentNumber = "124660-M+J-V1-XX-DR-A-10-01",
            Description = "TROUGH LAYOUT",
            Package = "10",
            ProjectNumber = "124660",
            ProjectName = "SSEN",
            Discipline = "General/Multi-discipline"
        };
        foreach (var (date, rev, superseded) in entries)
        {
            doc.RevisionHistory[date] = new RevisionInfo
            {
                Revision = rev,
                IsSuperseded = superseded,
                FilePath = $@"C:\fake\{rev}.pdf"
            };
        }
        // Match the ProjectManager invariant: the document-level fields reflect the latest entry
        // (including superseded), because that's what import currently sets.
        var rawLatest = doc.RevisionHistory.OrderByDescending(kv => kv.Key).First();
        doc.Revision = rawLatest.Value.Revision;
        doc.FilePath = rawLatest.Value.FilePath;
        return doc;
    }

    [Fact]
    public void LatestNonSupersededRevision_skips_superseded_entries()
    {
        var doc = DocWithHistory(
            (new DateTime(2026, 4, 16), "2A", superseded: true),
            (new DateTime(2026, 4, 17), "1",  superseded: false));

        var latest = doc.LatestNonSupersededRevision;

        Assert.NotNull(latest);
        Assert.Equal("1", latest!.Value.Value.Revision);
    }

    [Fact]
    public void LatestNonSupersededRevision_returns_null_when_all_superseded()
    {
        var doc = DocWithHistory(
            (new DateTime(2026, 4, 16), "2A", superseded: true));

        Assert.Null(doc.LatestNonSupersededRevision);
    }

    [Fact]
    public void LatestNonSupersededRevision_when_superseded_is_newest_falls_back_to_prior()
    {
        // The 16/04 2A was issued and later marked superseded; 15/04 1A is the real current state.
        var doc = DocWithHistory(
            (new DateTime(2026, 4, 15), "1A", superseded: false),
            (new DateTime(2026, 4, 16), "2A", superseded: true));

        Assert.Equal("1A", doc.LatestNonSupersededRevision!.Value.Value.Revision);
    }

    [Fact]
    public void IsLatestRevision_ignores_superseded_entries()
    {
        var doc = DocWithHistory(
            (new DateTime(2026, 4, 16), "2A", superseded: true),
            (new DateTime(2026, 4, 17), "1",  superseded: false));

        // Set the current document-level revision to the non-superseded one, matching what the
        // edit flow does after marking an entry superseded.
        doc.Revision = "1";

        Assert.True(doc.IsLatestRevision);
    }
}
