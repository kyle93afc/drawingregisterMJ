using System;
using System.IO;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Models;

public sealed class ProjectManagerNoDateFoldersTests : IDisposable
{
    private readonly string _basePath;

    public ProjectManagerNoDateFoldersTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dr-nodate-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_basePath, "Old Drawings"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_basePath, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public void ImportDocuments_folder_without_date_subfolders_returns_warning_instead_of_throwing()
    {
        var result = new ProjectManager().ImportDocuments(_basePath);

        Assert.Equal(0, result.TotalPdfFiles);
        Assert.Equal("No valid new date folders found. Folders should start with a date in format YYYYMMDD.", result.Warning);
    }
}
