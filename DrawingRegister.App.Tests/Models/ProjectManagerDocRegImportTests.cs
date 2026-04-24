using System;
using System.IO;
using System.Linq;
using DrawingRegister.App.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace DrawingRegister.App.Tests.Models;

/// <summary>
/// End-to-end integration tests for the DocReg branch of
/// <see cref="ProjectManager.ImportDocuments"/>.
///
/// Exercises the real import pipeline against a temp folder that mimics a
/// warrant-issue batch: one standard drawing and one DocReg file. Confirms
/// the DocReg branch is wired up, project-mismatch enforcement works, and
/// normal drawings still import alongside DocRegs.
/// </summary>
public sealed class ProjectManagerDocRegImportTests : IDisposable
{
    private readonly string _basePath;

    public ProjectManagerDocRegImportTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dr-docreg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
        {
            try { Directory.Delete(_basePath, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void ImportDocuments_registers_DocReg_file_as_DOCREG_type()
    {
        PrimeProjectInfo("124379");
        var dateFolder = Path.Combine(_basePath, "20260424-WARRANT ISSUE - STRUCTURAL");
        Directory.CreateDirectory(dateFolder);

        WriteMinimalA4Pdf(Path.Combine(dateFolder, "124379-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf"));
        WriteMinimalA4Pdf(Path.Combine(dateFolder, "DocReg-124379-20260422.pdf"));

        var pm = new ProjectManager();
        var result = pm.ImportDocuments(_basePath);

        Assert.Equal(2, result.SuccessfullyParsed);
        Assert.Empty(result.SkippedFiles);

        var docReg = pm.Documents.SingleOrDefault(d => d.DocumentType == "DOCREG");
        Assert.NotNull(docReg);
        Assert.Equal("DocReg-124379-20260422", docReg!.DocumentNumber);
        Assert.Equal("Document Register", docReg.Description);
        Assert.Equal("-", docReg.RevisionHistory.Values.Single().Revision);
        Assert.Equal(string.Empty, docReg.Discipline);
        Assert.Equal(string.Empty, docReg.Package);
        Assert.Equal(
            new DateTime(2026, 4, 24),
            docReg.RevisionHistory.Keys.Single().Date);
        Assert.EndsWith("DocReg-124379-20260422.pdf", docReg.FilePath);
    }

    [Fact]
    public void ImportDocuments_skips_DocReg_with_project_mismatch()
    {
        PrimeProjectInfo("124379");
        var dateFolder = Path.Combine(_basePath, "20260424-WARRANT ISSUE - STRUCTURAL");
        Directory.CreateDirectory(dateFolder);

        WriteMinimalA4Pdf(Path.Combine(dateFolder, "124379-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf"));
        WriteMinimalA4Pdf(Path.Combine(dateFolder, "DocReg-999999-20260422.pdf"));

        var pm = new ProjectManager();
        var result = pm.ImportDocuments(_basePath);

        Assert.Equal(1, result.SuccessfullyParsed);
        var skipped = Assert.Single(result.SkippedFiles);
        Assert.Contains("Project number mismatch", skipped.Reason);
        Assert.Contains("999999", skipped.Reason);
        Assert.DoesNotContain(pm.Documents, d => d.DocumentType == "DOCREG");
    }

    [Fact]
    public void ImportDocuments_accepts_case_variant_DOCREG_filename()
    {
        PrimeProjectInfo("124379");
        var dateFolder = Path.Combine(_basePath, "20260424-WARRANT ISSUE - STRUCTURAL");
        Directory.CreateDirectory(dateFolder);

        WriteMinimalA4Pdf(Path.Combine(dateFolder, "DOCREG-124379-20260422.pdf"));

        var pm = new ProjectManager();
        var result = pm.ImportDocuments(_basePath);

        Assert.Equal(1, result.SuccessfullyParsed);
        Assert.Single(pm.Documents);
        Assert.Equal("DOCREG", pm.Documents[0].DocumentType);
        Assert.Equal("DOCREG-124379-20260422", pm.Documents[0].DocumentNumber);
    }

    /// <summary>
    /// <see cref="ProjectManager.ImportDocuments"/> reloads <see cref="ProjectInfo"/>
    /// from disk at the start of a full scan and overwrites <see cref="ProjectManager.ProjectNumber"/>
    /// with the loaded value. To exercise the project-number-sensitive DocReg branch,
    /// we need to write a matching project_info.json before the scan.
    /// </summary>
    private void PrimeProjectInfo(string projectNumber)
    {
        var info = new ProjectInfo { ProjectNumber = projectNumber };
        info.Save(_basePath);
    }

    private static void WriteMinimalA4Pdf(string path)
    {
        // PdfPig 0.1.13: AddPage(PageSize, bool isPortrait)
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4, true);
        File.WriteAllBytes(path, builder.Build());
    }
}
