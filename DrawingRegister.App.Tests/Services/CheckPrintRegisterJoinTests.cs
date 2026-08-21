using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class CheckPrintRegisterJoinTests
{
    [Fact]
    public void BuildLiveQueue_joins_current_revision_by_issue_date()
    {
        var document = new DocumentMetadata { DocumentNumber = "DOC-01" };
        document.RevisionHistory[new DateTime(2026, 4, 1)] = new RevisionInfo { Revision = "Z", IsDistributed = true };
        document.RevisionHistory[new DateTime(2026, 4, 2)] = new RevisionInfo
        {
            Revision = "A",
            Purpose = "Construction",
            Method = "Email",
            IssuedBy = "MJ"
        };
        var check = new CheckPrint { DocumentCode = "DOC-01", Revision = "A", Status = CheckStatus.FC };

        var row = Assert.Single(CheckPrintRegisterJoin.BuildLiveQueue([check], [document]));

        Assert.Same(check, row.CheckPrint);
        Assert.True(row.IsCurrent);
        Assert.Equal(new DateTime(2026, 4, 2), row.IssueDate);
        Assert.Equal("Construction", row.RegisterRevision!.Purpose);
        Assert.Equal("Email", row.RegisterRevision.Method);
        Assert.Equal("MJ", row.RegisterRevision.IssuedBy);
        Assert.Equal("Not distributed", row.DistributionText);
        Assert.Equal("FC — no stamp annotation found", row.QueueReason);
    }

    [Fact]
    public void BuildLiveQueue_removes_current_APPD_after_distribution()
    {
        var document = Document("DOC-01", "A", distributed: true);
        var check = new CheckPrint { DocumentCode = "DOC-01", Revision = "A", Status = CheckStatus.APPD };

        var queue = CheckPrintRegisterJoin.BuildLiveQueue([check], [document]);

        Assert.Empty(queue);
    }

    [Fact]
    public void BuildLiveQueue_keeps_approved_but_unissued_revision_with_reason()
    {
        var document = Document("DOC-01", "A", distributed: false);
        var check = new CheckPrint { DocumentCode = "DOC-01", Revision = "A", Status = CheckStatus.APPD };

        var row = Assert.Single(CheckPrintRegisterJoin.BuildLiveQueue([check], [document]));

        Assert.Equal("Approved but not distributed", row.QueueReason);
    }

    [Fact]
    public void BuildLiveQueue_keeps_unmatched_check_with_reason()
    {
        var check = new CheckPrint { DocumentCode = "MISSING", Revision = "A", Status = CheckStatus.APPD };

        var row = Assert.Single(CheckPrintRegisterJoin.BuildLiveQueue([check], []));

        Assert.Null(row.RegisterRevision);
        Assert.False(row.IsCurrent);
        Assert.Equal("No matching register revision", row.QueueReason);
    }

    [Fact]
    public void BuildLiveQueue_keeps_superseded_APPD_with_reason()
    {
        var document = Document("DOC-01", "B", distributed: true);
        document.RevisionHistory[new DateTime(2026, 4, 3)] = new RevisionInfo
        {
            Revision = "A",
            IsDistributed = true,
            IsSuperseded = true
        };
        var check = new CheckPrint { DocumentCode = "DOC-01", Revision = "A", Status = CheckStatus.APPD };

        var row = Assert.Single(CheckPrintRegisterJoin.BuildLiveQueue([check], [document]));

        Assert.False(row.IsCurrent);
        Assert.Equal("Superseded revision", row.QueueReason);
    }

    private static DocumentMetadata Document(string code, string revision, bool distributed)
    {
        var document = new DocumentMetadata { DocumentNumber = code };
        document.RevisionHistory[new DateTime(2026, 4, 2)] = new RevisionInfo
        {
            Revision = revision,
            IsDistributed = distributed
        };
        return document;
    }
}
