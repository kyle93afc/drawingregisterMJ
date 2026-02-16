using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DrawingRegister.App.Services;
using Serilog;
using Velopack;
using MessageBox = System.Windows.MessageBox;

namespace DrawingRegister.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static UpdateService? _updateService;

    /// <summary>
    /// Custom entry point for the application.
    /// Velopack requires this to be the first thing that runs.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // IMPORTANT: This must be the first line in Main()
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    public App()
    {
        try
        {
            // Configure Serilog for structured logging
            ConfigureSerilog();

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            Log.Information("Application starting up - Version {Version}", UpdateService.CurrentVersion);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error initializing application: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Log.Information("OnStartup called");
            base.OnStartup(e);

            // Show "What's New" if version changed since last launch
            _ = ShowWhatsNewIfUpdatedAsync();

            // Check for updates on startup (fire-and-forget)
            _ = CheckForUpdatesOnStartupAsync();

            Log.Information("OnStartup completed");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error during startup");
            MessageBox.Show(
                $"Error during startup: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static async Task ShowWhatsNewIfUpdatedAsync()
    {
        try
        {
            var versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_version.txt");
            var currentVersion = UpdateService.CurrentVersion;
            var lastVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : null;

            // Always write current version so first launch doesn't trigger
            File.WriteAllText(versionFile, currentVersion);

            if (lastVersion == null || lastVersion == currentVersion) return;

            Log.Information("Version changed from {OldVersion} to {NewVersion}, showing What's New", lastVersion, currentVersion);

            var updateService = new UpdateService();
            var releaseNotes = await updateService.GetReleaseNotesAsync(currentVersion);

            if (string.IsNullOrWhiteSpace(releaseNotes)) return;

            await Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    $"Updated to v{currentVersion}\n\n{releaseNotes}",
                    "What's New",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show What's New dialog");
        }
    }

    private static async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            _updateService = new UpdateService();

            if (!_updateService.IsInstalled)
            {
                Log.Information("Application not installed via Velopack, skipping update check");
                return;
            }

            var updateAvailable = await _updateService.CheckForUpdatesAsync();

            if (updateAvailable)
            {
                var newVersion = _updateService.GetAvailableVersion() ?? "unknown";
                Log.Information("Update available: {NewVersion}", newVersion);

                // Fetch release notes from GitHub
                var releaseNotes = await _updateService.GetReleaseNotesAsync(newVersion);
                var notesSection = string.IsNullOrWhiteSpace(releaseNotes)
                    ? ""
                    : $"\n\n{releaseNotes}";

                // Show update dialog on UI thread
                await Current.Dispatcher.InvokeAsync(() =>
                {
                    var result = MessageBox.Show(
                        $"A new version ({newVersion}) is available!\n\nCurrent version: {UpdateService.CurrentVersion}{notesSection}\n\nWould you like to download and install it now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        _ = DownloadAndInstallUpdateAsync();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates on startup");
            // Don't show error to user - update check is non-blocking
        }
    }

    private static async Task DownloadAndInstallUpdateAsync()
    {
        if (_updateService == null) return;

        try
        {
            // Show progress dialog
            var progressDialog = new UpdateProgressDialog();
            progressDialog.Show();

            await _updateService.DownloadUpdatesAsync(progress =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    progressDialog.UpdateProgress(progress);
                });
            });

            progressDialog.Close();

            var result = MessageBox.Show(
                "Update downloaded successfully!\n\nThe application will now restart to apply the update.",
                "Update Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _updateService.ApplyUpdatesAndRestart();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download and install update");
            MessageBox.Show(
                $"Failed to download update: {ex.Message}",
                "Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application shutting down");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureSerilog()
    {
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "app_.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Log.Fatal(exception, "Unhandled domain exception");

        MessageBox.Show(
            $"Fatal error: {exception?.Message}\n\nStack Trace:\n{exception?.StackTrace}",
            "Fatal Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Dispatcher unhandled exception");

        MessageBox.Show(
            $"An error occurred: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
