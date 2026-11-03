using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Services;

public sealed class SupersededRevisionTests
{
    [Fact]
    public void LatestNonSupersededRevision_returns_null_when_all_superseded()
    {
        var doc = new DocumentMetadata
        {
            Revision = "2A",
            FilePath = @"C:\fake\2A.pdf",
            RevisionHistory =
            {
                [new DateTime(2026, 4, 16)] = new RevisionInfo
                {
                    Revision = "2A",
                    IsSuperseded = true,
                    FilePath = @"C:\fake\2A.pdf"
                }
            }
        };

        Assert.Null(doc.LatestNonSupersededRevision);
    }
}
