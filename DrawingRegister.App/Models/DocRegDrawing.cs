using CommunityToolkit.Mvvm.ComponentModel;

namespace DrawingRegister.App.Models;

public enum DocRegSection
{
    InHouse,
    ArchitectExternal
}

public partial class DocRegDrawing : ObservableObject
{
    [ObservableProperty] private bool include = true;
    [ObservableProperty] private DocRegSection section = DocRegSection.ArchitectExternal;
    [ObservableProperty] private string documentNumber = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private string revision = string.Empty;
    [ObservableProperty] private string package = string.Empty;
    [ObservableProperty] private string documentType = string.Empty;
    [ObservableProperty] private string size = string.Empty;
    [ObservableProperty] private DateTime? issueDate;
    [ObservableProperty] private string? parsedProjectNumber;

    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ParseNote { get; set; } = string.Empty;
}
