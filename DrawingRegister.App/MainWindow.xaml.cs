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
using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Style = System.Windows.Style;
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
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"DrawingRegister_{DateTime.Now:yyyyMMdd}"
            };

            if (saveDialog.ShowDialog() != true) return;

            // Create the document
            var document = new Document();
            document.DefaultPageSetup.Orientation = MigraDoc.DocumentObjectModel.Orientation.Landscape;
            document.DefaultPageSetup.PageFormat = PageFormat.A4;
            document.DefaultPageSetup.TopMargin = Unit.FromCentimeter(1);
            document.DefaultPageSetup.BottomMargin = Unit.FromCentimeter(1);
            document.DefaultPageSetup.LeftMargin = Unit.FromCentimeter(1);
            document.DefaultPageSetup.RightMargin = Unit.FromCentimeter(1);

            var section = document.AddSection();

            // Add title
            var title = section.AddParagraph("DRAWING REGISTER");
            title.Format.Font.Size = 16;
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = Unit.FromCentimeter(0.5);
            title.Format.Alignment = ParagraphAlignment.Center;

            // Project Info Table
            var infoTable = section.AddTable();
            infoTable.Borders.Width = 0.5;
            infoTable.Format.SpaceAfter = Unit.FromCentimeter(1);
            
            // Add two columns for project info
            infoTable.AddColumn(Unit.FromCentimeter(4));
            infoTable.AddColumn(Unit.FromCentimeter(10));

            // Add project info rows
            AddProjectInfoRow(infoTable, "DISCIPLINE:", DisciplineBox?.Text ?? "");
            AddProjectInfoRow(infoTable, "REG NO:", RegNoBox?.Text ?? "");
            AddProjectInfoRow(infoTable, "PROJECT NO:", ProjectNoBox?.Text ?? "");
            AddProjectInfoRow(infoTable, "CLIENT NO:", ClientNoBox?.Text ?? "");
            AddProjectInfoRow(infoTable, "PROJECT NAME:", ProjectNameBox?.Text ?? "");

            // Main document table
            var table = section.AddTable();
            table.Borders.Width = 0.5;
            table.Format.Font.Size = 9;

            // Define columns with specific widths
            table.AddColumn(Unit.FromCentimeter(4.5));  // Document No
            table.AddColumn(Unit.FromCentimeter(6));    // Description
            table.AddColumn(Unit.FromCentimeter(2));    // Package
            table.AddColumn(Unit.FromCentimeter(2));    // Type
            table.AddColumn(Unit.FromCentimeter(1.5));  // Size
            table.AddColumn(Unit.FromCentimeter(2));    // Latest Rev
            table.AddColumn(Unit.FromCentimeter(2.5));  // Latest Date

            // Get last 3 issue dates
            var lastThreeDates = _project.IssueDates
                .OrderByDescending(d => d)
                .Take(3)
                .OrderBy(d => d)
                .ToList();

            foreach (var date in lastThreeDates)
            {
                table.AddColumn(Unit.FromCentimeter(2)); // Rev columns for last 3 dates
            }

            // Add header row
            var headerRow = table.AddRow();
            headerRow.Shading.Color = MigraDoc.DocumentObjectModel.Colors.LightGray;
            headerRow.Format.Font.Bold = true;
            headerRow.Format.Alignment = ParagraphAlignment.Center;
            
            // Add headers
            int colIndex = 0;
            headerRow.Cells[colIndex++].AddParagraph("DOCUMENT NO");
            headerRow.Cells[colIndex++].AddParagraph("DESCRIPTION");
            headerRow.Cells[colIndex++].AddParagraph("PACKAGE");
            headerRow.Cells[colIndex++].AddParagraph("TYPE");
            headerRow.Cells[colIndex++].AddParagraph("SIZE");
            headerRow.Cells[colIndex++].AddParagraph("LATEST REV");
            headerRow.Cells[colIndex++].AddParagraph("LATEST DATE");

            // Add date headers
            foreach (var date in lastThreeDates)
            {
                headerRow.Cells[colIndex++].AddParagraph(date.ToString("yyyy-MM-dd"));
            }

            // Style all header cells
            foreach (Cell cell in headerRow.Cells)
            {
                cell.Format.Font.Bold = true;
                cell.Format.Alignment = ParagraphAlignment.Center;
                cell.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;
                cell.Format.Font.Color = MigraDoc.DocumentObjectModel.Colors.Black;
            }

            // Add data rows
            if (DocumentGrid?.Items != null)
            {
                foreach (var item in DocumentGrid.Items)
                {
                    if (item is DocumentMetadata doc)
                    {
                        var row = table.AddRow();
                        colIndex = 0;

                        // Standard columns
                        row.Cells[colIndex++].AddParagraph(doc.DocumentNumber);
                        row.Cells[colIndex++].AddParagraph(doc.Description);
                        row.Cells[colIndex++].AddParagraph(doc.Package);
                        row.Cells[colIndex++].AddParagraph(doc.DocumentType);
                        row.Cells[colIndex++].AddParagraph(doc.Size);

                        // Latest revision
                        var latestRev = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                        row.Cells[colIndex++].AddParagraph(latestRev.Value?.Revision ?? "-");
                        row.Cells[colIndex++].AddParagraph(latestRev.Key.ToString("yyyy-MM-dd"));

                        // Historical revisions for last 3 dates
                        foreach (var date in lastThreeDates)
                        {
                            var revision = doc.RevisionHistory.TryGetValue(date, out var revInfo)
                                ? revInfo.Revision
                                : "-";
                            row.Cells[colIndex++].AddParagraph(revision);
                        }

                        // Style all cells in the row
                        foreach (Cell cell in row.Cells)
                        {
                            cell.Format.Alignment = ParagraphAlignment.Center;
                            cell.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;
                            cell.Format.Font.Size = 9;
                        }
                    }
                }
            }

            // Render the document
            var renderer = new PdfDocumentRenderer(true)
            {
                Document = document
            };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(saveDialog.FileName);

            MessageBox.Show("PDF report generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddProjectInfoRow(Table table, string label, string value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label).Format.Font.Bold = true;
        row.Cells[0].Format.Alignment = ParagraphAlignment.Left;
        row.Cells[1].AddParagraph(value);
        row.Cells[1].Format.Alignment = ParagraphAlignment.Left;
        
        // Style the row
        row.Format.Font.Size = 10;
        foreach (Cell cell in row.Cells)
        {
            cell.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;
            cell.Format.SpaceBefore = Unit.FromPoint(2);
            cell.Format.SpaceAfter = Unit.FromPoint(2);
        }
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