using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
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
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _rescanTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _scanning;
    private bool _rescanQueued;

    public CheckingWindow(ProjectManager project)
    {
        InitializeComponent();
        _project = project;
        FolderPathBox.Text = project._currentStorage?.CheckingFolderPath ?? string.Empty;
        RefreshQueue();
        ScanStatus.Text = project.CheckPrints.Count == 0
            ? "Select a checking folder to begin."
            : $"Loaded {project.CheckPrints.Count} saved check print(s).";

        _rescanTimer.Tick += async (_, _) => { _rescanTimer.Stop(); await ScanAsync(); };
        WatchFolder(FolderPathBox.Text);
        Closed += (_, _) => { _rescanTimer.Stop(); _watcher?.Dispose(); };
    }

    // Any PDF change in the checking folder (Bluebeam save, new print, rename, delete) restarts a 2s debounce, then rescans.
    private void WatchFolder(string folderPath)
    {
        _watcher?.Dispose();
        _watcher = null;
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return;

        _watcher = new FileSystemWatcher(folderPath, "*.pdf")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        FileSystemEventHandler bump = (_, _) => Dispatcher.BeginInvoke(() => { _rescanTimer.Stop(); _rescanTimer.Start(); });
        _watcher.Created += bump;
        _watcher.Changed += bump;
        _watcher.Deleted += bump;
        _watcher.Renamed += (s, e) => bump(s, e);
        _watcher.EnableRaisingEvents = true;
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

        WatchFolder(FolderPathBox.Text);
        await ScanAsync();
    }

    private async Task ScanAsync()
    {
        var folderPath = FolderPathBox.Text;
        if (string.IsNullOrWhiteSpace(folderPath))
            return;
        if (_scanning)
        {
            _rescanQueued = true;
            return;
        }

        _scanning = true;
        ScanButton.IsEnabled = false;
        ScanStatus.Text = "Scanning PDFs…";
        try
        {
            var plan = await Task.Run(() => CheckPrintScanner.Plan(folderPath));
            var result = CheckPrintApplier.Apply(plan);
            _project.StoreCheckPrintInventory(folderPath, result);

            var flagged = result.Facts.Count(fact => fact.IsFlagged);
            RefreshQueue();
            ScanStatus.Text = $"Scanned {result.Facts.Count} PDF(s) at {DateTime.Now:HH:mm:ss}; {flagged} flagged.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Check-print scan failed: {ex.Message}", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Error);
            ScanStatus.Text = "Scan failed.";
        }
        finally
        {
            ScanButton.IsEnabled = true;
            _scanning = false;
        }

        if (_rescanQueued)
        {
            _rescanQueued = false;
            await ScanAsync();
        }
    }

    private void FilterBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyView();
    }

    private CheckPrintQueueRow? SelectedRow =>
        CheckGrid.SelectedItem is CheckPrintQueueRow row && !string.IsNullOrEmpty(row.CheckPrint.DocumentCode) ? row : null;

    private void CheckGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ReserveButton.IsEnabled = SelectedRow is not null;
        CompareButton.IsEnabled = BluebeamButton.IsEnabled = PreviousCp(SelectedRow) is not null;
    }

    // Highest CP below the selected one for the same drawing, any revision.
    private CheckPrint? PreviousCp(CheckPrintQueueRow? row) => row is null ? null : _rows
        .Select(r => r.CheckPrint)
        .Where(cp => string.Equals(cp.DocumentCode, row.CheckPrint.DocumentCode, StringComparison.OrdinalIgnoreCase)
                     && cp.Cp < row.CheckPrint.Cp)
        .OrderByDescending(cp => cp.Cp)
        .FirstOrDefault();

    private void OpenInBluebeam_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is { } row && PreviousCp(row) is { } previous && !BluebeamLauncher.Open(previous.FilePath, row.CheckPrint.FilePath))
            ScanStatus.Text = "Bluebeam Revu not found; opened each PDF with the default viewer.";
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row || PreviousCp(row) is not { } previous)
            return;
        new CheckPrintCompareWindow(previous, row.CheckPrint) { Owner = this }.Show();
    }

    private void CheckGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (CheckGrid.SelectedItem is not CheckPrintQueueRow row)
            return;

        if (!File.Exists(row.CheckPrint.FilePath))
        {
            MessageBox.Show($"File not found: {row.CheckPrint.FilePath}", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = row.CheckPrint.FilePath, UseShellExecute = true });
    }

    private async void ReserveCp_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row)
            return;

        var documentCode = row.CheckPrint.DocumentCode;
        var revision = row.CheckPrint.Revision;
        ReserveButton.IsEnabled = false;
        ScanStatus.Text = "Reserving the next CP number…";
        try
        {
            var reservation = await Task.Run(() =>
                CheckPrintAllocator.ReserveNext(_project._currentBasePath, documentCode, revision));

            // Suggested filename: the selected print's name with its CP token replaced.
            var stem = Path.GetFileNameWithoutExtension(row.CheckPrint.FilePath);
            var suggested = Regex.Replace(stem, @"CP[-_\s]?\d+", $"CP{reservation.Cp:00}", RegexOptions.IgnoreCase) + ".pdf";
            System.Windows.Clipboard.SetText(suggested);
            ScanStatus.Text = $"CP{reservation.Cp:00} reserved for {documentCode} rev {revision}. Filename copied to clipboard.";
            MessageBox.Show(
                $"Next check print for {documentCode} rev {revision} is CP{reservation.Cp:00}.\n\nSave it as:\n{suggested}\n\n(Copied to clipboard.)",
                "Check Prints", MessageBoxButton.OK, MessageBoxImage.Information);
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
            ReserveButton.IsEnabled = SelectedRow is not null;
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
        ApplyView();
        return _rows.Count;
    }

    // Needs-checking first, then needs-technician, then needs-review, then checked; newest CP first within a drawing.
    private static int SortRank(CheckStatus? status) => status switch
    {
        CheckStatus.FC => 0,
        CheckStatus.COMMENTS => 1,
        CheckStatus.CONFLICT => 2,
        CheckStatus.UNKNOWN => 3,
        CheckStatus.AWC => 4,
        _ => 5
    };

    private void ApplyView()
    {
        IEnumerable<CheckPrintQueueRow> view = _rows;
        view = FilterBox.SelectedIndex switch
        {
            1 => view.Where(r => r.CheckPrint.Status == CheckStatus.FC),
            2 => view.Where(r => r.CheckPrint.Status == CheckStatus.COMMENTS),
            3 => view.Where(r => r.CheckPrint.Status is CheckStatus.UNKNOWN or CheckStatus.CONFLICT),
            4 => view.Where(r => r.CheckPrint.Status is CheckStatus.AWC or CheckStatus.APPD),
            _ => view
        };
        CheckGrid.ItemsSource = view
            .OrderBy(r => SortRank(r.CheckPrint.Status))
            .ThenBy(r => r.CheckPrint.DocumentCode, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(r => r.CheckPrint.Cp)
            .ToList();
    }
}
