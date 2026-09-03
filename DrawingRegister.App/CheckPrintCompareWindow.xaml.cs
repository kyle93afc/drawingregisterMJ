using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DrawingRegister.App;

// Previous check print on the left, newest on the right, so the checker can see what changed between markups.
public partial class CheckPrintCompareWindow : Window
{
    private readonly CheckPrint _previous;
    private readonly CheckPrint _latest;

    public CheckPrintCompareWindow(CheckPrint previous, CheckPrint latest)
    {
        InitializeComponent();
        (_previous, _latest) = (previous, latest);
        LeftTitle.Text = Caption(previous);
        RightTitle.Text = Caption(latest);
        Loaded += async (_, _) =>
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrawingRegister", "WebView2"));
                await Task.WhenAll(Show(LeftView, previous.FilePath, env), Show(RightView, latest.FilePath, env));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open PDFs for comparison: {ex.Message}", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        };
        // Release file handles so Bluebeam can save over them.
        Closed += (_, _) => { LeftView.Dispose(); RightView.Dispose(); };
    }

    private void OpenInBluebeam_Click(object sender, RoutedEventArgs e)
    {
        if (!BluebeamLauncher.Open(_previous.FilePath, _latest.FilePath))
            MessageBox.Show("Bluebeam Revu was not found. Opened each PDF with the default viewer instead.", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string Caption(CheckPrint cp) =>
        $"CP{cp.Cp:00}  rev {cp.Revision}  |  {cp.StatusText}  ({Path.GetFileName(cp.FilePath)})";

    private static async Task Show(WebView2 view, string filePath, CoreWebView2Environment env)
    {
        await view.EnsureCoreWebView2Async(env);
        view.CoreWebView2.Settings.AreDevToolsEnabled = false;
        view.CoreWebView2.NavigationStarting += (_, args) =>
            args.Cancel = !args.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        view.CoreWebView2.Navigate(new Uri(filePath).AbsoluteUri + "#view=Fit");
    }
}
