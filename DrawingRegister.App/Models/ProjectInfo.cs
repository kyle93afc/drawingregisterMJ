using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    public bool UseNumericRevisions { get; set; } = false;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RevisionScheme RevisionScheme { get; set; } = RevisionScheme.Legacy;

    public static ProjectInfo Load(string baseFolderPath)
    {
        var filePath = Path.Combine(baseFolderPath, FILENAME);
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var info = JsonSerializer.Deserialize<ProjectInfo>(json) ?? new ProjectInfo();
            info.MigrateLegacyFlags(json);
            return info;
        }
        return new ProjectInfo();
    }

    public void Save(string baseFolderPath)
    {
        var filePath = Path.Combine(baseFolderPath, FILENAME);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
    }

    public static string GetFilePath(string baseFolderPath)
    {
        return Path.Combine(baseFolderPath, FILENAME);
    }

    /// <summary>
    /// If the JSON on disk doesn't contain a RevisionScheme field, infer it from UseNumericRevisions
    /// so projects saved before the enum was introduced keep behaving the same way until the user
    /// explicitly picks a new scheme.
    /// </summary>
    private void MigrateLegacyFlags(string rawJson)
    {
        if (rawJson.Contains("\"RevisionScheme\"", StringComparison.Ordinal))
            return;

        RevisionScheme = UseNumericRevisions ? RevisionScheme.Numeric : RevisionScheme.Legacy;
    }
}

/// <summary>
/// How drawing revisions are generated and parsed for a given project.
/// </summary>
public enum RevisionScheme
{
    /// <summary>Prefix-based (P01, C01, T01, I01) falling back to alphabetical A, B, C.</summary>
    Legacy = 0,

    /// <summary>Plain whole numbers: 1, 2, 3...</summary>
    Numeric = 1,

    /// <summary>
    /// Project 124660 SSEN procedure: internal drafts 1A/1B/1C... then a formal issue drops the
    /// letter to Rev 1. Next cycle is 2A/2B/... then Rev 2.
    /// </summary>
    SubLetterNumeric = 2
}
