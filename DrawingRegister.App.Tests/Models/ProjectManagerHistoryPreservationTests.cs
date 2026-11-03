using System;
using System.IO;
using System.Linq;
using DrawingRegister.App.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace DrawingRegister.App.Tests.Models;

// File-boundary regressions: copying a register must not duplicate stored issues or
// lose edited issue details, and rescanning one issue must not erase other issues.
// Same-day issue folders are distinct; unrelated external paths must stay external.
public sealed class ProjectManagerHistoryPreservationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"drawing-register-history-{Guid.NewGuid():N}");
    private const string Drawing = "124379-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf";

    [Fact]
    public void Copied_register_preserves_issue_history_and_rebinds_copied_files()
    {
        var original = Path.Combine(_root, "original");
        var copy = Path.Combine(_root, "copy");
        CreateProject(original);
        var first = CreateDrawing(original, "20260424-FIRST ISSUE", Drawing);
        CreateDrawing(original, "20260424-SECOND ISSUE", Drawing);
        var manager = new ProjectManager();
        manager.ImportDocuments(original);
        var document = Assert.Single(manager.Documents);
        Assert.Equal(2, document.RevisionHistory.Count);
        var edited = document.RevisionHistory.Single(r => r.Value.FilePath == first);
        edited.Value.Purpose = "Edited purpose";
        edited.Value.IssuedBy = "QA";
        edited.Value.IsSuperseded = true;
        document.SetCompanyDistributions(new() { "recipient-1" }, edited.Key);
        manager.SaveProjectData();
        CopyDirectory(original, copy);

        var reopened = new ProjectManager();
        reopened.ImportDocuments(copy);
        var loaded = Assert.Single(reopened.Documents);
        Assert.Equal(2, loaded.RevisionHistory.Count);
        var preserved = loaded.RevisionHistory[edited.Key];
        Assert.Equal("Edited purpose", preserved.Purpose);
        Assert.Equal("QA", preserved.IssuedBy);
        Assert.True(preserved.IsSuperseded);
        Assert.Equal(Path.Combine(copy, "20260424-FIRST ISSUE", Drawing), preserved.FilePath);
        Assert.Equal(new[] { "recipient-1" }, loaded.DistributionCompanyIds[edited.Key.Date]);
        Assert.All(loaded.RevisionHistory.Values, r => Assert.StartsWith(copy + Path.DirectorySeparatorChar, r.FilePath, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, loaded.RevisionHistory.Keys.Distinct().Count());
    }

    [Fact]
    public void Rescanning_one_issue_keeps_other_issues_and_unsaved_edits()
    {
        var project = Path.Combine(_root, "project");
        CreateProject(project);
        var first = CreateDrawing(project, "20260424-FIRST ISSUE", Drawing);
        CreateDrawing(project, "20260424-SECOND ISSUE", Drawing);
        CreateDrawing(project, "20260501-THIRD ISSUE", Drawing);
        var manager = new ProjectManager();
        manager.ImportDocuments(project);
        var document = Assert.Single(manager.Documents);
        Assert.Equal(3, document.RevisionHistory.Count);
        var firstRevision = document.RevisionHistory.Single(r => r.Value.FilePath == first);
        firstRevision.Value.Purpose = "Unsaved correction";
        firstRevision.Value.IsSuperseded = true;
        document.SetCompanyDistributions(new() { "recipient-1" }, firstRevision.Key);

        CreateDrawing(project, "20260424-FIRST ISSUE", "124379-M+J-V1-XX-DR-S-16-02-T01-SECTION.pdf");
        manager.ImportDocuments(project, Path.GetDirectoryName(first));

        var reopened = new ProjectManager();
        reopened.ImportDocuments(project);
        var loaded = Assert.Single(reopened.Documents, d => d.DocumentNumber.EndsWith("16-01", StringComparison.Ordinal));
        Assert.Equal(3, loaded.RevisionHistory.Count);
        Assert.Equal("Unsaved correction", loaded.RevisionHistory[firstRevision.Key].Purpose);
        Assert.True(loaded.RevisionHistory[firstRevision.Key].IsSuperseded);
        Assert.Equal(new[] { "recipient-1" }, loaded.DistributionCompanyIds[firstRevision.Key.Date]);
        Assert.Contains(reopened.Documents, d => d.DocumentNumber.EndsWith("16-02", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("deleted", 1)]
    [InlineData("renamed", 1)]
    [InlineData("revised", 2)]
    public void Rescan_preserves_legacy_metadata_when_a_file_changes(string change, int expectedRevisions)
    {
        var project = Path.Combine(_root, "project");
        CreateProject(project);
        var file = CreateDrawing(project, "20260424-ISSUE", Drawing);
        var manager = new ProjectManager();
        manager.ImportDocuments(project);
        var storagePath = Path.Combine(project, "project_data.json");
        var storage = ProjectStorage.Load(storagePath);
        var saved = Assert.Single(storage.Documents);
        var revision = Assert.Single(saved.RevisionHistory).Value;
        revision.Purpose = "Retain me";
        revision.Method = "Hand";
        revision.IsSuperseded = true;
        var legacyKey = new DateTime(2026, 4, 24).AddTicks(1234567);
        saved.RevisionHistory.Clear();
        saved.RevisionHistory.Add(legacyKey, revision);
        storage.Save(storagePath);
        File.Delete(file);
        if (change != "deleted")
            CreateDrawing(project, "20260424-ISSUE", change == "renamed"
                ? Drawing.Replace("FOUNDATION PLAN", "RENAMED PLAN")
                : Drawing.Replace("T01", "T02"));

        // A new manager exercises reload as well as rescanning old persisted keys.
        var reopened = new ProjectManager();
        reopened.ImportDocuments(project, Path.GetDirectoryName(file));
        reopened.ImportDocuments(project, Path.GetDirectoryName(file));
        var loaded = Assert.Single(reopened.Documents);
        Assert.Equal(expectedRevisions, loaded.RevisionHistory.Count);
        Assert.Equal("Retain me", loaded.RevisionHistory[legacyKey].Purpose);
        Assert.Equal("Hand", loaded.RevisionHistory[legacyKey].Method);
        Assert.True(loaded.RevisionHistory[legacyKey].IsSuperseded);
        if (change == "renamed")
            Assert.EndsWith("RENAMED PLAN.pdf", loaded.RevisionHistory[legacyKey].FilePath);
    }

    [Fact]
    public void Copy_rebases_missing_internal_paths_but_keeps_external_references()
    {
        var original = Path.Combine(_root, "original");
        var copy = Path.Combine(_root, "copy");
        CreateProject(original);
        var file = CreateDrawing(original, "20260424-ISSUE", Drawing);
        var manager = new ProjectManager();
        manager.ImportDocuments(original);
        var external = Path.Combine(_root, "original-other", "external.pdf");
        manager.CheckPrints.Add(new CheckPrint { FilePath = external });
        manager.CheckPrints.Add(new CheckPrint { FilePath = Path.Combine(original, "checks", "missing.pdf") });
        manager.SaveProjectData();
        File.Delete(file);
        CopyDirectory(original, copy);

        var reopened = new ProjectManager();
        reopened.ImportDocuments(copy);
        Assert.Equal(Path.Combine(copy, "20260424-ISSUE", Drawing), Assert.Single(Assert.Single(reopened.Documents).RevisionHistory).Value.FilePath);
        Assert.Contains(reopened.CheckPrints, c => c.FilePath == external);
        Assert.Contains(reopened.CheckPrints, c => c.FilePath == Path.Combine(copy, "checks", "missing.pdf"));
    }

    [Fact]
    public void Refresh_preserves_unsaved_issue_details()
    {
        var project = Path.Combine(_root, "project");
        CreateProject(project);
        CreateDrawing(project, "20260424-ISSUE", Drawing);
        var manager = new ProjectManager();
        manager.ImportDocuments(project);
        Assert.Single(Assert.Single(manager.Documents).RevisionHistory).Value.IssuedBy = "QA";
        manager.ImportDocuments(project);
        manager.SaveProjectData();
        var reopened = new ProjectManager();
        reopened.ImportDocuments(project);
        Assert.Equal("QA", Assert.Single(Assert.Single(reopened.Documents).RevisionHistory).Value.IssuedBy);
    }

    private static void CreateProject(string path)
    {
        Directory.CreateDirectory(path);
        new ProjectInfo { ProjectNumber = "124379", OrganizationId = "MJ" }.Save(path);
    }

    private static string CreateDrawing(string project, string issue, string name)
    {
        var folder = Path.Combine(project, issue);
        Directory.CreateDirectory(folder);
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4, true);
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var child in Directory.GetDirectories(source))
            CopyDirectory(child, Path.Combine(destination, Path.GetFileName(child)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
