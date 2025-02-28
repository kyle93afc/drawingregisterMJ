using System;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using System.IO;
using System.Diagnostics;

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
    }
}

