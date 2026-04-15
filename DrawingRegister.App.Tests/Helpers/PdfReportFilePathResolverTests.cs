using DrawingRegister.App.Helpers;

namespace DrawingRegister.App.Tests.Helpers;

public sealed class PdfReportFilePathResolverTests
{
    [Fact]
    public void GetWritablePath_returns_original_path_when_target_is_available()
    {
        using var workspace = new TestWorkspace();
        var filePath = Path.Combine(workspace.RootPath, "Register.pdf");

        var resolvedPath = PdfReportFilePathResolver.GetWritablePath(filePath);

        Assert.Equal(filePath, resolvedPath);
    }

    [Fact]
    public void GetWritablePath_returns_suffixed_path_when_target_is_locked()
    {
        using var workspace = new TestWorkspace();
        var filePath = Path.Combine(workspace.RootPath, "Register.pdf");
        File.WriteAllText(filePath, "existing");

        using var lockedStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var resolvedPath = PdfReportFilePathResolver.GetWritablePath(filePath);

        Assert.Equal(Path.Combine(workspace.RootPath, "Register (1).pdf"), resolvedPath);
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
