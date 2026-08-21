using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace DrawingRegister.App.Models;

public class ProjectStorage
{
    // Static project metadata (ProjectNumber, ProjectName, Discipline, RegisterNumber, ClientNumber)
    // has been moved to ProjectInfo.cs (project_info.json) for separate persistence.
    public string BaseFolderPath { get; set; } = string.Empty;
    public DateTime LastScanDate { get; set; }
    public DateTime LastProcessedDate { get; set; }
    public List<DocumentStorageInfo> Documents { get; set; } = new();
    public List<DrawingProject> Projects { get; set; } = new();
    public string CheckingFolderPath { get; set; } = string.Empty;
    public List<CheckPrint> CheckPrints { get; set; } = new();

    public static ProjectStorage Load(string filePath)
    {
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ProjectStorage>(json) ?? new ProjectStorage();
        }
        return new ProjectStorage();
    }

    public void Save(string filePath, bool updateProcessedDate = true)
    {
        if (updateProcessedDate)
            LastProcessedDate = DateTime.Now;
        var json = JsonSerializer.Serialize(this);
        File.WriteAllText(filePath, json);
    }
}

public class DocumentStorageInfo
{
    public string DocumentNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Package { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public Dictionary<DateTime, RevisionStorageInfo> RevisionHistory { get; set; } = new();
    public Dictionary<DateTime, List<string>> DistributionCompanyIds { get; set; } = new();
}

public class RevisionStorageInfo
{
    public string Revision { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public bool IsDistributed { get; set; }
    public string FilePath { get; set; } = string.Empty;
} 
