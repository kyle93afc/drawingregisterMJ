using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;
using Serilog;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DrawingRegister.App;

public partial class DocRegFolderDialog : Window
{
    private readonly OrganizationProfile _organization;
    private readonly IReadOnlyList<DocumentMetadata> _registerDocuments;
    private readonly string? _registerNumber;
    private readonly string? _clientNumber;
    private readonly string? _initialFolderPath;
    private readonly ObservableCollection<DocRegDrawing> _drawings = new();
    private string? _scannedFolderPath;
    private bool _hasEdits;
    private bool _applyingScan;

    public DocRegFolderDialog(
        string? projectNumber,
        string? projectName,
        string? discipline,
        string? registerNumber,
        string? clientNumber,
        OrganizationProfile? organization,
        IReadOnlyList<DocumentMetadata>? registerDocuments,
        string? initialFolderPath = null)
    {
        InitializeComponent();

        _organization = organization ?? OrganizationRegistry.Default;
        _registerDocuments = registerDocuments ?? Array.Empty<DocumentMetadata>();
        _registerNumber = registerNumber;
        _clientNumber = clientNumber;
        _initialFolderPath = initialFolderPath;

        ProjectNumberBox.Text = projectNumber ?? string.Empty;
        ProjectNameBox.Text = projectName ?? string.Empty;
        DisciplineBox.Text = discipline ?? string.Empty;
        SectionColumn.ItemsSource = new[]
        {
            new KeyValuePair<DocRegSection, string>(DocRegSection.InHouse, $"{_organization.ShortName} drawings"),
            new KeyValuePair<DocRegSection, string>(DocRegSection.ArchitectExternal, "Architect / external")
        };
        ReviewGrid.ItemsSource = _drawings;
        _drawings.CollectionChanged += Drawings_CollectionChanged;
        UpdateCounts();
        Loaded += DocRegFolderDialog_Loaded;
    }

    private async void DocRegFolderDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= DocRegFolderDialog_Loaded;
        if (!string.IsNullOrWhiteSpace(_initialFolderPath))
            await ScanSelectedFolderAsync(_initialFolderPath);
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var picker = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder of M+J and architect PDFs",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_scannedFolderPath) ? _scannedFolderPath : FolderPathBox.Text
        };

        if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        await ScanSelectedFolderAsync(picker.SelectedPath);
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        await ScanSelectedFolderAsync();
    }

    private async Task ScanSelectedFolderAsync(string? folderPath = null)
    {
        folderPath = string.IsNullOrWhiteSpace(folderPath) ? FolderPathBox.Text.Trim() : folderPath.Trim();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            MessageBox.Show(this, "Choose a folder to scan.", "DocReg folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_hasEdits && _drawings.Count > 0)
        {
            var confirm = MessageBox.Show(
                this,
                "Rescanning replaces the current review rows and discards edits. Continue?",
                "Reload review list",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;
        }

        SetBusy(true);
        StatusText.Text = "Scanning...";

        try
        {
            var organization = _organization;
            var registerDocuments = _registerDocuments;
            var projectNumber = ProjectNumberBox.Text;
            var result = await Task.Run(() => DocRegFolderScanner.Scan(
                folderPath,
                organization,
                registerDocuments,
                projectNumber));

            ReplaceRows(result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DocReg folder scan failed for {Folder}", folderPath);
            StatusText.Text = "Scan failed. The review list was not changed.";
            MessageBox.Show(this, $"Could not scan that folder.\n\n{ex.Message}", "DocReg folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ReplaceRows(DocRegScanResult result)
    {
        _applyingScan = true;
        try
        {
            foreach (var drawing in _drawings)
                drawing.PropertyChanged -= Drawing_PropertyChanged;

            _drawings.Clear();
            foreach (var drawing in result.Drawings)
            {
                drawing.PropertyChanged += Drawing_PropertyChanged;
                _drawings.Add(drawing);
            }

            _scannedFolderPath = result.FolderPath;
            FolderPathBox.Text = result.FolderPath;
            ReviewGrid.SelectedIndex = _drawings.Count > 0 ? 0 : -1;
            _hasEdits = false;
            StatusText.Text = result.ExclusionNote
                ?? $"{result.Drawings.Count} PDF(s) ready for review.";
            UpdateCounts();
        }
        finally
        {
            _applyingScan = false;
        }
    }

    private void Drawings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_applyingScan)
            return;
        _hasEdits = true;
        UpdateCounts();
    }

    private void Drawing_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_applyingScan)
            return;
        _hasEdits = true;
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        var included = _drawings.Count(drawing => drawing.Include);
        CountText.Text = $"{included} included / {_drawings.Count} scanned";
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ReviewGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !ReviewGrid.CommitEdit(DataGridEditingUnit.Row, true))
        {
            MessageBox.Show(this, "Correct the current cell before exporting.", "DocReg folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var projectNumber = ProjectNumberBox.Text.Trim();
        var errors = DocRegFolderExport.Validate(_drawings, projectNumber);
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), "DocReg folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var request = DocRegFolderExport.BuildRequest(
            reportDate: DateTime.Today,
            projectNumber: projectNumber,
            projectName: ProjectNameBox.Text.Trim(),
            discipline: DisciplineBox.Text.Trim(),
            registerNumber: _registerNumber,
            clientNumber: _clientNumber,
            organization: _organization,
            drawings: _drawings.ToList());

        var saveDialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = RegisterReportGenerator.GetSuggestedFileName(request)
        };
        if (Directory.Exists(_scannedFolderPath))
            saveDialog.InitialDirectory = _scannedFolderPath;

        if (saveDialog.ShowDialog() != true)
            return;

        if (DocRegFolderExport.WouldOverwriteSourcePdf(saveDialog.FileName, _drawings))
        {
            MessageBox.Show(
                this,
                "That path is one of the source drawing PDFs. Choose a different file name so the original is not overwritten.",
                "DocReg folder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SetBusy(true);
        StatusText.Text = "Exporting...";
        try
        {
            var result = await Task.Run(() => RegisterReportGenerator.Generate(request, saveDialog.FileName));
            var saveMessage = result.ActualFilePath == saveDialog.FileName
                ? $"PDF saved to:\n{result.ActualFilePath}"
                : $"The selected PDF was open in another program, so the report was saved as:\n{result.ActualFilePath}";

            StatusText.Text = saveMessage.Replace("\n", " ");
            MessageBox.Show(this, saveMessage, "DocReg folder", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DocReg folder export failed");
            StatusText.Text = "Export failed.";
            MessageBox.Show(this, $"Could not generate the DocReg PDF.\n\n{ex.Message}", "DocReg folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        BrowseButton.IsEnabled = !isBusy;
        ScanButton.IsEnabled = !isBusy;
        ExportButton.IsEnabled = !isBusy;
        ReviewGrid.IsEnabled = !isBusy;
        ProjectNumberBox.IsEnabled = !isBusy;
        ProjectNameBox.IsEnabled = !isBusy;
        DisciplineBox.IsEnabled = !isBusy;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Header_DragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
