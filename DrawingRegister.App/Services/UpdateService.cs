using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace DrawingRegister.App.Services;

/// <summary>
/// Service for checking and applying application updates via GitHub releases.
/// </summary>
public class UpdateService
{
    private const string GitHubOwner = "kyle93afc";
    private const string GitHubRepo = "drawingregisterMJ";

    private readonly UpdateManager _updateManager;
    private UpdateInfo? _updateInfo;

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    public static string CurrentVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
    }

    public UpdateService()
    {
        var source = new GithubSource($"https://github.com/{GitHubOwner}/{GitHubRepo}", null, false);
        _updateManager = new UpdateManager(source);
    }

    /// <summary>
    /// Checks for available updates from GitHub releases.
    /// </summary>
    /// <returns>True if an update is available, false otherwise.</returns>
    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            Log.Information("Checking for updates...");

            if (!_updateManager.IsInstalled)
            {
                Log.Information("Application is not installed via Velopack, skipping update check");
                return false;
            }

            _updateInfo = await _updateManager.CheckForUpdatesAsync();

            if (_updateInfo != null)
            {
                Log.Information("Update available: {Version}", _updateInfo.TargetFullRelease?.Version);
                return true;
            }

            Log.Information("No updates available");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates");
            return false;
        }
    }

    /// <summary>
    /// Gets the version of the available update.
    /// </summary>
    public string? GetAvailableVersion()
    {
        return _updateInfo?.TargetFullRelease?.Version?.ToString();
    }

    /// <summary>
    /// Downloads the available update.
    /// </summary>
    /// <param name="progress">Progress callback (0-100).</param>
    public async Task DownloadUpdatesAsync(Action<int>? progress = null)
    {
        if (_updateInfo == null)
        {
            Log.Warning("No update info available. Call CheckForUpdatesAsync first.");
            return;
        }

        try
        {
            Log.Information("Downloading update...");
            await _updateManager.DownloadUpdatesAsync(_updateInfo, progress);
            Log.Information("Update downloaded successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download update");
            throw;
        }
    }

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// </summary>
    public void ApplyUpdatesAndRestart()
    {
        try
        {
            Log.Information("Applying update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_updateInfo);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply update");
            throw;
        }
    }

    /// <summary>
    /// Fetches release notes for a specific version from GitHub.
    /// </summary>
    public async Task<string> GetReleaseNotesAsync(string version)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DrawingRegister");
            var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/tags/v{version}";
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var body = doc.RootElement.GetProperty("body").GetString();
                return body ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to fetch release notes for version {Version}", version);
        }
        return string.Empty;
    }

    /// <summary>
    /// Checks if the application was installed via Velopack.
    /// </summary>
    public bool IsInstalled => _updateManager.IsInstalled;
}
