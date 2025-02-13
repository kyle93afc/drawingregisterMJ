using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Threading.Tasks;
using DrawingRegister.App.Models;
using System.Collections.Generic;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Binding = System.Windows.Data.Binding;
using System.Windows.Media;
using System.Collections.ObjectModel;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Geom;
using iText.Kernel.Colors;
using Microsoft.Win32;
using Style = System.Windows.Style;
using TextAlignment = iText.Layout.Properties.TextAlignment;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DrawingRegister.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
{
    private readonly ProjectManager _project = new();
    private bool _disposed;
    private string _searchText = string.Empty;
    private DocumentMetadata _selectedDocument = null!;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
            FilterDocuments();
        }
    }

    public DocumentMetadata SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            _selectedDocument = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDocument)));
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Bind UI elements to ProjectManager properties
        ProjectNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ProjectNumber)) { Source = _project });
        ProjectNameBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ProjectName)) { Source = _project });
        DisciplineBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.Discipline)) { Source = _project });
        RegNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.RegisterNumber)) { Source = _project });
        ClientNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ClientNumber)) { Source = _project });

        // Bind grid to ProjectManager collections
        DocumentGrid.ItemsSource = _project.Documents;

        // Initialize search type combo
        SearchTypeCombo.SelectedIndex = 0;

        // Subscribe to project's documents collection changes
        _project.Documents.CollectionChanged += Documents_CollectionChanged;
    }

    private void Documents_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Update issue date filter options when documents change
        UpdateIssueDateFilterOptions();
        UpdateRevisionColumns();
    }

    private void UpdateIssueDateFilterOptions()
    {
        var currentSelection = IssueDateFilter.SelectedItem;
        IssueDateFilter.Items.Clear();
        IssueDateFilter.Items.Add(new ComboBoxItem { Content = "All Dates" });

        // Get all unique issue dates from documents
        var issueDates = _project.Documents
            .SelectMany(d => d.RevisionHistory.Keys)
            .OrderByDescending(d => d)
            .Select(d => d.ToString("yyyy-MM-dd"))
            .Distinct()
            .Select(d => new ComboBoxItem { Content = d });

        foreach (var date in issueDates)
        {
            IssueDateFilter.Items.Add(date);
        }

        // Restore previous selection or default to "All Dates"
        if (currentSelection != null && IssueDateFilter.Items.Cast<ComboBoxItem>().Any(i => i.Content.Equals(((ComboBoxItem)currentSelection).Content)))
        {
            IssueDateFilter.SelectedItem = IssueDateFilter.Items.Cast<ComboBoxItem>().First(i => i.Content.Equals(((ComboBoxItem)currentSelection).Content));
        }
        else
        {
            IssueDateFilter.SelectedIndex = 0;
        }
    }

    private void UpdateRevisionColumns()
    {
        // Get all unique issue dates from documents
        var issueDates = _project.Documents
            .SelectMany(d => d.RevisionHistory.Keys)
            .OrderByDescending(d => d)  // Sort newest to oldest
            .Distinct()
            .ToList();

        // Remove any existing revision date columns
        var existingRevisionColumns = DocumentGrid.Columns
            .Where(c => c.Header.ToString()?.Contains("-") == true)  // Changed to match date format
            .ToList();
        foreach (var column in existingRevisionColumns)
        {
            DocumentGrid.Columns.Remove(column);
        }

        // Add a column for each issue date
        foreach (var date in issueDates)
        {
            var column = new System.Windows.Controls.DataGridTextColumn
            {
                Header = date.ToString("yyyy-MM-dd"),
                Width = DataGridLength.Auto,
                MinWidth = 40,
                HeaderStyle = (Style)FindResource("RotatedColumnHeader"),
                Binding = new System.Windows.Data.Binding("RevisionHistory")
                {
                    Converter = (System.Windows.Data.IValueConverter)FindResource("RevisionAtDateConverter"),
                    ConverterParameter = date
                }
            };
            DocumentGrid.Columns.Add(column);
        }
    }

    private void IssueDateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IssueDateFilter.SelectedItem is not ComboBoxItem selectedItem)
            return;

        var selectedContent = selectedItem.Content.ToString();
        
        if (selectedContent == "All Dates")
        {
            DocumentGrid.ItemsSource = _project.Documents;
            return;
        }

        if (DateTime.TryParse(selectedContent, out var selectedDate))
        {
            DocumentGrid.ItemsSource = _project.Documents
                .Where(d => d.RevisionHistory.Any(r => r.Key.Date == selectedDate.Date))
                .OrderBy(d => d.DocumentNumber)
                .ToList();
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _project.SaveProjectData();
            MessageBox.Show("Project saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterDocuments();
    }

    private void FilterDocuments()
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(DocumentGrid.ItemsSource);
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            view.Filter = null;
            return;
        }

        string searchText = SearchBox.Text.ToLower();
        var searchType = ((ComboBoxItem)SearchTypeCombo.SelectedItem).Content.ToString();

        view.Filter = obj =>
        {
            if (obj is not DocumentMetadata doc) return false;

            return searchType switch
            {
                "Document No" => doc.DocumentNumber.ToLower().Contains(searchText),
                "Description" => doc.Description.ToLower().Contains(searchText),
                "Package" => doc.Package.ToLower().Contains(searchText),
                "Type" => doc.DocumentType.ToLower().Contains(searchText),
                _ => false
            };
        };
    }

    private void ImportDocuments_Click(object sender, RoutedEventArgs e)
    {
        var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select the project folder containing PDFs to scan",
            UseDescriptionForTitle = true
        };

        if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                _project.ImportDocuments(folderBrowserDialog.SelectedPath);
                MessageBox.Show($"Successfully imported {_project.Documents.Count} documents.", 
                    "Import Complete", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing documents: {ex.Message}", 
                    "Import Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
    }

    private void RefreshView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_project._currentBasePath))
            {
                MessageBox.Show("No folder selected. Please import documents first.", 
                    "Refresh Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            // Rescan the current folder
            _project.ImportDocuments(_project._currentBasePath);

            // Refresh the document grid view
            var view = CollectionViewSource.GetDefaultView(DocumentGrid.ItemsSource);
            view.Refresh();

            // Reapply any active filters
            FilterDocuments();

            // Force grid to update
            DocumentGrid.Items.Refresh();

            MessageBox.Show($"Successfully refreshed {_project.Documents.Count} documents.", 
                "Refresh Complete", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing documents: {ex.Message}", 
                "Refresh Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    private void DocumentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedDocument = DocumentGrid.SelectedItem as DocumentMetadata;
    }

    private void DocumentGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DocumentGrid.SelectedItem is DocumentMetadata selectedDoc)
        {
            OpenDocument(selectedDoc.FilePath);
        }
    }

    private void RevisionTimeline_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is KeyValuePair<DateTime, RevisionInfo> revision)
        {
            OpenDocument(revision.Value.FilePath);
        }
    }

    private void OpenDocument(string filePath)
    {
        try 
        {
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DateFilter_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Please select both start and end dates");
                return;
            }

            var startDate = StartDatePicker.SelectedDate.Value.Date;
            var endDate = EndDatePicker.SelectedDate.Value.Date;
            
            if (startDate > endDate)
            {
                MessageBox.Show("Start date cannot be after end date");
                return;
            }

            DocumentGrid.ItemsSource = _project.Documents
                .Where(d => d.RevisionHistory.Any(r => 
                    r.Key.Date >= startDate && 
                    r.Key.Date <= endDate))
                .OrderBy(d => d.DocumentNumber)
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Filter error: {ex.Message}");
        }
    }

    private void ClearDateFilter_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = null;
        EndDatePicker.SelectedDate = null;
        DocumentGrid.ItemsSource = _project.Documents;
    }

    private void GeneratePdfReport_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = $"DrawingRegister_{DateTime.Now:yyyyMMdd}"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            using var writer = new PdfWriter(saveDialog.FileName);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());
            
            document.SetMargins(20, 20, 20, 20);
            
            // Add project info
            var projectInfo = new Table(UnitValue.CreatePercentArray(2))
                .UseAllAvailableWidth()
                .SetMarginBottom(20);

            AddProjectInfoRow(projectInfo, "DISCIPLINE:", DisciplineBox.Text);
            AddProjectInfoRow(projectInfo, "REG NO:", RegNoBox.Text);
            AddProjectInfoRow(projectInfo, "PROJECT NO:", ProjectNoBox.Text);
            AddProjectInfoRow(projectInfo, "CLIENT NO:", ClientNoBox.Text);
            AddProjectInfoRow(projectInfo, "PROJECT NAME:", ProjectNameBox.Text);

            document.Add(projectInfo);

            // Create table
            var columnCount = DocumentGrid.Columns.Count;
            var table = new Table(UnitValue.CreatePercentArray(columnCount))
                .UseAllAvailableWidth()
                .SetMarginBottom(20)
                .SetFontSize(9);

            // Add headers
            foreach (var column in DocumentGrid.Columns)
            {
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(column.Header.ToString() ?? ""))
                    .SetBackgroundColor(new DeviceRgb(235, 24, 69))
                    .SetFontColor(ColorConstants.WHITE)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetPadding(5));
            }

            // Add data rows
            foreach (var item in DocumentGrid.Items)
            {
                if (item is DocumentMetadata doc)
                {
                    // Standard columns
                    table.AddCell(new Cell().Add(new Paragraph(doc.DocumentNumber)));
                    table.AddCell(new Cell().Add(new Paragraph(doc.Description)));
                    table.AddCell(new Cell().Add(new Paragraph(doc.Package)));
                    table.AddCell(new Cell().Add(new Paragraph(doc.DocumentType)));
                    table.AddCell(new Cell().Add(new Paragraph(doc.Size)));

                    // Latest revision
                    var latestRev = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                    table.AddCell(new Cell().Add(new Paragraph(latestRev.Value?.Revision ?? "")));
                    table.AddCell(new Cell().Add(new Paragraph(latestRev.Key.ToString("yyyy-MM-dd"))));

                    // Dynamic date columns
                    foreach (var column in DocumentGrid.Columns.Skip(7))
                    {
                        if (column.Header?.ToString() is string dateStr &&
                            DateTime.TryParse(dateStr, out var date))
                        {
                            var revision = doc.RevisionHistory.TryGetValue(date, out var revInfo) 
                                ? revInfo.Revision 
                                : "";
                            table.AddCell(new Cell().Add(new Paragraph(revision)));
                        }
                    }
                }
            }

            document.Add(table);
            MessageBox.Show("PDF report generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddProjectInfoRow(Table table, string label, string value)
    {
        table.AddCell(new Cell().Add(new Paragraph(label)).SetBold().SetPadding(5));
        table.AddCell(new Cell().Add(new Paragraph(value)).SetPadding(5));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Dispose();
    }
}