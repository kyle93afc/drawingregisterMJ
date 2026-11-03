using System;
using System.IO;
using System.Linq;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class DocRegFolderScannerTests : IDisposable
{
    private readonly string _folder;

    public DocRegFolderScannerTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "DocRegFolderScannerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
        catch
        {
            // Best effort cleanup in test temp directory.
        }
    }

    [Fact]
    public void Scan_reads_top_level_pdfs_in_a_flat_folder()
    {
        WritePdf("12499-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf");
        WritePdf("A-101 - Ground Floor Plan - Rev B.pdf");
        WritePdf("NOTES.PDF");
        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not a pdf");
        File.WriteAllBytes(Path.Combine(_folder, "A-101.dwg"), Array.Empty<byte>());

        var result = DocRegFolderScanner.Scan(_folder, OrganizationRegistry.MJ);

        Assert.Equal(3, result.Drawings.Count);
        Assert.Contains(result.Drawings, d => d.DocumentNumber == "12499-M+J-V1-XX-DR-S-16-01");
        Assert.Contains(result.Drawings, d => d.DocumentNumber == "A-101");
        Assert.Contains(result.Drawings, d => d.DocumentNumber == "NOTES");
        Assert.DoesNotContain(result.Drawings, d => d.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Drawings, d => d.FileName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(_folder, result.FolderPath);
    }

    [Fact]
    public void Scan_includes_mixed_drawings_in_dated_issue_folders_and_ignores_archive_subfolders()
    {
        var issue = Directory.CreateDirectory(Path.Combine(_folder, "20261102"));
        File.WriteAllBytes(Path.Combine(issue.FullName, "12499-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(issue.FullName, "P26-999 - Sample Site - BW_01A - Site Plan.pdf"), Array.Empty<byte>());
        var archive = Directory.CreateDirectory(Path.Combine(issue.FullName, "SS"));
        File.WriteAllBytes(Path.Combine(archive.FullName, "A-OLD - Superseded.pdf"), Array.Empty<byte>());
        var invalidDate = Directory.CreateDirectory(Path.Combine(_folder, "20261399"));
        File.WriteAllBytes(Path.Combine(invalidDate.FullName, "A-INVALID - Not an issue folder.pdf"), Array.Empty<byte>());

        var result = DocRegFolderScanner.Scan(_folder, OrganizationRegistry.MJ);

        Assert.Equal(2, result.Drawings.Count);
        Assert.Contains(result.Drawings, drawing => drawing.Section == DocRegSection.InHouse);
        Assert.Contains(result.Drawings, drawing => drawing.DocumentNumber == "BW_01A" && drawing.Section == DocRegSection.ArchitectExternal);
    }

    [Fact]
    public void Scan_does_not_include_pdfs_from_subfolders()
    {
        WritePdf("A-101 - Ground Floor Plan - Rev B.pdf");
        var nested = Directory.CreateDirectory(Path.Combine(_folder, "SS"));
        File.WriteAllBytes(Path.Combine(nested.FullName, "S-99 - Nested Sheet - Rev A.pdf"), Array.Empty<byte>());

        var result = DocRegFolderScanner.Scan(_folder, OrganizationRegistry.MJ);

        Assert.Single(result.Drawings);
        Assert.Equal("A-101", result.Drawings[0].DocumentNumber);
        Assert.DoesNotContain(result.Drawings, d => d.DocumentNumber.Contains("S-99", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_excludes_generated_docreg_filename_and_reports_count()
    {
        WritePdf("A-101 - Ground Floor Plan - Rev B.pdf");
        WritePdf("DocReg-12499-20261103.pdf");
        WritePdf("docreg-12499-20261104.pdf");
        WritePdf("DocReg-12499-20261103 (1).pdf");

        var result = DocRegFolderScanner.Scan(_folder, OrganizationRegistry.MJ);

        Assert.Single(result.Drawings);
        Assert.Equal("A-101", result.Drawings[0].DocumentNumber);
        Assert.Equal(3, result.ExcludedGeneratedCount);
        Assert.False(string.IsNullOrWhiteSpace(result.ExclusionNote));
    }

    [Fact]
    public void Scan_missing_folder_throws_and_does_not_create_project_files()
    {
        var missing = Path.Combine(_folder, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(() =>
            DocRegFolderScanner.Scan(missing, OrganizationRegistry.MJ));

        Assert.False(Directory.Exists(missing));
        Assert.False(File.Exists(Path.Combine(_folder, "project_info.json")));
        Assert.False(File.Exists(Path.Combine(_folder, "project_data.json")));
    }

    [Fact]
    public void Scan_does_not_write_project_files_or_change_source_pdfs()
    {
        var sourcePath = WritePdf("A-101 - Ground Floor Plan - Rev B.pdf");
        var originalBytes = File.ReadAllBytes(sourcePath);
        var originalWriteTime = File.GetLastWriteTimeUtc(sourcePath);

        DocRegFolderScanner.Scan(_folder, OrganizationRegistry.MJ);

        Assert.False(File.Exists(Path.Combine(_folder, "project_info.json")));
        Assert.False(File.Exists(Path.Combine(_folder, "project_data.json")));
        Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(sourcePath));
    }

    [Fact]
    public void Scan_keeps_duplicate_references_as_distinct_rows()
    {
        WritePdf("A-101 - Ground Floor Plan - Rev B.pdf");
        WritePdf("A-101 - Ground Floor Plan Copy - Rev B.pdf");

        var result = DocRegFolderScanner.Scan(_folder, OrganizationRegistry.MJ);

        Assert.Equal(2, result.Drawings.Count);
        Assert.All(result.Drawings, d => Assert.Equal("A-101", d.DocumentNumber));
        Assert.All(result.Drawings, d => Assert.Equal("B", d.Revision));
        Assert.Equal(2, result.Drawings.Select(d => d.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Scan_enriches_only_when_document_number_and_revision_match()
    {
        WritePdf("12499-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf");
        var matchingPath = Path.Combine(_folder, "12499-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf");
        File.SetLastWriteTimeUtc(matchingPath, new DateTime(2020, 1, 2, 8, 0, 0, DateTimeKind.Utc));

        WritePdf("12499-M+J-V1-XX-DR-S-16-01-T03-FOUNDATION PLAN.pdf");

        var register = new DocumentMetadata
        {
            DocumentNumber = "12499-M+J-V1-XX-DR-S-16-01",
            Description = "From Register",
            Package = "16",
            DocumentType = "DR",
            Size = "A1",
            RevisionHistory =
            {
                [new DateTime(2026, 4, 1)] = new RevisionInfo { Revision = "T01" },
                [new DateTime(2026, 5, 1)] = new RevisionInfo { Revision = "T02" }
            }
        };

        var result = DocRegFolderScanner.Scan(
            _folder,
            OrganizationRegistry.MJ,
            new[] { register },
            currentProjectNumber: "12499");

        var matched = result.Drawings.Single(d => d.Revision == "T01");
        Assert.Equal(new DateTime(2026, 4, 1), matched.IssueDate);
        Assert.Equal("A1", matched.Size);
        Assert.Equal("FOUNDATION PLAN", matched.Description);
        Assert.NotEqual(new DateTime(2026, 5, 1), matched.IssueDate);
        Assert.NotEqual(new DateTime(2020, 1, 2), matched.IssueDate?.Date);

        var unmatched = result.Drawings.Single(d => d.Revision == "T03");
        Assert.Null(unmatched.IssueDate);
        Assert.Equal(string.Empty, unmatched.Size);
    }

    [Fact]
    public void Scan_does_not_use_folder_name_as_architect_project_number()
    {
        var namedFolder = Path.Combine(_folder, "24012-SampleSite");
        Directory.CreateDirectory(namedFolder);
        File.WriteAllBytes(
            Path.Combine(namedFolder, "A-101 - Ground Floor Plan - Rev B.pdf"),
            Array.Empty<byte>());

        var result = DocRegFolderScanner.Scan(namedFolder, OrganizationRegistry.MJ, currentProjectNumber: "12499");

        Assert.Single(result.Drawings);
        Assert.Null(result.Drawings[0].ParsedProjectNumber);
        Assert.Equal("A-101", result.Drawings[0].DocumentNumber);
    }

    [Fact]
    public void Scan_notes_in_house_project_number_mismatch()
    {
        WritePdf("12499-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf");

        var result = DocRegFolderScanner.Scan(
            _folder,
            OrganizationRegistry.MJ,
            currentProjectNumber: "12500");

        Assert.Contains("does not match", result.Drawings[0].ParseNote, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("12499", result.Drawings[0].ParsedProjectNumber);
    }

    private string WritePdf(string fileName)
    {
        var path = Path.Combine(_folder, fileName);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }
}
