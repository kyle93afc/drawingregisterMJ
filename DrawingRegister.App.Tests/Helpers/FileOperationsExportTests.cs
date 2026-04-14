using System.Reflection;
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
        var exportMethod = typeof(FileOperations).GetMethod("CollectLatestPdfs", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(exportMethod);

        var exportResult = exportMethod!.Invoke(null, new object[] { documents, destinationDirectory.FullName, exportTimestamp });

        Assert.NotNull(exportResult);

        var exportFolderPath = (string?)exportResult!.GetType().GetProperty("ExportFolderPath")?.GetValue(exportResult);
        var copiedCount = (int?)exportResult.GetType().GetProperty("CopiedCount")?.GetValue(exportResult);
        var skippedCount = (int?)exportResult.GetType().GetProperty("SkippedCount")?.GetValue(exportResult);

        Assert.Equal(Path.Combine(destinationDirectory.FullName, "Latest Drawings 2026-04-14 15-30"), exportFolderPath);
        Assert.Equal(2, copiedCount);
        Assert.Equal(0, skippedCount);
        Assert.True(File.Exists(Path.Combine(exportFolderPath!, "A-001.pdf")));
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

        var exportMethod = typeof(FileOperations).GetMethod("CollectLatestPdfs", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(exportMethod);

        var exportResult = exportMethod!.Invoke(null, new object[] { documents, destinationDirectory.FullName, new DateTime(2026, 4, 14, 15, 30, 0) });

        Assert.NotNull(exportResult);

        var copiedCount = (int?)exportResult!.GetType().GetProperty("CopiedCount")?.GetValue(exportResult);
        var skippedCount = (int?)exportResult.GetType().GetProperty("SkippedCount")?.GetValue(exportResult);
        var skippedFiles = exportResult.GetType().GetProperty("SkippedFiles")?.GetValue(exportResult) as System.Collections.IEnumerable;

        Assert.Equal(1, copiedCount);
        Assert.Equal(1, skippedCount);
        Assert.NotNull(skippedFiles);
        Assert.Single(skippedFiles!.Cast<object>());
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

        var exportMethod = typeof(FileOperations).GetMethod("CollectLatestPdfs", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(exportMethod);

        var exportResult = exportMethod!.Invoke(null, new object[] { documents, destinationDirectory.FullName, new DateTime(2026, 4, 14, 15, 30, 0) });
        var exportFolderPath = (string?)exportResult!.GetType().GetProperty("ExportFolderPath")?.GetValue(exportResult);

        Assert.NotNull(exportFolderPath);

        var exportedFiles = Directory.GetFiles(exportFolderPath!, "*.pdf").Select(Path.GetFileName).OrderBy(name => name).ToArray();

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
