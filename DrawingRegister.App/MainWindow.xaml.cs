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
using QuestPDF.Previewer;
using Style = System.Windows.Style;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using IContainer = QuestPDF.Infrastructure.IContainer;
using Colors = QuestPDF.Helpers.Colors;
using QuestPDF.Elements.Table;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Windows.Input;

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
        
        // Add keyboard shortcut for editing documents
        DocumentGrid.KeyDown += DocumentGrid_KeyDown;
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
        const double REVISION_COLUMN_WIDTH = 40; // Fixed width for revision columns

        // Get all unique issue dates from documents
        var issueDates = _project.Documents
            .SelectMany(d => d.RevisionHistory.Keys)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        // Remove any existing revision date columns
        var existingRevisionColumns = DocumentGrid.Columns
            .Where(c => c.Header.ToString()?.Contains("/") == true)
            .ToList();
        foreach (var column in existingRevisionColumns)
        {
            DocumentGrid.Columns.Remove(column);
        }

        // Add a column for each issue date
        foreach (var date in issueDates)
        {
            var column = new DataGridTextColumn
            {
                Header = date.ToString("dd/MM/yyyy"),
                Width = new DataGridLength(REVISION_COLUMN_WIDTH),
                HeaderStyle = (Style)FindResource("RotatedColumnHeader"),
                Binding = new Binding("RevisionHistory")
                {
                    Converter = (IValueConverter)FindResource("RevisionAtDateConverter"),
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
            
            // Hide distribution summary and disable view button
            DistributionSummaryBorder.Visibility = Visibility.Collapsed;
            ViewDistributionButton.IsEnabled = false;
            
            // Reset the distribution information display
            DistributionInfoText.Text = "No recipients selected";
            return;
        }

        // Enable the view distribution button when a specific date is selected
        ViewDistributionButton.IsEnabled = true;

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
                    if (item.Content.ToString().Contains(commonPurpose.Substring(0, 1)))
                    {
                        PurposeOfIssueFilter.SelectedItem = item;
                        break;
                    }
                }

                foreach (ComboBoxItem item in MethodOfIssueFilter.Items)
                {
                    if (item.Content.ToString().Contains(commonMethod.Substring(0, 1)))
                    {
                        MethodOfIssueFilter.SelectedItem = item;
                        break;
                    }
                }

                IssuedByFilter.Text = commonIssuer;
                
                // Update the visual indicators
                UpdateIssueIndicators(commonPurpose, commonMethod);
            }
            else
            {
                // Reset the filters if no data found
                PurposeOfIssueFilter.SelectedIndex = 0;
                MethodOfIssueFilter.SelectedIndex = 0;
                IssuedByFilter.Text = string.Empty;
                
                // Clear the visual indicators
                UpdateIssueIndicators(string.Empty, string.Empty);
            }
            
            // Show distribution summary
            var distributionSummary = DistributionSummary.GenerateForDate(_project, selectedDate);
            DistributionSummaryText.Text = distributionSummary.GetFormattedSummary();
            DistributionSummaryBorder.Visibility = distributionSummary.TotalRecipients > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // Update the distribution information display in the Issue Information section
            UpdateDistributionInfoDisplay(selectedDate);
        }
    }

    private void UpdateIssueIndicators(string purpose, string method)
    {
        // Get current values if null is passed (to preserve existing values)
        string currentPurpose = purpose ?? CurrentPurposeIndicator.Text;
        string currentMethod = method ?? CurrentMethodIndicator.Text;
        
        // Reset all purpose indicators
        ResetIndicator(PurposeA);
        ResetIndicator(PurposeC);
        ResetIndicator(PurposeI);
        ResetIndicator(PurposeT);
        
        // Reset all method indicators
        ResetIndicator(MethodE);
        ResetIndicator(MethodS);
        ResetIndicator(MethodP);
        ResetIndicator(MethodH);
        
        // Update the single-digit indicators
        if (purpose != null)
            CurrentPurposeIndicator.Text = !string.IsNullOrEmpty(purpose) && purpose.Length > 0 ? purpose.Substring(0, 1) : "";
        
        if (method != null)
            CurrentMethodIndicator.Text = !string.IsNullOrEmpty(method) && method.Length > 0 ? method.Substring(0, 1) : "";
        
        CurrentIssuedByIndicator.Text = IssuedByFilter.Text.Length > 0 ? IssuedByFilter.Text.Substring(0, Math.Min(2, IssuedByFilter.Text.Length)) : "";
        
        // Highlight the selected purpose
        if (!string.IsNullOrEmpty(currentPurpose))
        {
            if (currentPurpose.StartsWith("A"))
                HighlightIndicator(PurposeA);
            else if (currentPurpose.StartsWith("C"))
                HighlightIndicator(PurposeC);
            else if (currentPurpose.StartsWith("I"))
                HighlightIndicator(PurposeI);
            else if (currentPurpose.StartsWith("T"))
                HighlightIndicator(PurposeT);
        }
        
        // Highlight the selected method
        if (!string.IsNullOrEmpty(currentMethod))
        {
            if (currentMethod.StartsWith("E"))
                HighlightIndicator(MethodE);
            else if (currentMethod.StartsWith("S"))
                HighlightIndicator(MethodS);
            else if (currentMethod.StartsWith("P"))
                HighlightIndicator(MethodP);
            else if (currentMethod.StartsWith("H"))
                HighlightIndicator(MethodH);
        }
    }
    
    private void ResetIndicator(Border indicator)
    {
        indicator.Background = System.Windows.Media.Brushes.Transparent;
        indicator.BorderBrush = System.Windows.Media.Brushes.Gray;
        var textBlock = indicator.Child as TextBlock;
        if (textBlock != null)
            textBlock.Foreground = System.Windows.Media.Brushes.Gray;
    }
    
    private void HighlightIndicator(Border indicator)
    {
        indicator.Background = System.Windows.Media.Brushes.LightBlue;
        indicator.BorderBrush = System.Windows.Media.Brushes.Blue;
        var textBlock = indicator.Child as TextBlock;
        if (textBlock != null)
            textBlock.Foreground = System.Windows.Media.Brushes.Blue;
    }
    
    private void PurposeOfIssueFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterDocuments();
        
        if (PurposeOfIssueFilter.SelectedItem is ComboBoxItem selectedItem)
        {
            string purpose = selectedItem.Content.ToString();
            if (purpose != "All")
            {
                // Extract the first character for the indicator
                string purposeChar = purpose.Substring(0, 1);
                CurrentPurposeIndicator.Text = purposeChar;
                
                // Update the visual indicators
                UpdateIssueIndicators(purpose, null);
            }
            else
            {
                // Clear the purpose indicator
                CurrentPurposeIndicator.Text = "";
                UpdateIssueIndicators("", null);
            }
        }
    }
    
    private void MethodOfIssueFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterDocuments();
        
        if (MethodOfIssueFilter.SelectedItem is ComboBoxItem selectedItem)
        {
            string method = selectedItem.Content.ToString();
            if (method != "All")
            {
                // Extract the first character for the indicator
                string methodChar = method.Substring(0, 1);
                CurrentMethodIndicator.Text = methodChar;
                
                // Update the visual indicators
                UpdateIssueIndicators(null, method);
            }
            else
            {
                // Clear the method indicator
                CurrentMethodIndicator.Text = "";
                UpdateIssueIndicators(null, "");
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

    private void SearchTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterDocuments();
    }

    private void FilterDocuments()
    {
        // Get the current filters
        string purposeFilter = "All";
        string methodFilter = "All";
        
        if (PurposeOfIssueFilter.SelectedItem is ComboBoxItem purposeItem)
            purposeFilter = purposeItem.Content.ToString();
            
        if (MethodOfIssueFilter.SelectedItem is ComboBoxItem methodItem)
            methodFilter = methodItem.Content.ToString();
            
        string issuedByFilter = IssuedByFilter.Text.Trim();
        string searchText = SearchBox.Text.Trim();
        
        // Start with all documents
        var filteredDocs = _project.Documents.ToList();
        
        // Apply search text filter if not empty
        if (!string.IsNullOrEmpty(searchText))
        {
            string searchType = "Document No";
            if (SearchTypeCombo.SelectedItem is ComboBoxItem selectedItem)
                searchType = selectedItem.Content.ToString();
                
            switch (searchType)
            {
                case "Document No":
                    filteredDocs = filteredDocs
                        .Where(d => d.DocumentNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    break;
                case "Description":
                    filteredDocs = filteredDocs
                        .Where(d => d.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    break;
                case "Package":
                    filteredDocs = filteredDocs
                        .Where(d => d.Package.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    break;
                case "Type":
                    filteredDocs = filteredDocs
                        .Where(d => d.DocumentType.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    break;
            }
        }
        
        // Apply purpose filter
        if (purposeFilter != "All")
        {
            string purpose = purposeFilter.Split('-')[0].Trim();
            filteredDocs = filteredDocs
                .Where(d => d.RevisionHistory.Any(r => r.Value.Purpose.StartsWith(purpose)))
                .ToList();
        }
        
        // Apply method filter
        if (methodFilter != "All")
        {
            string method = methodFilter.Split('-')[0].Trim();
            filteredDocs = filteredDocs
                .Where(d => d.RevisionHistory.Any(r => r.Value.Method.StartsWith(method)))
                .ToList();
        }
        
        // Apply issued by filter
        if (!string.IsNullOrEmpty(issuedByFilter))
        {
            filteredDocs = filteredDocs
                .Where(d => d.RevisionHistory.Any(r => 
                    r.Value.IssuedBy.Contains(issuedByFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        
        // Update the grid
        DocumentGrid.ItemsSource = filteredDocs.OrderBy(d => d.DocumentNumber).ToList();
    }

    private void ImportDocuments_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select PDF folder to scan",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                // Open the progress window
                var progressWindow = new FolderProgressWindow();
                progressWindow.Show();

                // Wire up the folder status callback
                _project.OnFolderStatusUpdated = (folderName, status) =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var existing = progressWindow.FolderStatuses.FirstOrDefault(fs => fs.FolderName == folderName);
                        if (existing != null)
                            existing.Status = status;
                        else
                            progressWindow.FolderStatuses.Add(new FolderStatusViewModel(folderName, status));
                    });
                };

                _project.ImportDocuments(dialog.SelectedPath);
                UpdateIssueDateFilterOptions();
                UpdateRevisionColumns();
                FilterDocuments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing documents: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RefreshView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Clear the selected document to return to full view
            SelectedDocument = null;
            DocumentGrid.SelectedItem = null;

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

    private void DocumentGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // If clicked on empty space (not on a row)
        if (e.OriginalSource is System.Windows.Controls.DataGridRow) return;
        
        // Clear selection and selected document
        DocumentGrid.SelectedItem = null;
        SelectedDocument = null;
    }

    private void DocumentGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDoc)
        {
            // Check if Ctrl key is pressed for editing instead of opening
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                EditDocument(selectedDoc);
            }
            else
            {
                OpenDocument(selectedDoc.FilePath);
            }
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

    private void EditDocument_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDoc)
        {
            EditDocument(selectedDoc);
        }
    }

    private void EditDocument(Models.DocumentMetadata document)
    {
        var dialog = new DocumentEditDialog(document);
        dialog.Owner = this;

        if (dialog.ShowDialog() == true)
        {
            // Store original values for file renaming
            string originalDocNumber = document.DocumentNumber;
            string originalDescription = document.Description;
            string originalFilePath = document.FilePath;

            // Update document metadata
            document.DocumentNumber = dialog.DocumentNumber;
            document.Description = dialog.Description;
            document.Package = dialog.Package;
            document.DocumentType = dialog.DocumentType;
            document.Size = dialog.Size;

            // Rename the file if requested
            if (dialog.UpdateFile && 
                (originalDocNumber != document.DocumentNumber || originalDescription != document.Description))
            {
                // Attempt to rename the file
                bool success = Helpers.FileOperations.RenameDocumentFile(
                    document, 
                    document.DocumentNumber, 
                    document.Description);

                if (!success)
                {
                    // If file rename failed, revert metadata changes
                    document.DocumentNumber = originalDocNumber;
                    document.Description = originalDescription;
                    document.FilePath = originalFilePath;
                    
                    MessageBox.Show(
                        "Failed to rename the file. The document metadata has not been updated.",
                        "File Rename Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    return;
                }
            }

            // Save changes to project data
            _project.SaveProjectData();

            // Refresh views
            DocumentGrid.Items.Refresh();
            RevisionTimeline.Items.Refresh();
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
            // Update all documents that have a revision on this date
            var updatedCount = 0;
            foreach (var doc in _project.Documents)
            {
                var matchingRevisions = doc.RevisionHistory
                    .Where(r => r.Key.Date == selectedDate.Date)
                    .ToList();

                foreach (var revision in matchingRevisions)
                {
                    var revInfo = revision.Value;
                    // Only update the IssuedBy field
                    revInfo.IssuedBy = issuedBy;

                    // Update document's current values if this is the latest revision
                    if (doc.RevisionHistory.Max(r => r.Key) == revision.Key)
                    {
                        doc.IssuedBy = issuedBy;
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
            // Initialize QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;
            
            // Enable QuestPDF debugging to get more detailed error information
            QuestPDF.Settings.EnableDebugging = true;

            // Determine if we're generating a transmittal (specific date) or full register
            bool isTransmittal = IssueDateFilter.SelectedItem is ComboBoxItem selectedItem && 
                                selectedItem.Content.ToString() != "All Dates";
            
            string fileNamePrefix = isTransmittal 
                ? $"Transmittal_{_project.RegisterNumber}_{DateTime.Now:yyyyMMdd}" 
                : $"Register_{_project.RegisterNumber}_{DateTime.Now:yyyyMMdd}";

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = fileNamePrefix
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // Create and save the document
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(1, Unit.Centimetre);
                            page.DefaultTextStyle(x => x.FontSize(9));

                            // Use a single Element call for each section
                            page.Header().Element(header => ComposeHeader(header, isTransmittal));
                            page.Content().Element(content => ComposeContent(content, isTransmittal));
                            
                            page.Footer().AlignCenter().Text(text =>
                            {
                                text.CurrentPageNumber();
                                text.Span(" of ");
                                text.TotalPages();
                            });
                        });
                    })
                    .GeneratePdf(saveDialog.FileName);

                    MessageBox.Show($"PDF saved successfully to:\n{saveDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Open the PDF after saving
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
                catch (Exception docEx)
                {
                    MessageBox.Show($"Error generating PDF document: {docEx.Message}\n\nStack Trace:\n{docEx.StackTrace}", 
                        "Document Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ComposeHeader(IContainer container, bool isTransmittal = false)
    {
        container.Padding(10).Column(column =>
        {
            // Title Row with logo
            column.Item().Row(row =>
            {
                // Use appropriate title based on whether this is a transmittal or register
                string title = isTransmittal ? "TRANSMITTAL" : "DOCUMENT AND DRAWING REGISTER";
                
                row.RelativeItem(3).Text(title)
                    .FontSize(18)
                    .Bold()
                    .FontColor("#000000");

                // Logo with proper image handling and error checking
                try
                {
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var exeDir = Path.GetDirectoryName(exePath);
                    var logoPath = Path.Combine(exeDir!, "Resources", "WHITE LOGO RED BACKGROUND.jpg");
                    
                    if (File.Exists(logoPath))
                    {
                        // Use a single method chain for the container
                        row.RelativeItem().AlignRight().Height(35).Image(logoPath).FitHeight();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading logo: {ex.Message}");
                }
            });

            // Separator line
            column.Item().PaddingTop(2).LineHorizontal(1).LineColor("#eb1845");

            // Project Info section
            column.Item().PaddingTop(2).Row(row =>
            {
                // Left side - Project info
                row.RelativeItem(3).Column(leftCol =>
                {
                    leftCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("DISCIPLINE:").Bold();
                        r.RelativeItem(2).AlignLeft().Text((_project.Discipline ?? "").ToUpper());
                    });

                    leftCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("PROJECT NO:").Bold();
                        r.RelativeItem(2).AlignLeft().Text((_project.ProjectNumber ?? "").ToUpper());
                    });

                    leftCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("PROJECT NAME:").Bold();
                        r.RelativeItem(2).AlignLeft().Text((_project.ProjectName ?? "").ToUpper());
                    });
                });

                // Right side - Registration info
                row.RelativeItem().Column(rightCol =>
                {
                    rightCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("REG NO:").Bold();
                        r.RelativeItem().AlignLeft().Text((_project.RegisterNumber ?? "").ToUpper());
                    });

                    rightCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("CLIENT NO:").Bold();
                        r.RelativeItem().AlignLeft().Text((_project.ClientNumber ?? "").ToUpper());
                    });
                    
                    // Add transmittal number if this is a transmittal
                    if (isTransmittal)
                    {
                        rightCol.Item().Row(r =>
                        {
                            r.RelativeItem().AlignLeft().Text("TRANSMITTAL NO:").Bold();
                            // Generate a transmittal number based on register number and date
                            string transmittalNo = $"{_project.RegisterNumber}-T{DateTime.Now:yyMMdd}";
                            r.RelativeItem().AlignLeft().Text(transmittalNo);
                        });
                    }
                });
            });

            // Bottom separator line
            column.Item().PaddingTop(2).LineHorizontal(1).LineColor("#eb1845");
        });
    }

    private void ComposeContent(IContainer container, bool isTransmittal = false)
    {
        container.Column(column =>
        {
            // Get documents to display - either all or filtered by date
            var documentsToDisplay = _project.Documents.ToList();
            DateTime? selectedDate = null;
            
            if (isTransmittal && IssueDateFilter.SelectedItem is ComboBoxItem selectedItem)
            {
                if (DateTime.TryParseExact(selectedItem.Content.ToString(), "dd/MM/yyyy", null, 
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    selectedDate = parsedDate;
                    documentsToDisplay = _project.Documents
                        .Where(d => d.RevisionHistory.Any(r => r.Key.Date == selectedDate.Value.Date))
                        .OrderBy(d => d.DocumentNumber)
                        .ToList();
                }
            }
            
            // Get last 3 issue dates in descending order (newest first)
            var lastThreeDates = _project.IssueDates
                .OrderByDescending(d => d)
                .Take(3)
                .ToList();

            // Add transmittal-specific information section if this is a transmittal
            if (isTransmittal && selectedDate.HasValue)
            {
                column.Item().PaddingBottom(10).Table(issueInfoTable =>
                {
                    // Define columns
                    issueInfoTable.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(8);
                    });

                    // Date of Issue row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("");
                    });
                    
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .AlignRight()
                         .Text("DATE OF ISSUE: " + selectedDate.Value.ToString("dd/MM/yyyy"));
                    });
                    
                    // Distribution row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("Distribution :").Bold());
                    });
                    
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff").Padding(5).Column(distributionColumn =>
                        {
                            // Get distribution text
                            string distributionText = "No recipients selected";
                            if (selectedDate.HasValue)
                            {
                                distributionText = GetDistributionTextForPdf(selectedDate.Value);
                            }
                            
                            // Split by lines and create a row for each category
                            var distributionLines = distributionText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in distributionLines)
                            {
                                distributionColumn.Item().Text(line);
                            }
                        });
                    });

                    // Purpose of Issue row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("Purpose of Issue :").Bold());
                    });
                    
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff").Padding(5).Table(purposeTable =>
                        {
                            purposeTable.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                            });
                            
                            // Get the purpose from the UI
                            string purpose = "Not specified";
                            if (PurposeOfIssueFilter.SelectedItem is ComboBoxItem purposeItem && 
                                purposeItem.Content.ToString() != "All")
                            {
                                purpose = purposeItem.Content.ToString();
                            }
                            
                            purposeTable.Cell().Element(c => c.Text(purpose));
                        });
                    });

                    // Method of Issue row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("Method of Issue :").Bold());
                    });
                    
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff").Padding(5).Table(methodTable =>
                        {
                            methodTable.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                            });
                            
                            // Get the method from the UI
                            string method = "Not specified";
                            if (MethodOfIssueFilter.SelectedItem is ComboBoxItem methodItem && 
                                methodItem.Content.ToString() != "All")
                            {
                                method = methodItem.Content.ToString();
                            }
                            
                            methodTable.Cell().Element(c => c.Text(method));
                        });
                    });

                    // Issued By row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("Issued by :").Bold());
                    });
                    
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff").Padding(5).Table(issuedByTable =>
                        {
                            issuedByTable.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                            });
                            
                            string issuedByText = IssuedByFilter?.Text ?? "";
                            issuedByTable.Cell().Element(c => c.AlignLeft().Text(issuedByText));
                        });
                    });
                });
            }
            else if (!isTransmittal)
            {
                // For full register, add a note about what a register is
                column.Item().PaddingBottom(10).Background("#f5f5f5").Padding(5).Text(text =>
                {
                    text.Span("NOTE: ").Bold();
                    text.Span("This document is a comprehensive Drawing Register containing all project drawings. ");
                    text.Span("For specific drawing distributions, please refer to Transmittals.");
                });
            }

            // Add document table
            column.Item().Table(table =>
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
                    
                    // Add columns for last 3 dates (newest first) if this is a register
                    // For transmittal, only show the current revision
                    if (!isTransmittal)
                    {
                        foreach (var _ in lastThreeDates)
                        {
                            columns.RelativeColumn(1);
                        }
                    }
                });

                // Add header row with styling
                table.Header(header =>
                {
                    // Create header cells with inline styling
                    header.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("DOCUMENT NO");
                    });
                    
                    header.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("DESCRIPTION");
                    });
                    
                    header.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("PACKAGE");
                    });
                    
                    header.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("TYPE");
                    });
                    
                    header.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("SIZE");
                    });
                    
                    header.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("LATEST REV");
                    });
                    
                    header.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text("LATEST DATE");
                    });

                    if (!isTransmittal)
                    {
                        // Add date headers in descending order for register view
                        foreach (var date in lastThreeDates)
                        {
                            header.Cell().Element(c =>
                            {
                                c.Background("#eb1845")
                                 .Padding(5)
                                 .AlignCenter()
                                 .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                                 .Text(date.ToString("yyyy-MM-dd"));
                            });
                        }
                    }
                });

                // Add data rows with alternating background
                bool isAlternate = false;
                
                foreach (var doc in documentsToDisplay)
                {
                    var latestRev = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                    var rowColor = isAlternate ? "#f5f5f5" : "#ffffff";

                    // Create data cells with inline styling
                    table.Cell().Element(c =>
                    {
                        c.Background(rowColor)
                         .Padding(5)
                         .AlignLeft()
                         .AlignMiddle()
                         .Text(doc.DocumentNumber);
                    });
                    
                    table.Cell().Element(c =>
                    {
                        c.Background(rowColor)
                         .Padding(5)
                         .AlignLeft()
                         .AlignMiddle()
                         .Text(doc.Description);
                    });
                    
                    table.Cell().Element(c =>
                    {
                        c.Background(rowColor)
                         .Padding(5)
                         .AlignLeft()
                         .AlignMiddle()
                         .Text(doc.Package);
                    });
                    
                    table.Cell().Element(c =>
                    {
                        c.Background(rowColor)
                         .Padding(5)
                         .AlignLeft()
                         .AlignMiddle()
                         .Text(doc.DocumentType);
                    });
                    
                    table.Cell().Element(c =>
                    {
                        c.Background(rowColor)
                         .Padding(5)
                         .AlignLeft()
                         .AlignMiddle()
                         .Text(doc.Size);
                    });
                    
                    table.Cell().Element(c =>
                    {
                        c.Background(rowColor)
                         .Padding(5)
                         .AlignLeft()
                         .AlignMiddle()
                         .Text(latestRev.Value?.Revision ?? "");
                    });
                    
                    table.Cell().Element(c =>
                    {
                        c.Background(rowColor)
                         .Padding(5)
                         .AlignLeft()
                         .AlignMiddle()
                         .Text(latestRev.Key.ToString("yyyy-MM-dd"));
                    });

                    if (!isTransmittal)
                    {
                        // Add historical revisions in descending order for register view
                        foreach (var date in lastThreeDates)
                        {
                            var revision = doc.RevisionHistory.TryGetValue(date, out var revInfo)
                                ? revInfo.Revision
                                : "";
                                
                            table.Cell().Element(c =>
                            {
                                c.Background(rowColor)
                                 .Padding(5)
                                 .AlignLeft()
                                 .AlignMiddle()
                                 .Text(revision);
                            });
                        }
                    }

                    isAlternate = !isAlternate;
                }
            });
            
            // Add transmittal footer if this is a transmittal
            if (isTransmittal)
            {
                column.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("TRANSMITTAL CONFIRMATION").Bold();
                        col.Item().PaddingTop(5).LineHorizontal(1);
                        col.Item().PaddingTop(10).Text("RECEIVED BY: _______________________");
                        col.Item().PaddingTop(10).Text("DATE: _______________________");
                        col.Item().PaddingTop(10).Text("SIGNATURE: _______________________");
                    });
                    
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("NOTES").Bold();
                        col.Item().PaddingTop(5).LineHorizontal(1);
                        col.Item().PaddingTop(5).Height(80).Border(1).BorderColor(Colors.Grey.Lighten2);
                    });
                });
            }
        });
    }

    private IContainer HeaderCell(IContainer container)
    {
        // Instead of returning the container, configure it directly
        container.Background("#eb1845")
            .Padding(5)
            .Border(1)
            .BorderColor("#d10835")
            .AlignCenter()
            .DefaultTextStyle(x => x.Bold().FontColor(Colors.White));
            
        // Return the container for method chaining
        return container;
    }

    private void DataCell(IContainer container, string text, string backgroundColor)
    {
        // Configure the container directly
        container.Background(backgroundColor)
            .Padding(5)
            .AlignLeft()
            .AlignMiddle()
            .Text(text);
    }

    private void IssuedByFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterDocuments();
        
        // Update the issued by indicator
        CurrentIssuedByIndicator.Text = IssuedByFilter.Text.Length > 0 ? IssuedByFilter.Text.Substring(0, Math.Min(2, IssuedByFilter.Text.Length)) : "";
    }

    private void ManageDistribution_Click(object sender, RoutedEventArgs e)
    {
        // No longer requiring document selection
        var dialog = new DistributionDialog(_project);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            // Refresh the UI
            DocumentGrid.Items.Refresh();
        }
    }

    private void ViewDistribution_Click(object sender, RoutedEventArgs e)
    {
        if (IssueDateFilter.SelectedItem is not ComboBoxItem selectedItem || selectedItem.Content.ToString() == "All Dates")
        {
            MessageBox.Show("Please select a specific date to view distribution information.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Parse the selected date
        if (DateTime.TryParseExact(selectedItem.Content.ToString(), "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var selectedDate))
        {
            // Show the distribution information dialog
            var dialog = new DistributionInfoDialog(_project, selectedDate);
            dialog.Owner = this;
            dialog.ShowDialog();
            
            // Update the distribution information display
            UpdateDistributionInfoDisplay(selectedDate);
        }
    }

    private void UpdateDistributionInfoDisplay(DateTime selectedDate)
    {
        // Get all documents distributed on this date
        var docsForDate = _project.Documents
            .Where(d => d.DistributionCompanyIds.ContainsKey(selectedDate))
            .ToList();
        
        if (!docsForDate.Any())
        {
            DistributionInfoText.Text = "No recipients selected";
            return;
        }
        
        // Get all company IDs that received documents on this date
        var allCompanyIds = docsForDate
            .SelectMany(d => d.DistributionCompanyIds.TryGetValue(selectedDate, out var ids) ? ids : new List<string>())
            .Distinct()
            .ToList();
        
        // Get the actual company objects
        var companies = allCompanyIds
            .Select(id => _project.DistributionManager.Companies.FirstOrDefault(c => c.Id == id))
            .Where(c => c != null)
            .ToList();
        
        // Group companies by category
        var companiesByCategory = companies
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key)
            .ToList();
        
        // Format the distribution information text with each category on its own line
        var distributionText = new System.Text.StringBuilder();
        
        foreach (var categoryGroup in companiesByCategory)
        {
            // Add each category on a new line with proper alignment
            distributionText.AppendLine($"{categoryGroup.Key}:       {string.Join(", ", categoryGroup.Select(c => c.Name))}");
        }
        
        // Remove the last newline if present
        var formattedText = distributionText.ToString().TrimEnd();
        DistributionInfoText.Text = formattedText;
    }

    private void BatchEdit_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BatchEditDialog(_project);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            // Refresh the UI
            DocumentGrid.Items.Refresh();
            RevisionTimeline.Items.Refresh();
            
            // Update issue date filter options
            UpdateIssueDateFilterOptions();
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

    private string GetDistributionTextForPdf(DateTime selectedDate)
    {
        // Get all documents distributed on this date
        var docsForDate = _project.Documents
            .Where(d => d.DistributionCompanyIds.ContainsKey(selectedDate))
            .ToList();
        
        if (!docsForDate.Any())
        {
            return "No recipients selected";
        }
        
        // Get all company IDs that received documents on this date
        var allCompanyIds = docsForDate
            .SelectMany(d => d.DistributionCompanyIds.TryGetValue(selectedDate, out var ids) ? ids : new List<string>())
            .Distinct()
            .ToList();
        
        // Get the actual company objects
        var companies = allCompanyIds
            .Select(id => _project.DistributionManager.Companies.FirstOrDefault(c => c.Id == id))
            .Where(c => c != null)
            .ToList();
        
        // Group companies by category
        var companiesByCategory = companies
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key)
            .ToList();
        
        // Format the distribution information text with better structure
        var distributionText = new System.Text.StringBuilder();
        
        foreach (var categoryGroup in companiesByCategory)
        {
            if (distributionText.Length > 0)
            {
                distributionText.AppendLine();
            }
            distributionText.Append($"{categoryGroup.Key}: ");
            distributionText.Append(string.Join(", ", categoryGroup.Select(c => c.Name)));
        }
        
        return distributionText.Length > 0 ? distributionText.ToString() : "No recipients selected";
    }

    private void DocumentGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F2 && DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDoc)
        {
            EditDocument(selectedDoc);
            e.Handled = true;
        }
    }
}