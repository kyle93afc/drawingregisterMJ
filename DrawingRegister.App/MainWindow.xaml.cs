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
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Style = System.Windows.Style;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using IContainer = QuestPDF.Infrastructure.IContainer;
using Colors = QuestPDF.Helpers.Colors;

namespace DrawingRegister.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
{
    private readonly ProjectManager _project = new();
    private bool _disposed;
    private string _searchText = string.Empty;
    private Models.DocumentMetadata? _selectedDocument;

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

    public Models.DocumentMetadata? SelectedDocument
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
        PurposeOfIssueFilter.SelectedIndex = 0;
        MethodOfIssueFilter.SelectedIndex = 0;

        // Subscribe to project's documents collection changes
        _project.Documents.CollectionChanged += Documents_CollectionChanged;

        // Add event handlers for new filters
        PurposeOfIssueFilter.SelectionChanged += (s, e) => FilterDocuments();
        MethodOfIssueFilter.SelectionChanged += (s, e) => FilterDocuments();
        IssuedByFilter.TextChanged += (s, e) => FilterDocuments();
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
            .Distinct()  // Remove duplicates before sorting
            .OrderByDescending(d => d)  // Sort newest to oldest
            .Select(d => new ComboBoxItem { Content = d.ToString("dd/MM/yyyy") });

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
            .Distinct()  // Remove duplicates before sorting
            .OrderByDescending(d => d)  // Sort newest to oldest
            .ToList();

        // Remove any existing revision date columns
        var existingRevisionColumns = DocumentGrid.Columns
            .Where(c => c.Header.ToString()?.Contains("/") == true)  // Changed to match new date format
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
                Header = date.ToString("dd/MM/yyyy"),
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

        // Parse date using the new format
        if (DateTime.TryParseExact(selectedContent, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var selectedDate))
        {
            // Get all documents for this date
            var docsForDate = _project.Documents
                .Where(d => d.RevisionHistory.Any(r => r.Key.Date == selectedDate.Date))
                .OrderBy(d => d.DocumentNumber)
                .ToList();

            DocumentGrid.ItemsSource = docsForDate;

            // Try to detect purpose, method, and issuer from the documents
            var revisions = docsForDate
                .SelectMany(d => d.RevisionHistory)
                .Where(r => r.Key.Date == selectedDate.Date)
                .Select(r => r.Value)
                .ToList();

            if (revisions.Any())
            {
                // Get the most common purpose
                var commonPurpose = revisions
                    .GroupBy(r => r.Purpose)
                    .OrderByDescending(g => g.Count())
                    .First().Key;

                // Get the most common method
                var commonMethod = revisions
                    .GroupBy(r => r.Method)
                    .OrderByDescending(g => g.Count())
                    .First().Key;

                // Get the most common issuer
                var commonIssuer = revisions
                    .GroupBy(r => r.IssuedBy)
                    .OrderByDescending(g => g.Count())
                    .First().Key;

                // Set the values in the UI
                foreach (ComboBoxItem item in PurposeOfIssueFilter.Items)
                {
                    if (item.Content.ToString() == commonPurpose)
                    {
                        PurposeOfIssueFilter.SelectedItem = item;
                        break;
                    }
                }

                foreach (ComboBoxItem item in MethodOfIssueFilter.Items)
                {
                    if (item.Content.ToString() == commonMethod)
                    {
                        MethodOfIssueFilter.SelectedItem = item;
                        break;
                    }
                }

                IssuedByFilter.Text = commonIssuer;
            }
            else
            {
                // Reset the filters if no data found
                PurposeOfIssueFilter.SelectedIndex = 0;
                MethodOfIssueFilter.SelectedIndex = 0;
                IssuedByFilter.Text = string.Empty;
            }
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
        if (string.IsNullOrWhiteSpace(SearchBox.Text) && 
            PurposeOfIssueFilter.SelectedIndex <= 0 && 
            MethodOfIssueFilter.SelectedIndex <= 0 && 
            string.IsNullOrWhiteSpace(IssuedByFilter.Text))
        {
            view.Filter = null;
            return;
        }

        string searchText = SearchBox.Text.ToLower();
        var searchType = ((ComboBoxItem)SearchTypeCombo.SelectedItem).Content.ToString();
        var purposeOfIssue = PurposeOfIssueFilter.SelectedIndex > 0 ? ((ComboBoxItem)PurposeOfIssueFilter.SelectedItem).Content.ToString() : null;
        var methodOfIssue = MethodOfIssueFilter.SelectedIndex > 0 ? ((ComboBoxItem)MethodOfIssueFilter.SelectedItem).Content.ToString() : null;
        var issuedBy = IssuedByFilter.Text.Trim().ToUpper();

        view.Filter = obj =>
        {
            if (obj is not Models.DocumentMetadata doc) return false;

            bool matchesSearch = string.IsNullOrWhiteSpace(searchText) || searchType switch
            {
                "Document No" => doc.DocumentNumber.ToLower().Contains(searchText),
                "Description" => doc.Description.ToLower().Contains(searchText),
                "Package" => doc.Package.ToLower().Contains(searchText),
                "Type" => doc.DocumentType.ToLower().Contains(searchText),
                _ => false
            };

            bool matchesPurpose = purposeOfIssue == null || doc.PurposeOfIssue == purposeOfIssue;
            bool matchesMethod = methodOfIssue == null || doc.MethodOfIssue == methodOfIssue;
            bool matchesIssuedBy = string.IsNullOrWhiteSpace(issuedBy) || doc.IssuedBy == issuedBy;

            return matchesSearch && matchesPurpose && matchesMethod && matchesIssuedBy;
        };
    }

