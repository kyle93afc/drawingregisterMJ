using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class CheckPrintAllocatorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"dr-check-allocation-test-{Guid.NewGuid():N}");
    private readonly string _storagePath;

    public CheckPrintAllocatorTests()
    {
        Directory.CreateDirectory(_folder);
        _storagePath = Path.Combine(_folder, "project_data.json");
        new ProjectStorage().Save(_storagePath, updateProcessedDate: false);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch { }
    }

    [Fact]
    public void Initial_allocation_reserves_CP_one_without_changing_project_data()
    {
        var projectData = File.ReadAllText(_storagePath);

        var reservation = CheckPrintAllocator.ReserveNext(_folder, "DOC-01", "A");

        Assert.Equal(1, reservation.Cp);
        Assert.Equal(projectData, File.ReadAllText(_storagePath));
    }

    [Fact]
    public void Repeated_allocation_uses_the_persisted_reservation()
    {
        CheckPrintAllocator.ReserveNext(_folder, "DOC-01", "A");

        var reservation = CheckPrintAllocator.ReserveNext(_folder, "DOC-01", "A");

        Assert.Equal(2, reservation.Cp);
    }

    [Fact]
    public void Scanned_CP_sets_the_next_number_and_the_sequence_continues_across_revisions()
    {
        new ProjectStorage
        {
            CheckPrints =
            [
                new CheckPrint { DocumentCode = "DOC-01", Revision = "A", Cp = 4 },
                new CheckPrint { DocumentCode = "DOC-02", Revision = "A", Cp = 9 }
            ]
        }.Save(_storagePath, updateProcessedDate: false);

        var sameRevision = CheckPrintAllocator.ReserveNext(_folder, "DOC-01", "A");
        var newRevision = CheckPrintAllocator.ReserveNext(_folder, "DOC-01", "B");

        Assert.Equal(5, sameRevision.Cp);
        Assert.Equal(6, newRevision.Cp);
        Assert.Equal("B", newRevision.Revision);
    }

    [Fact]
    public async Task Competing_requests_receive_distinct_CP_numbers()
    {
        var requests = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => CheckPrintAllocator.ReserveNext(_folder, "DOC-01", "A").Cp));

        var numbers = await Task.WhenAll(requests);

        Assert.Equal(Enumerable.Range(1, 8), numbers.Order());
    }

    [Fact]
    public void Unavailable_project_store_creates_no_apparent_reservation()
    {
        File.Delete(_storagePath);

        var exception = Assert.Throws<DirectoryNotFoundException>(() =>
            CheckPrintAllocator.ReserveNext(_folder, "DOC-01", "A"));

        Assert.Contains("unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_folder, "check_print_reservations.json")));
    }
}
