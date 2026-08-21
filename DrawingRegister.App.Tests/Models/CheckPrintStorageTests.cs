using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Models;

public sealed class CheckPrintStorageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"dr-check-storage-test-{Guid.NewGuid():N}");

    public CheckPrintStorageTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch { }
    }

    [Fact]
    public void ProjectStorage_round_trips_check_prints_separately_from_register_documents()
    {
        var path = Path.Combine(_folder, "project_data.json");
        new ProjectStorage
        {
            Documents = [new DocumentStorageInfo { DocumentNumber = "REGISTER-01" }],
            CheckingFolderPath = @"C:\Checking",
            CheckPrints = [CheckPrint("hash-one")]
        }.Save(path);

        var loaded = ProjectStorage.Load(path);

        Assert.Equal("REGISTER-01", Assert.Single(loaded.Documents).DocumentNumber);
        Assert.Equal(@"C:\Checking", loaded.CheckingFolderPath);
        Assert.Equal("hash-one", Assert.Single(loaded.CheckPrints).SourceHash);
    }

    [Fact]
    public void ProjectManager_loads_saved_check_print_inventory()
    {
        new ProjectStorage
        {
            Projects = [new DrawingProject { FolderPath = Path.Combine(_folder, "already-scanned") }],
            CheckPrints = [CheckPrint("saved-hash")]
        }.Save(Path.Combine(_folder, "project_data.json"));

        var manager = new ProjectManager();
        manager.ImportDocuments(_folder);

        Assert.Equal("saved-hash", Assert.Single(manager.CheckPrints).SourceHash);
        Assert.Empty(manager.Documents);
    }

    [Fact]
    public void Saving_same_scan_twice_replaces_inventory_without_duplicates()
    {
        var registerScanDate = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Local);
        var manager = new ProjectManager
        {
            _currentBasePath = _folder,
            _currentStorage = new ProjectStorage
            {
                LastScanDate = registerScanDate,
                LastProcessedDate = registerScanDate,
                Documents = [new DocumentStorageInfo { DocumentNumber = "REGISTER-UNCHANGED" }]
            }
        };
        var result = new ApplyResult([CheckPrint("same-hash")]);

        manager.StoreCheckPrintInventory(@"C:\Checking", result);
        manager.StoreCheckPrintInventory(@"C:\Checking", result);

        var stored = ProjectStorage.Load(Path.Combine(_folder, "project_data.json"));
        Assert.Single(stored.CheckPrints);
        Assert.Single(manager.CheckPrints);
        Assert.Equal("REGISTER-UNCHANGED", Assert.Single(stored.Documents).DocumentNumber);
        Assert.Equal(registerScanDate, stored.LastScanDate);
        Assert.Equal(registerScanDate, stored.LastProcessedDate);
    }

    private static CheckPrint CheckPrint(string hash) => new()
    {
        DocumentCode = "124660-M+J-V1-XX-DR-A-01-02",
        Revision = "1A",
        Cp = 1,
        Status = CheckStatus.FC,
        FilePath = @"C:\Checking\print.pdf",
        SourceHash = hash
    };
}