    private void ImportDocuments_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the project folder containing PDFs to scan"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _project.ImportDocuments(dialog.FolderName);
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
        if (DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDoc)
        {
            SelectedDocument = selectedDoc;
        }
    }

    private void DocumentGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDoc)
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

    private void EditRevision_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && 
            button.DataContext is KeyValuePair<DateTime, RevisionInfo> revision &&
            SelectedDocument != null)
        {
            var dialog = new RevisionEditDialog(SelectedDocument, revision.Key, revision.Value);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Update the revision info
                revision.Value.Purpose = dialog.Purpose;
                revision.Value.Method = dialog.Method;
                revision.Value.IssuedBy = dialog.IssuedBy;
                revision.Value.Revision = dialog.Revision;

                // Update the document's current values
                SelectedDocument.PurposeOfIssue = dialog.Purpose;
                SelectedDocument.MethodOfIssue = dialog.Method;
                SelectedDocument.IssuedBy = dialog.IssuedBy;
                SelectedDocument.Revision = dialog.Revision;

                // Save changes
                _project.SaveProjectData();

                // Refresh views
                DocumentGrid.Items.Refresh();
                RevisionTimeline.Items.Refresh();
            }
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

    private void SaveIssueDetails_Click(object sender, RoutedEventArgs e)
    {
        // Get the selected date
        if (IssueDateFilter.SelectedItem is not ComboBoxItem selectedItem)
        {
            MessageBox.Show("Please select a date of issue first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedContent = selectedItem.Content.ToString();
        if (selectedContent == "All Dates")
        {
            MessageBox.Show("Please select a specific date of issue.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Get the selected values
        var purpose = PurposeOfIssueFilter.SelectedIndex > 0 
            ? ((ComboBoxItem)PurposeOfIssueFilter.SelectedItem).Content.ToString()
            : null;
        var method = MethodOfIssueFilter.SelectedIndex > 0 
            ? ((ComboBoxItem)MethodOfIssueFilter.SelectedItem).Content.ToString()
            : null;
        var issuedBy = IssuedByFilter.Text.Trim().ToUpper();

        if (string.IsNullOrEmpty(purpose))
        {
            MessageBox.Show("Please select a purpose of issue.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(method))
        {
            MessageBox.Show("Please select a method of issue.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(issuedBy))
        {
            MessageBox.Show("Please enter initials for issued by.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DateTime.TryParseExact(selectedContent, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var selectedDate))
        {
            // Check if there are multiple purposes for this date
            var existingPurposes = _project.Documents
                .SelectMany(d => d.RevisionHistory)
                .Where(r => r.Key.Date == selectedDate.Date)
                .Select(r => r.Value.Purpose)
                .Distinct()
                .ToList();

            if (existingPurposes.Count > 1)
            {
                var result = MessageBox.Show(
                    $"This date ({selectedContent}) has multiple purposes:\n{string.Join(", ", existingPurposes)}\n\nDo you want to update all documents to use '{purpose}'?",
                    "Multiple Purposes Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // Update all documents that have a revision on this date
            var updatedCount = 0;
            foreach (var doc in _project.Documents)
            {
                if (doc.RevisionHistory.TryGetValue(selectedDate, out var revInfo))
                {
                    revInfo.Purpose = purpose;
                    revInfo.Method = method;
                    revInfo.IssuedBy = issuedBy;
                    revInfo.Revision = DrawingRegister.App.Models.DocumentMetadata.GenerateRevisionCode(purpose, doc.RevisionHistory);

                    // Update document's current values if this is the latest revision
                    if (doc.RevisionHistory.Max(r => r.Key) == selectedDate)
                    {
                        doc.PurposeOfIssue = purpose;
                        doc.MethodOfIssue = method;
                        doc.IssuedBy = issuedBy;
                        doc.Revision = revInfo.Revision;
                    }
                    updatedCount++;
                }
            }

            // Save changes
            _project.SaveProjectData();

            // Refresh views
            DocumentGrid.Items.Refresh();
            RevisionTimeline.Items.Refresh();

            MessageBox.Show($"Successfully updated {updatedCount} documents.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
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

            // Initialize QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;

            // Create and generate the document
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                });
            })
            .GeneratePdf(saveDialog.FileName);

            // Open the generated PDF
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = saveDialog.FileName,
                UseShellExecute = true
            });

            MessageBox.Show("PDF report generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            // Title row with background
            column.Item().Background("#eb1845").Padding(10).Row(row =>
            {
                row.RelativeItem().AlignCenter().Text("DRAWING REGISTER")
                    .FontSize(20)
                    .FontColor(Colors.White)
                    .Bold();
            });

            // Project Info Table with styling
            column.Item().Border(1).BorderColor("#cccccc").Padding(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                });

                // Row 1
                AddProjectInfoCell(table, "DISCIPLINE:", DisciplineBox?.Text ?? "");
                AddProjectInfoCell(table, "CLIENT NO:", ClientNoBox?.Text ?? "");

                // Row 2
                AddProjectInfoCell(table, "REG NO:", RegNoBox?.Text ?? "");
                AddProjectInfoCell(table, "PROJECT NAME:", ProjectNameBox?.Text ?? "");

                // Row 3
                AddProjectInfoCell(table, "PROJECT NO:", ProjectNoBox?.Text ?? "");
                table.Cell().ColumnSpan(2); // Empty cells for alignment
            });

            // Add some spacing
            column.Item().Height(10);
        });
    }

    private void ComposeContent(IContainer container)
    {
        if (DocumentGrid?.Items == null) return;

        // Get last 3 issue dates in descending order (newest first)
        var lastThreeDates = _project.IssueDates
            .OrderByDescending(d => d)
            .Take(3)
            .ToList(); // Removed the OrderBy to keep descending order

        container.Table(table =>
        {
            // Define columns with better proportions
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.5f);    // Document No
                columns.RelativeColumn(3);       // Description
                columns.RelativeColumn(1);       // Package
                columns.RelativeColumn(0.8f);    // Type
                columns.RelativeColumn(0.6f);    // Size
                columns.RelativeColumn(0.8f);    // Latest Rev
                columns.RelativeColumn(1.2f);    // Latest Date
                
                // Add columns for last 3 dates (newest first)
                foreach (var _ in lastThreeDates)
                {
                    columns.RelativeColumn(1);
                }
            });

            // Add header row with styling
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("DOCUMENT NO");
                header.Cell().Element(HeaderCell).Text("DESCRIPTION");
                header.Cell().Element(HeaderCell).Text("PACKAGE");
                header.Cell().Element(HeaderCell).Text("TYPE");
                header.Cell().Element(HeaderCell).Text("SIZE");
                header.Cell().Element(HeaderCell).Text("LATEST REV");
                header.Cell().Element(HeaderCell).Text("LATEST DATE");

                // Add date headers in descending order
                foreach (var date in lastThreeDates)
                {
                    header.Cell().Element(HeaderCell).Text(date.ToString("yyyy-MM-dd"));
                }
            });

            // Add data rows with alternating background
            bool isAlternate = false;
            foreach (var item in DocumentGrid.Items)
            {
                if (item is not Models.DocumentMetadata doc) continue;

                var latestRev = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                var rowColor = isAlternate ? "#f5f5f5" : "#ffffff";

                table.Cell().Element(cell => DataCell(cell, doc.DocumentNumber, rowColor));
                table.Cell().Element(cell => DataCell(cell, doc.Description, rowColor));
                table.Cell().Element(cell => DataCell(cell, doc.Package, rowColor));
                table.Cell().Element(cell => DataCell(cell, doc.DocumentType, rowColor));
                table.Cell().Element(cell => DataCell(cell, doc.Size, rowColor));
                table.Cell().Element(cell => DataCell(cell, latestRev.Value?.Revision ?? "-", rowColor));
                table.Cell().Element(cell => DataCell(cell, latestRev.Key.ToString("yyyy-MM-dd"), rowColor));

                // Add historical revisions in descending order
                foreach (var date in lastThreeDates)
                {
                    var revision = doc.RevisionHistory.TryGetValue(date, out var revInfo)
                        ? revInfo.Revision
                        : "-";
                    table.Cell().Element(cell => DataCell(cell, revision, rowColor));
                }

                isAlternate = !isAlternate;
            }
        });
    }

    private IContainer HeaderCell(IContainer container)
    {
        return container.Background("#eb1845")
            .Padding(5)
            .Border(1)
            .BorderColor("#d10835")
            .AlignCenter()
            .DefaultTextStyle(x => x.Bold().FontColor(Colors.White));
    }

    private void DataCell(IContainer container, string text, string backgroundColor)
    {
        container.Border(1)
            .BorderColor("#cccccc")
            .Background(backgroundColor)
            .Padding(5)
            .AlignCenter()
            .Text(text);
    }

    private void AddProjectInfoCell(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(5).AlignLeft().Text(label).Bold();
        table.Cell().Padding(5).AlignLeft().Text(value);
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