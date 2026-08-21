using System.Windows;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace DrawingRegister.App;

public partial class CheckingWindow : Window
{
    private readonly ProjectManager _project;
    private IReadOnlyList<CheckPrintQueueRow> _rows = [];

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

    private void CheckGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CheckGrid.SelectedItem is not CheckPrintQueueRow row)
            return;

        DocumentCodeBox.Text = row.CheckPrint.DocumentCode;
        RevisionBox.Text = row.CheckPrint.Revision;
    }

    private async void ReserveCp_Click(object sender, RoutedEventArgs e)
    {
        var documentCode = DocumentCodeBox.Text.Trim();
        var revision = RevisionBox.Text.Trim();
        if (string.IsNullOrEmpty(documentCode) || string.IsNullOrEmpty(revision))
        {
            MessageBox.Show("Enter a document code and revision first.", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ReserveButton.IsEnabled = false;
        ScanStatus.Text = "Reserving the next CP number…";
        try
        {
            var reservation = await Task.Run(() =>
                CheckPrintAllocator.ReserveNext(_project._currentBasePath, documentCode, revision));
            ScanStatus.Text = $"Reserved CP {reservation.Cp} for {reservation.DocumentCode} revision {reservation.Revision}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"CP reservation failed: {ex.Message}\n\nNo CP number was reserved. Please try again.",
                "Check Prints",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ScanStatus.Text = "Reservation failed; please retry.";
        }
        finally
        {
            ReserveButton.IsEnabled = true;
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Check Print Status",
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"check-print-status-{DateTime.Today:yyyyMMdd}.csv"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            CheckStatusReport.WriteCsv(_rows, dialog.FileName);
            ScanStatus.Text = $"Exported {_rows.Count} row(s) to {dialog.FileName}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"CSV export failed: {ex.Message}", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private int RefreshQueue()
    {
        _rows = CheckStatusReport.Render(new ApplyResult(_project.CheckPrints.ToList()), _project.Documents);
        CheckGrid.ItemsSource = _rows;
        return _rows.Count;
    }
}
