using System.Collections.Generic;

namespace DrawingRegister.App.Models;

public class ImportResult
{
    public int TotalPdfFiles { get; set; }
    public int SuccessfullyParsed { get; set; }
    public List<SkippedFileInfo> SkippedFiles { get; set; } = new();
    public bool HasSkippedFiles => SkippedFiles.Count > 0;
    public List<FileRenameInfo> SuggestedRenames { get; set; } = new();
    public bool HasSuggestedRenames => SuggestedRenames.Count > 0;
}

public class SkippedFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class FileRenameInfo
{
    public string OriginalPath { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
}
