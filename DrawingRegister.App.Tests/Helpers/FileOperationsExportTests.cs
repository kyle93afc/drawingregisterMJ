using DrawingRegister.App.Helpers;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Helpers;

public sealed class FileOperationsExportTests
{
    [Fact]
    public void CollectLatestPdfs_creates_timestamped_folder_and_copies_visible_documents()
    {
        using var workspace = new TestWorkspace();
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source"));
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "exports"));

        var firstSourcePath = CreatePdf(sourceDirectory.FullName, "A-001.pdf", "first");
        var secondSourcePath = CreatePdf(sourceDirectory.FullName, "B-002.pdf", "second");

        var documents = new[]
        {
            new DocumentMetadata { DocumentNumber = "A-001", Description = "First", FilePath = firstSourcePath },
            new DocumentMetadata { DocumentNumber = "B-002", Description = "Second", FilePath = secondSourcePath }
        };

        var exportTimestamp = new DateTime(2026, 4, 14, 15, 30, 0);
        var exportResult = FileOperations.CollectLatestPdfs(documents, destinationDirectory.FullName, exportTimestamp);
        var exportFolderPath = exportResult.ExportFolderPath;

        Assert.Equal(Path.Combine(destinationDirectory.FullName, "Latest Drawings 2026-04-14 15-30"), exportFolderPath);
        Assert.Equal(2, exportResult.CopiedCount);
        Assert.Equal(0, exportResult.SkippedCount);
        Assert.True(File.Exists(Path.Combine(exportFolderPath, "A-001.pdf")));
        Assert.True(File.Exists(Path.Combine(exportFolderPath, "B-002.pdf")));
    }

    [Fact]
    public void CollectLatestPdfs_skips_missing_files_and_reports_them()
    {
        using var workspace = new TestWorkspace();
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source"));
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "exports"));

        var existingSourcePath = CreatePdf(sourceDirectory.FullName, "exists.pdf", "exists");
        var missingSourcePath = Path.Combine(sourceDirectory.FullName, "missing.pdf");

        var documents = new[]
        {
            new DocumentMetadata { DocumentNumber = "DR-001", Description = "Exists", FilePath = existingSourcePath },
            new DocumentMetadata { DocumentNumber = "DR-002", Description = "Missing", FilePath = missingSourcePath }
        };

        var exportResult = FileOperations.CollectLatestPdfs(documents, destinationDirectory.FullName, new DateTime(2026, 4, 14, 15, 30, 0));

        Assert.Equal(1, exportResult.CopiedCount);
        Assert.Equal(1, exportResult.SkippedCount);
        Assert.Single(exportResult.SkippedFiles);
    }

    [Fact]
    public void CollectLatestPdfs_renames_when_two_documents_would_export_with_the_same_name()
    {
        using var workspace = new TestWorkspace();
        var firstSourceDirectory = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source-1"));
        var secondSourceDirectory = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source-2"));
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "exports"));

        var firstSourcePath = CreatePdf(firstSourceDirectory.FullName, "shared.pdf", "first");
        var secondSourcePath = CreatePdf(secondSourceDirectory.FullName, "shared.pdf", "second");

        var documents = new[]
        {
            new DocumentMetadata { DocumentNumber = "DR-001", Description = "First", FilePath = firstSourcePath },
            new DocumentMetadata { DocumentNumber = "DR-002", Description = "Second", FilePath = secondSourcePath }
        };

        var exportResult = FileOperations.CollectLatestPdfs(documents, destinationDirectory.FullName, new DateTime(2026, 4, 14, 15, 30, 0));
        var exportFolderPath = exportResult.ExportFolderPath;

        var exportedFiles = Directory.GetFiles(exportFolderPath, "*.pdf").Select(Path.GetFileName).OrderBy(name => name).ToArray();

        Assert.Equal(2, exportedFiles.Length);
        Assert.Contains("shared.pdf", exportedFiles);
        Assert.Contains("shared_DR-002.pdf", exportedFiles);
    }

    private static string CreatePdf(string directory, string fileName, string contents)
    {
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, contents);
        return filePath;
    }

    private sealed class TestWorkspace : IDisposable
    {
        public TestWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "DrawingRegisterTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
