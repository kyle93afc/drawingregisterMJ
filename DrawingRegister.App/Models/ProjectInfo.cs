using System;
using System.IO;
using System.Text.Json;

namespace DrawingRegister.App.Models;

/// <summary>
/// Stores project metadata in a separate JSON file (project_info.json).
/// This includes Discipline, Register Number, Project Number, Client Number, and Project Name.
/// Created: January 2026 - Separated from project_data.json for cleaner data management.
/// </summary>
public class ProjectInfo
{
    private const string FILENAME = "project_info.json";

    public string ProjectNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string ClientNumber { get; set; } = string.Empty;

    public static ProjectInfo Load(string baseFolderPath)
    {
        var filePath = Path.Combine(baseFolderPath, FILENAME);
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ProjectInfo>(json) ?? new ProjectInfo();
        }
        return new ProjectInfo();
    }

    public void Save(string baseFolderPath)
    {
        var filePath = Path.Combine(baseFolderPath, FILENAME);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
    }

    public static string GetFilePath(string baseFolderPath)
    {
        return Path.Combine(baseFolderPath, FILENAME);
    }
}
