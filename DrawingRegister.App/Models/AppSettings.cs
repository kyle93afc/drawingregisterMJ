using System;
using System.IO;
using System.Text.Json;

namespace DrawingRegister.App.Models;

/// <summary>
/// Machine/user-level application settings (persisted in %LocalAppData%/DrawingRegister/appsettings.json).
/// Allows configuring default organization profile and server/projects paths independently per machine.
/// </summary>
public class AppSettings
{
    private const string SETTINGS_DIR = "DrawingRegister";
    private const string SETTINGS_FILE = "appsettings.json";

    public string DefaultOrganizationId { get; set; } = "MJ";
    public string DefaultProjectsFolder { get; set; } = string.Empty;
    public string LastOpenedFolder { get; set; } = string.Empty;

    private static AppSettings? _current;
    public static AppSettings Current => _current ??= Load();

    public static string GetSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, SETTINGS_DIR);
        return Path.Combine(dir, SETTINGS_FILE);
    }

    public static AppSettings Load()
    {
        try
        {
            var filePath = GetSettingsPath();
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                    return settings;
            }
        }
        catch
        {
            // Fall back to defaults on read failure.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var filePath = GetSettingsPath();
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Do not throw if settings cannot be written.
        }
    }

    public static void ResetCache()
    {
        _current = null;
    }
}
