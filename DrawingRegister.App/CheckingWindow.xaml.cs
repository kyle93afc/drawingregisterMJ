using System.Windows;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using MessageBox = System.Windows.MessageBox;

namespace DrawingRegister.App;

public partial class CheckingWindow : Window
{
    private readonly ProjectManager _project;

    public CheckingWindow(ProjectManager project)
    {
        InitializeComponent();
        _project = project;
        FolderPathBox.Text = project._currentStorage?.CheckingFolderPath ?? string.Empty;
        var liveCount = RefreshQueue();
        ScanStatus.Text = project.CheckPrints.Count == 0
            ? "Select a checking folder to begin."
            : $"Loaded {project.CheckPrints.Count} saved check print(s); {liveCount} live.";
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select checking folder",
            UseDescriptionForTitle = true,
            SelectedPath = FolderPathBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            FolderPathBox.Text = dialog.SelectedPath;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FolderPathBox.Text))
        {
            MessageBox.Show("Select a checking folder first.", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ScanButton.IsEnabled = false;
        ScanStatus.Text = "Scanning PDFs…";
        try
        {
            var folderPath = FolderPathBox.Text;
            var plan = await Task.Run(() => CheckPrintScanner.Plan(folderPath));
            var result = CheckPrintApplier.Apply(plan);
            _project.StoreCheckPrintInventory(folderPath, result);

            var flagged = result.Facts.Count(fact => fact.IsFlagged);
            var liveCount = RefreshQueue();
            ScanStatus.Text = $"Scanned {result.Facts.Count} PDF(s); {flagged} flagged; {liveCount} live.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Check-print scan failed: {ex.Message}", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Error);
            ScanStatus.Text = "Scan failed.";
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    private int RefreshQueue()
    {
        var rows = CheckPrintRegisterJoin.BuildLiveQueue(_project.CheckPrints, _project.Documents);
        CheckGrid.ItemsSource = rows;
        return rows.Count;
    }
}
