using System;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using System.IO;
using System.Diagnostics;
using Squirrel;
using System.Threading.Tasks;

namespace DrawingRegister.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public App()
        {
            try
            {
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                
                // Add startup logging
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                
                // Log application startup
                LogMessage("Application starting up");
            }
            catch (Exception ex)
            {
                LogMessage($"Error in App constructor: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing application: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Initialization Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogMessage($"Unhandled exception: {exception?.Message}\n{exception?.StackTrace}");
            
            MessageBox.Show($"Fatal error: {exception?.Message}\n\nStack Trace:\n{exception?.StackTrace}", 
                "Fatal Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogMessage($"Dispatcher unhandled exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
            
            MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}", 
                "Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            
            e.Handled = true;
        }
        
        private void LogMessage(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n");
                Debug.WriteLine($"[{DateTime.Now}] {message}");
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
        
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                LogMessage("OnStartup called");
                base.OnStartup(e);
                
                // Check for updates in the background
                Task.Run(async () => await CheckForUpdates());
                
                LogMessage("OnStartup completed");
            }
            catch (Exception ex)
            {
                LogMessage($"Error in OnStartup: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error during startup: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Startup Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
        
        private async Task CheckForUpdates()
        {
            try
            {
                // Replace with your GitHub releases URL
                string updateUrl = "https://github.com/YourUsername/YourRepo/releases";
                
                using (var mgr = await UpdateManager.GitHubUpdateManager(updateUrl))
                {
                    var updateInfo = await mgr.CheckForUpdate();
                    
                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        // Show update notification on UI thread
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var result = MessageBox.Show(
                                "A new version of Drawing Register is available. Would you like to update now?",
                                "Update Available",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);
                                
                            if (result == MessageBoxResult.Yes)
                            {
                                Task.Run(async () => await PerformUpdate(mgr));
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error checking for updates: {ex.Message}");
                // Don't show error to user - updates are optional
            }
        }
        
        private async Task PerformUpdate(UpdateManager mgr)
        {
            try
            {
                await mgr.UpdateApp();
                
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        "Update downloaded successfully. The application will restart to apply the update.",
                        "Update Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                        
                    UpdateManager.RestartApp();
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error performing update: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"Failed to update: {ex.Message}",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }
    }
}

