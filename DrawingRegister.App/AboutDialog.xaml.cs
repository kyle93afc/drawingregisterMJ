using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using DrawingRegister.App.Services;

namespace DrawingRegister.App;

/// <summary>
/// About dialog displaying application information, version, and links.
/// </summary>
public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        // Set version from UpdateService
        VersionText.Text = $"Version {UpdateService.CurrentVersion}";
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Header_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
    }
}
