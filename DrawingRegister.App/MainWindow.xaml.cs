using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Threading.Tasks;
using DrawingRegister.App.Helpers;
using DrawingRegister.App.Models;
using Serilog;
using System.Collections.Generic;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Binding = System.Windows.Data.Binding;
using System.Windows.Media;
using System.Windows.Input;
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
using Microsoft.Web.WebView2.Core;

namespace DrawingRegister.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
{
    private readonly ProjectManager _project = new();
    // Lock passed to BindingOperations.EnableCollectionSynchronization so that
    // the import Task.Run can mutate _project.Documents without WPF throwing
    // "collection changed from a thread different from the Dispatcher thread".
    private readonly object _documentsCollectionLock = new();
    private bool _disposed;
    private string _searchText = string.Empty;
    private Models.DocumentMetadata? _selectedDocument;
    private bool _webView2Initialized = false;
    private string? _currentPreviewFilePath = null;
    private bool _isPreviewVisible = false;

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
        using var _perf = PerfLog.Begin("MainWindow.ctor");
        ContentRendered += (_, _) =>
            PerfLog.Event("Startup.TimeToFirstPaint", App.ProcessUptime.ElapsedMilliseconds);

        // Allow background-thread mutation of _project.Documents during scans.
        BindingOperations.EnableCollectionSynchronization(_project.Documents, _documentsCollectionLock);

        InitializeComponent();
        DataContext = this;

        // Bind UI elements to ProjectManager properties
        ProjectNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ProjectNumber)) { Source = _project, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        ProjectNameBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ProjectName)) { Source = _project });
        RegNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.RegisterNumber)) { Source = _project });
        ClientNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ClientNumber)) { Source = _project });

        // Initialize DisciplineCombo based on stored Discipline value
        InitializeDisciplineCombo();
        InitializeRevisionSchemeCombo();

        // Subscribe to ProjectNumber changes to update RegisterNumber
        _project.PropertyChanged += Project_PropertyChanged;

        // Bind grid to ProjectManager collections
        DocumentGrid.ItemsSource = _project.Documents;

        // Initialize search type combo
        SearchTypeCombo.SelectedIndex = 0;
        ReportModeCombo.SelectedIndex = 0;
        ReportDatePicker.SelectedDate = DateTime.Today;
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
        UpdateStatusBar();
    }

    // Detach Documents.CollectionChanged and null out DocumentGrid.ItemsSource
    // for the duration of a bulk load. Without this, every Add during a 96-doc
    // import fires the handler above -> UpdateRevisionColumns rebuilds all
    // DataTemplates, etc. Nulling ItemsSource also makes it safe to mutate
    // the ObservableCollection from a background thread (nothing is bound).
    private IDisposable SuspendDocumentUpdates()
    {
        var previousItemsSource = DocumentGrid.ItemsSource;
        DocumentGrid.ItemsSource = null;
        _project.Documents.CollectionChanged -= Documents_CollectionChanged;
        return new ActionDisposable(() =>
        {
            _project.Documents.CollectionChanged += Documents_CollectionChanged;
            // Rebind; callers will overwrite via FilterDocuments() right after.
            DocumentGrid.ItemsSource = previousItemsSource ?? _project.Documents;
        });
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var a = System.Threading.Interlocked.Exchange(ref _action, null);
            a?.Invoke();
        }
    }

    private void UpdateStatusBar()
    {
        var totalCount = DocumentGrid.Items.Count;
        var selectedCount = DocumentGrid.SelectedItems.Count;
        var statusText = $"Documents: {totalCount}";
        if (selectedCount > 0)
            statusText += $" | Selected: {selectedCount}";
        StatusDocumentCount.Text = statusText;
    }

    private void InitializeDisciplineCombo()
    {
        // Set the DisciplineCombo selection based on stored Discipline value (full description)
        var storedDiscipline = _project.Discipline;
        if (!string.IsNullOrEmpty(storedDiscipline))
        {
            // Find the ComboBoxItem with matching Content (full description)
            foreach (ComboBoxItem item in DisciplineCombo.Items)
            {
                if (item.Content?.ToString() == storedDiscipline)
                {
                    DisciplineCombo.SelectedItem = item;
                    return;
                }
            }
            // Fallback: try matching by Tag (code) for backward compatibility with old data
            foreach (ComboBoxItem item in DisciplineCombo.Items)
            {
                if (item.Tag?.ToString() == storedDiscipline)
                {
                    DisciplineCombo.SelectedItem = item;
                    return;
                }
            }
        }
        // Default to first item (General/Multi-discipline) if no match
        DisciplineCombo.SelectedIndex = 0;
    }

    private void Project_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectManager.ProjectNumber))
        {
            UpdateRegisterNumber();
        }
    }

    private void DisciplineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRegisterNumber();
    }

    private bool _suppressRevisionSchemeChange;

    private void InitializeRevisionSchemeCombo()
    {
        _suppressRevisionSchemeChange = true;
        try
        {
            var target = _project.RevisionScheme.ToString();
            foreach (ComboBoxItem item in RevisionSchemeCombo.Items)
            {
                if (item.Tag?.ToString() == target)
                {
                    RevisionSchemeCombo.SelectedItem = item;
                    return;
                }
            }
            RevisionSchemeCombo.SelectedIndex = 0;
        }
        finally
        {
            _suppressRevisionSchemeChange = false;
        }
    }

    private void RevisionSchemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressRevisionSchemeChange) return;
        if (RevisionSchemeCombo.SelectedItem is ComboBoxItem item
            && Enum.TryParse<RevisionScheme>(item.Tag?.ToString(), out var scheme))
        {
            _project.RevisionScheme = scheme;
        }
    }

    private void UpdateRegisterNumber()
    {
        if (DisciplineCombo.SelectedItem is ComboBoxItem selected)
        {
            var disciplineCode = selected.Tag?.ToString() ?? "Z";
            var disciplineDescription = selected.Content?.ToString() ?? "General/Multi-discipline";
            _project.Discipline = disciplineDescription;

            if (!string.IsNullOrEmpty(_project.ProjectNumber))
            {
                _project.RegisterNumber = $"{_project.ProjectNumber}-M+J-00-XX-RE-{disciplineCode}-00-01";
            }
            else
            {
                _project.RegisterNumber = string.Empty;
            }
        }
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
        const double REVISION_COLUMN_WIDTH = 44; // Fixed width for revision columns

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

        // Add a column for each issue date with colored pill badges
        foreach (var date in issueDates)
        {
            var revisionAtDateConverter = (IValueConverter)FindResource("RevisionAtDateConverter");
            var revisionColorAtDateConverter = (IValueConverter)FindResource("RevisionColorAtDateConverter");

            // Create a DataTemplate with a pill badge
            var template = new DataTemplate();
            var borderFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty, FindResource("Slate50Brush"));
            borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(3));
            borderFactory.SetValue(System.Windows.Controls.Border.PaddingProperty, new Thickness(4, 1, 4, 1));
            borderFactory.SetValue(System.Windows.Controls.Border.MarginProperty, new Thickness(2, 2, 2, 2));
            borderFactory.SetValue(System.Windows.Controls.Border.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);

            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty,
                new Binding("RevisionHistory")
                {
                    Converter = revisionAtDateConverter,
                    ConverterParameter = date
                });
            textBlockFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            textBlockFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            textBlockFactory.SetBinding(TextBlock.ForegroundProperty,
                new Binding("RevisionHistory")
                {
                    Converter = revisionColorAtDateConverter,
                    ConverterParameter = date
                });

            borderFactory.AppendChild(textBlockFactory);
            template.VisualTree = borderFactory;

            var column = new DataGridTemplateColumn
            {
                Header = date.ToString("dd/MM/yyyy"),
                Width = new DataGridLength(REVISION_COLUMN_WIDTH),
                HeaderStyle = (Style)FindResource("RotatedColumnHeader"),
                CellTemplate = template
            };
            DocumentGrid.Columns.Add(column);
        }
    }

    private void IssueDateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IssueDateFilter.SelectedItem is not ComboBoxItem selectedItem)
            return;

        var selectedContent = selectedItem.Content.ToString();

        SubfolderFilterLabel.Visibility = Visibility.Collapsed;
        SubfolderFilterCombo.Visibility = Visibility.Collapsed;
        SubfolderFilterCombo.ItemsSource = null; // Clear previous items
        
        if (selectedContent == "All Dates")
        {
            DocumentGrid.ItemsSource = _project.Documents.OrderBy(d => d.DocumentNumber).ToList();
            
            // Hide distribution summary and disable view button
            // Also hide subfolder filter
            DistributionSummaryBorder.Visibility = Visibility.Collapsed;
            ViewDistributionButton.IsEnabled = false;
            
            // Reset the distribution information display
            DistributionInfoText.Text = "No recipients selected";
            return;
        }
        FilterDocuments(); // Call filter documents after handling UI for "All Dates"

        // Enable the view distribution button when a specific date is selected
        ViewDistributionButton.IsEnabled = true;

        // Parse date using the new format
        if (DateTime.TryParseExact(selectedContent, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var selectedDate))
        {
            // Get all documents for this EXACT date only
            var docsForDate = _project.Documents
                .Where(d => d.RevisionHistory.Keys.Any(date => date.Date == selectedDate.Date))
                .OrderBy(d => d.DocumentNumber)
                .ToList();

            DocumentGrid.ItemsSource = docsForDate;

            UpdateDistributionInfoDisplay(selectedDate);

            // Populate subfolder filter. Auto-selecting the first item triggers
            // SubfolderFilterCombo_SelectionChanged, which prefills Purpose / Method /
            // Issued By from the matching revisions, refreshes the distribution summary,
            // and re-runs FilterDocuments.
            PopulateSubfolderFilterForDate(selectedDate.Date);

            // Hide the subfolder filter when there is only one (or zero) subfolder to pick from
            bool hasMultipleSubfolders = SubfolderFilterCombo.Items.Count > 1;
            SubfolderFilterLabel.Visibility = hasMultipleSubfolders ? Visibility.Visible : Visibility.Collapsed;
            SubfolderFilterCombo.Visibility = hasMultipleSubfolders ? Visibility.Visible : Visibility.Collapsed;

            // Ensure the grid reflects the current filters even when the subfolder combo
            // ended up empty (no auto-selection fires in that case).
            if (SubfolderFilterCombo.Items.Count == 0)
                FilterDocuments();
        }
    }

    private void PopulateSubfolderFilterForDate(DateTime selectedDate)
    {
        var uniqueSubfolderPaths = new HashSet<string>();

        foreach (var doc in _project.Documents)
        {
            foreach (var revEntry in doc.RevisionHistory)
            {
                if (revEntry.Key.Date == selectedDate) // Compare only Date part
                {
                    if (!string.IsNullOrEmpty(revEntry.Value.FilePath))
                    {
                        try
                        {
                            string? directoryPath = Path.GetDirectoryName(revEntry.Value.FilePath);
                            if (!string.IsNullOrEmpty(directoryPath))
                            {
                                uniqueSubfolderPaths.Add(directoryPath);
                            }
                        }
                        catch (ArgumentException) { /* Ignore invalid paths */ }
                    }
                }
            }
        }

        var subfolderItems = new ObservableCollection<ComboBoxItem>();

        // Only offer the "All Subfolders" option when there is actually more than one subfolder.
        if (uniqueSubfolderPaths.Count > 1)
        {
            subfolderItems.Add(new ComboBoxItem { Content = "All Subfolders", Tag = "ALL" });
        }

        foreach (var fullPath in uniqueSubfolderPaths.OrderBy(p => new DirectoryInfo(p).Name))
        {
            string displayName = new DirectoryInfo(fullPath).Name;
            var match = System.Text.RegularExpressions.Regex.Match(displayName, @"^(\d{8})(.*)");
            if (match.Success)
            {
                string dateStr = match.Groups[1].Value;
                string description = match.Groups[2].Value.Trim('-', '_', ' ');
                displayName = !string.IsNullOrWhiteSpace(description) ? $"{dateStr} - {description}" : dateStr;
            }
            subfolderItems.Add(new ComboBoxItem { Content = displayName, Tag = fullPath });
        }

        SubfolderFilterCombo.ItemsSource = subfolderItems;
        if (SubfolderFilterCombo.Items.Count > 0)
        {
            SubfolderFilterCombo.SelectedIndex = 0;
        }
    }

    private string MapPurposeToPrefix(string purpose)
    {
        if (string.IsNullOrEmpty(purpose)) return null;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "S", "S" }, { "Concept", "S" },
            { "P", "P" }, { "Planning", "P" },
            { "T", "T" }, { "Tender", "T" }, { "Tender Civil", "T" },
            { "C", "C" }, { "Construction", "C" },
            { "A", "A" }, { "Approval", "A" },
            { "I", "I" }, { "Information", "I" },
            { "W", "W" }, { "Warrant", "W" }, { "Warrant Civil", "W" },
        };
        return map.TryGetValue(purpose.Trim(), out var prefix) ? prefix : null;
    }

    private void UpdateIssueIndicators(string purpose, string method)
    {
        // Get current values if null is passed (to preserve existing values)
        string currentPurpose = purpose ?? CurrentPurposeIndicator.Text;
        string currentMethod = method ?? CurrentMethodIndicator.Text;
        
        // Reset all purpose indicators
        ResetIndicator(PurposeS);
        ResetIndicator(PurposeP);
        ResetIndicator(PurposeT);
        ResetIndicator(PurposeC);
        ResetIndicator(PurposeA);
        ResetIndicator(PurposeI);
        ResetIndicator(PurposeW);
        
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
            if (currentPurpose.StartsWith("S"))
                HighlightIndicator(PurposeS);
            else if (currentPurpose.StartsWith("P"))
                HighlightIndicator(PurposeP);
            else if (currentPurpose.StartsWith("T"))
                HighlightIndicator(PurposeT);
            else if (currentPurpose.StartsWith("C"))
                HighlightIndicator(PurposeC);
            else if (currentPurpose.StartsWith("A"))
                HighlightIndicator(PurposeA);
            else if (currentPurpose.StartsWith("I"))
                HighlightIndicator(PurposeI);
            else if (currentPurpose.StartsWith("W"))
                HighlightIndicator(PurposeW);
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

    private void ApplyIssueFiltersFromRevisions(List<RevisionInfo> revisions)
    {
        if (revisions.Any())
        {
            var commonPurpose = revisions
                .GroupBy(r => r.Purpose)
                .OrderByDescending(g => g.Count())
                .First().Key;

            var commonMethod = revisions
                .GroupBy(r => r.Method)
                .OrderByDescending(g => g.Count())
                .First().Key;

            var commonIssuer = revisions
                .GroupBy(r => r.IssuedBy)
                .OrderByDescending(g => g.Count())
                .First().Key;

            var purposePrefix = MapPurposeToPrefix(commonPurpose);
            if (purposePrefix != null)
            {
                foreach (ComboBoxItem item in PurposeOfIssueFilter.Items)
                {
                    if (item.Content.ToString().StartsWith(purposePrefix + " "))
                    {
                        PurposeOfIssueFilter.SelectedItem = item;
                        break;
                    }
                }
            }

            var methodPrefix = commonMethod?.Length > 0 ? commonMethod.Substring(0, 1).ToUpper() : null;
            if (methodPrefix != null)
            {
                foreach (ComboBoxItem item in MethodOfIssueFilter.Items)
                {
                    if (item.Content.ToString().StartsWith(methodPrefix + " "))
                    {
                        MethodOfIssueFilter.SelectedItem = item;
                        break;
                    }
                }
            }

            IssuedByFilter.Text = commonIssuer;

            UpdateIssueIndicators(commonPurpose, commonMethod);
        }
        else
        {
            PurposeOfIssueFilter.SelectedIndex = 0;
            MethodOfIssueFilter.SelectedIndex = 0;
            IssuedByFilter.Text = string.Empty;
            UpdateIssueIndicators(string.Empty, string.Empty);
        }
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

    private void SubfolderFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IssueDateFilter.SelectedItem is ComboBoxItem dateItem
            && dateItem.Content?.ToString() is string dateContent
            && dateContent != "All Dates"
            && DateTime.TryParseExact(dateContent, "dd/MM/yyyy", null,
                System.Globalization.DateTimeStyles.None, out var selectedDate))
        {
            string? subfolderPath = null;
            if (SubfolderFilterCombo.SelectedItem is ComboBoxItem folderItem
                && folderItem.Tag is string tag
                && tag != "ALL")
            {
                subfolderPath = tag;
            }

            var revisions = _project.Documents
                .SelectMany(d => d.RevisionHistory)
                .Where(r =>
                    r.Key.Date == selectedDate.Date &&
                    (subfolderPath == null ||
                        (!string.IsNullOrEmpty(r.Value.FilePath) &&
                         string.Equals(Path.GetDirectoryName(r.Value.FilePath), subfolderPath, StringComparison.OrdinalIgnoreCase))))
                .Select(r => r.Value)
                .ToList();

            ApplyIssueFiltersFromRevisions(revisions);

            var distributionSummary = DistributionSummary.GenerateForDate(_project, selectedDate, subfolderPath);
            DistributionSummaryText.Text = distributionSummary.GetFormattedSummary();
            DistributionSummaryBorder.Visibility = distributionSummary.TotalRecipients > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        FilterDocuments();
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
        using var _perf = PerfLog.Begin("FilterDocuments");
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
        
        // Date and Subfolder filter logic
        DateTime? selectedFilterDate = null;
        string? selectedSubfolderPath = null;

        if (IssueDateFilter.SelectedItem is ComboBoxItem dateItem && dateItem.Content.ToString() != "All Dates")
        {
            if (DateTime.TryParseExact(dateItem.Content.ToString(), "dd/MM/yyyy", null, 
                System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                selectedFilterDate = parsedDate.Date;
            }
        }

        if (selectedFilterDate.HasValue && SubfolderFilterCombo.Visibility == Visibility.Visible && SubfolderFilterCombo.SelectedItem is ComboBoxItem folderItem && folderItem.Tag is string folderPathTag)
        {
            if (folderPathTag != "ALL")
            {
                selectedSubfolderPath = folderPathTag;
            }
        }
        
        // Apply search text filter if not empty
        if (!string.IsNullOrEmpty(searchText))
        {
            string searchType = "Document No";
            if (SearchTypeCombo.SelectedItem is ComboBoxItem selectedItem)
                searchType = selectedItem.Content.ToString();
                
            switch (searchType)
            {
                case "Document No":
                    filteredDocs = filteredDocs.Where(d => d.DocumentNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
                    break;
                case "Description":
                    filteredDocs = filteredDocs.Where(d => d.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
                    break;
                case "Package":
                    filteredDocs = filteredDocs.Where(d => d.Package.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
                    break;
                case "Type":
                    filteredDocs = filteredDocs.Where(d => d.DocumentType.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
                    break;
            }
        }
        
        // Apply date filter if selected
        if (selectedFilterDate.HasValue)
        {
            // First, filter by date
            filteredDocs = filteredDocs.Where(doc => 
                doc.RevisionHistory.Any(revEntry => revEntry.Key.Date == selectedFilterDate.Value)
            ).ToList();

            // Then, if a specific subfolder is selected, filter by subfolder
            if (!string.IsNullOrEmpty(selectedSubfolderPath))
            {
                filteredDocs = filteredDocs.Where(doc => 
                    doc.RevisionHistory.Any(revEntry => 
                        revEntry.Key.Date == selectedFilterDate.Value &&
                        !string.IsNullOrEmpty(revEntry.Value.FilePath) &&
                        string.Equals(Path.GetDirectoryName(revEntry.Value.FilePath), selectedSubfolderPath, StringComparison.OrdinalIgnoreCase)
                    )
                ).ToList();
            }
        }
        
        // Apply purpose filter
        if (purposeFilter != "All")
        {
            string purposeCode = purposeFilter.Split('-')[0].Trim();
            if (selectedFilterDate.HasValue)
            {
                filteredDocs = filteredDocs.Where(d => 
                    d.RevisionHistory.Any(r => 
                        r.Key.Date == selectedFilterDate.Value && 
                        r.Value.Purpose.StartsWith(purposeCode, StringComparison.OrdinalIgnoreCase)
                    )
                ).ToList();
            }
            else
            {
                filteredDocs = filteredDocs.Where(d => 
                    d.RevisionHistory.Any(r => 
                        r.Value.Purpose.StartsWith(purposeCode, StringComparison.OrdinalIgnoreCase)
                    )
                ).ToList();
            }
        }
        
        // Apply method filter
        if (methodFilter != "All")
        {
            string methodCode = methodFilter.Split('-')[0].Trim();
            if (selectedFilterDate.HasValue)
            {
                filteredDocs = filteredDocs.Where(d => 
                    d.RevisionHistory.Any(r => 
                        r.Key.Date == selectedFilterDate.Value && 
                        r.Value.Method.StartsWith(methodCode, StringComparison.OrdinalIgnoreCase)
                    )
                ).ToList();
            }
            else
            {
                filteredDocs = filteredDocs.Where(d => 
                    d.RevisionHistory.Any(r => r.Value.Method.StartsWith(methodCode, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
        }
        
        // Apply issued by filter
        if (!string.IsNullOrEmpty(issuedByFilter))
        {
            if (selectedFilterDate.HasValue)
            {
                filteredDocs = filteredDocs
                    .Where(d => d.RevisionHistory.Any(r => 
                            r.Key.Date == selectedFilterDate.Value && 
                            r.Value.IssuedBy.Contains(issuedByFilter, StringComparison.OrdinalIgnoreCase)
                        )
                    ).ToList();
            }
            else
            {
                filteredDocs = filteredDocs.Where(d => 
                    d.RevisionHistory.Any(r => 
                        r.Value.IssuedBy.Contains(issuedByFilter, StringComparison.OrdinalIgnoreCase)
                    )
                ).ToList();
            }
        }
        
        // Update the grid
        DocumentGrid.ItemsSource = filteredDocs.OrderBy(d => d.DocumentNumber).ToList();
    }

    private async void ImportDocuments_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select PDF folder to scan",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            using var _perf = PerfLog.Begin($"ImportDocuments_Click({System.IO.Path.GetFileName(dialog.SelectedPath)})");
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

                ImportResult importResult;
                using (SuspendDocumentUpdates())
                {
                    var selectedPath = dialog.SelectedPath;
                    importResult = await Task.Run(() => _project.ImportDocuments(selectedPath));
                }
                InitializeDisciplineCombo();
                InitializeRevisionSchemeCombo();
                UpdateRegisterNumber();
                UpdateIssueDateFilterOptions();
                UpdateRevisionColumns();
                FilterDocuments();

                if (importResult.HasSkippedFiles)
                {
                    var skippedList = importResult.SkippedFiles.Take(20)
                        .Select(f => $"  • {f.FileName}\n    Reason: {f.Reason}");
                    var message = $"{importResult.SkippedFiles.Count} of {importResult.TotalPdfFiles} PDF files were not added to the register:\n\n"
                        + string.Join("\n\n", skippedList);
                    if (importResult.SkippedFiles.Count > 20)
                        message += $"\n\n... and {importResult.SkippedFiles.Count - 20} more.";
                    MessageBox.Show(message, "Import Warning - Skipped Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                HandleSuggestedRenames(importResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing documents: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void RefreshView_Click(object sender, RoutedEventArgs e)
    {
        using var _perf = PerfLog.Begin("RefreshView_Click");
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
            ImportResult importResult;
            using (SuspendDocumentUpdates())
            {
                var basePath = _project._currentBasePath;
                importResult = await Task.Run(() => _project.ImportDocuments(basePath));
            }

            // Re-initialize discipline combo and update register number
            InitializeDisciplineCombo();
            InitializeRevisionSchemeCombo();
            UpdateRegisterNumber();

            // Clear all filters so user sees everything
            SearchBox.Text = "";
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            if (IssueDateFilter.Items.Count > 0)
                IssueDateFilter.SelectedIndex = 0; // "All Dates"
            if (PurposeOfIssueFilter.Items.Count > 0)
                PurposeOfIssueFilter.SelectedIndex = 0; // "All"
            if (MethodOfIssueFilter.Items.Count > 0)
                MethodOfIssueFilter.SelectedIndex = 0; // "All"
            IssuedByFilter.Text = "";

            // Refresh the document grid view
            var view = CollectionViewSource.GetDefaultView(DocumentGrid.ItemsSource);
            view.Refresh();

            // Apply filters (now all cleared, so shows everything)
            FilterDocuments();

            // Force grid to update
            DocumentGrid.Items.Refresh();

            var refreshMessage = $"Successfully refreshed {_project.Documents.Count} documents. ({importResult.SuccessfullyParsed} of {importResult.TotalPdfFiles} PDF files parsed)";
            if (importResult.HasSkippedFiles)
            {
                var skippedList = importResult.SkippedFiles.Take(20)
                    .Select(f => $"  • {f.FileName}\n    Reason: {f.Reason}");
                refreshMessage += $"\n\n⚠ {importResult.SkippedFiles.Count} PDF files were not added:\n\n"
                    + string.Join("\n\n", skippedList);
                if (importResult.SkippedFiles.Count > 20)
                    refreshMessage += $"\n\n... and {importResult.SkippedFiles.Count - 20} more.";
                MessageBox.Show(refreshMessage, "Refresh Complete - With Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(refreshMessage, "Refresh Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            HandleSuggestedRenames(importResult);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing documents: {ex.Message}",
                "Refresh Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void RescanFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_project._currentBasePath))
            {
                MessageBox.Show("No project loaded. Please scan a folder first.",
                    "Rescan Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_project._currentStorage == null || _project._currentStorage.Projects.Count == 0)
            {
                MessageBox.Show("No processed folders found. Please scan a folder first.",
                    "Rescan Error", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var folderPaths = _project._currentStorage.Projects.Select(p => p.FolderPath).ToList();
            var dialog = new RescanFolderDialog(folderPaths);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                var selectedFolder = dialog.SelectedFolderPath;

                ImportResult importResult;
                using (SuspendDocumentUpdates())
                {
                    var basePath = _project._currentBasePath;
                    importResult = await Task.Run(() => _project.ImportDocuments(basePath, selectedFolder));
                }

                InitializeDisciplineCombo();
                InitializeRevisionSchemeCombo();
                UpdateRegisterNumber();
                UpdateIssueDateFilterOptions();
                UpdateRevisionColumns();
                FilterDocuments();

                var folderName = System.IO.Path.GetFileName(selectedFolder);
                var message = $"Rescan of '{folderName}' complete. ({importResult.SuccessfullyParsed} of {importResult.TotalPdfFiles} PDF files parsed)";

                if (importResult.HasSkippedFiles)
                {
                    var skippedList = importResult.SkippedFiles.Take(20)
                        .Select(f => $"  • {f.FileName}\n    Reason: {f.Reason}");
                    message += $"\n\n{importResult.SkippedFiles.Count} PDF files were not added:\n\n"
                        + string.Join("\n\n", skippedList);
                    if (importResult.SkippedFiles.Count > 20)
                        message += $"\n\n... and {importResult.SkippedFiles.Count - 20} more.";
                    MessageBox.Show(message, "Rescan Complete - With Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(message, "Rescan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                HandleSuggestedRenames(importResult);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error rescanning folder: {ex.Message}",
                "Rescan Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DocumentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDoc)
        {
            SelectedDocument = selectedDoc;
            if (_isPreviewVisible)
            {
                await PreviewDocumentAsync(selectedDoc);
            }
        }
        else if (_isPreviewVisible)
        {
            ShowPreviewPlaceholder("NO DOCUMENT SELECTED", "Select a document to preview");
        }
        UpdateStatusBar();
    }

    private void DocumentGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // If clicked on empty space (not on a row)
        if (e.OriginalSource is System.Windows.Controls.DataGridRow) return;

        // Clear selection and selected document
        DocumentGrid.SelectedItem = null;
        SelectedDocument = null;

        if (_isPreviewVisible)
        {
            ShowPreviewPlaceholder("NO DOCUMENT SELECTED", "Select a document to preview");
        }
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

    private async void RevisionTimeline_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is KeyValuePair<DateTime, RevisionInfo> revision)
        {
            if (_isPreviewVisible && SelectedDocument != null)
            {
                await PreviewDocumentAsync(SelectedDocument, revision.Value.FilePath);
            }
            else
            {
                OpenDocument(revision.Value.FilePath);
            }
        }
    }

    private void EditRevision_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && 
            button.DataContext is KeyValuePair<DateTime, RevisionInfo> revision &&
            SelectedDocument != null)
        {
            var dialog = new RevisionEditDialog(SelectedDocument, revision.Key, revision.Value, _project.RevisionScheme);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Update the revision info
                revision.Value.Purpose = dialog.Purpose;
                revision.Value.Method = dialog.Method;
                revision.Value.IssuedBy = dialog.IssuedBy;
                revision.Value.Revision = dialog.Revision;
                revision.Value.IsSuperseded = dialog.IsSuperseded;

                // Update the document's current values (reflect the latest non-superseded revision)
                var currentLatest = SelectedDocument.LatestNonSupersededRevision;
                if (currentLatest.HasValue)
                {
                    SelectedDocument.PurposeOfIssue = currentLatest.Value.Value.Purpose;
                    SelectedDocument.MethodOfIssue = currentLatest.Value.Value.Method;
                    SelectedDocument.IssuedBy = currentLatest.Value.Value.IssuedBy;
                    SelectedDocument.Revision = currentLatest.Value.Value.Revision;
                    SelectedDocument.FilePath = currentLatest.Value.Value.FilePath;
                }
                else
                {
                    SelectedDocument.PurposeOfIssue = dialog.Purpose;
                    SelectedDocument.MethodOfIssue = dialog.Method;
                    SelectedDocument.IssuedBy = dialog.IssuedBy;
                    SelectedDocument.Revision = dialog.Revision;
                }

                // Save changes
                _project.SaveProjectData();

                // Refresh views
                DocumentGrid.Items.Refresh();
                RevisionTimeline.Items.Refresh();
            }
        }
    }

    // =========================================
    // PDF PREVIEW PANEL
    // =========================================

    private async System.Threading.Tasks.Task EnsureWebView2InitializedAsync()
    {
        if (_webView2Initialized) return;

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DrawingRegister", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await PdfWebView.EnsureCoreWebView2Async(env);

            PdfWebView.CoreWebView2.Settings.IsZoomControlEnabled = true;
            PdfWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PdfWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            PdfWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Block non-file navigations for security
            PdfWebView.CoreWebView2.NavigationStarting += (s, args) =>
            {
                var uri = args.Uri;
                if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                    !uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
            };

            _webView2Initialized = true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "The WebView2 Runtime is not installed.\n\n" +
                "Please download and install it from:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/\n\n" +
                "The PDF preview feature requires this component.",
                "WebView2 Runtime Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            PreviewToggle.IsChecked = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize PDF preview: {ex.Message}",
                "Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            PreviewToggle.IsChecked = false;
        }
    }

    private async void PreviewToggle_Changed(object sender, RoutedEventArgs e)
    {
        _isPreviewVisible = PreviewToggle.IsChecked == true;

        if (_isPreviewVisible)
        {
            await EnsureWebView2InitializedAsync();
            if (!_webView2Initialized) return;

            // Show the preview panel
            SplitterColumn.Width = new GridLength(5);
            PreviewColumn.Width = new GridLength(450);
            PreviewSplitter.Visibility = Visibility.Visible;
            PreviewPanel.Visibility = Visibility.Visible;

            // Preview the currently selected document
            if (SelectedDocument != null)
            {
                await PreviewDocumentAsync(SelectedDocument);
            }
            else
            {
                ShowPreviewPlaceholder("NO DOCUMENT SELECTED", "Select a document to preview");
            }
        }
        else
        {
            // Hide the preview panel
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.Width = new GridLength(0);
            PreviewSplitter.Visibility = Visibility.Collapsed;
            PreviewPanel.Visibility = Visibility.Collapsed;

            // Release file handle
            if (_webView2Initialized)
            {
                PdfWebView.CoreWebView2.Navigate("about:blank");
            }
            _currentPreviewFilePath = null;
        }
    }

    private async System.Threading.Tasks.Task PreviewDocumentAsync(Models.DocumentMetadata document, string? specificRevisionFilePath = null)
    {
        if (!_isPreviewVisible || !_webView2Initialized) return;

        // Determine the file path to preview
        string? filePath = specificRevisionFilePath;
        string revisionLabel = document.Revision ?? "";

        if (string.IsNullOrEmpty(filePath))
        {
            // Use the latest revision's file path, or the document's main file path
            if (document.RevisionHistory.Any())
            {
                var latest = document.RevisionHistory.OrderByDescending(kv => kv.Key).First();
                filePath = latest.Value.FilePath;
                revisionLabel = latest.Value.Revision ?? revisionLabel;
            }
            else
            {
                filePath = document.FilePath;
            }
        }
        else
        {
            // Find the revision label for the specific file
            var matchingRevision = document.RevisionHistory
                .FirstOrDefault(kv => string.Equals(kv.Value.FilePath, specificRevisionFilePath, StringComparison.OrdinalIgnoreCase));
            if (matchingRevision.Value != null)
            {
                revisionLabel = matchingRevision.Value.Revision ?? revisionLabel;
            }
        }

        // No file path available
        if (string.IsNullOrEmpty(filePath))
        {
            ShowPreviewPlaceholder("NO FILE PATH", "This document has no associated file");
            _currentPreviewFilePath = null;
            return;
        }

        // Skip reload if same file already displayed
        if (string.Equals(_currentPreviewFilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            // Just update header text
            PreviewHeaderText.Text = $"{document.DocumentNumber}  Rev {revisionLabel}";
            return;
        }

        // Check file exists
        if (!File.Exists(filePath))
        {
            ShowPreviewError(filePath);
            _currentPreviewFilePath = null;
            return;
        }

        // Check it's a PDF
        if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ShowPreviewPlaceholder("NOT A PDF FILE", Path.GetFileName(filePath));
            _currentPreviewFilePath = null;
            return;
        }

        // Navigate to the PDF
        try
        {
            PreviewHeaderText.Text = $"{document.DocumentNumber}  Rev {revisionLabel}";
            PdfWebView.Visibility = Visibility.Visible;
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            PreviewError.Visibility = Visibility.Collapsed;

            var fileUri = new Uri(filePath).AbsoluteUri + "#view=Fit";
            PdfWebView.CoreWebView2.Navigate(fileUri);
            _currentPreviewFilePath = filePath;
        }
        catch (Exception ex)
        {
            ShowPreviewPlaceholder("PREVIEW ERROR", ex.Message);
            _currentPreviewFilePath = null;
        }
    }

    private void ShowPreviewPlaceholder(string title, string subtitle)
    {
        PlaceholderTitle.Text = title;
        PlaceholderSubtitle.Text = subtitle;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        PreviewError.Visibility = Visibility.Collapsed;
        PdfWebView.Visibility = Visibility.Collapsed;

        if (_webView2Initialized)
        {
            PdfWebView.CoreWebView2.Navigate("about:blank");
        }
        _currentPreviewFilePath = null;
    }

    private void ShowPreviewError(string filePath)
    {
        ErrorFilePath.Text = filePath;
        PreviewError.Visibility = Visibility.Visible;
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        PdfWebView.Visibility = Visibility.Collapsed;

        if (_webView2Initialized)
        {
            PdfWebView.CoreWebView2.Navigate("about:blank");
        }
        _currentPreviewFilePath = null;
    }

    private void OpenExternalPdf_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPreviewFilePath) && File.Exists(_currentPreviewFilePath))
        {
            OpenDocument(_currentPreviewFilePath);
        }
    }

    private void HandleSuggestedRenames(ImportResult importResult)
    {
        if (!importResult.HasSuggestedRenames) return;

        var dialog = new RenameFilesDialog(importResult.SuggestedRenames);
        dialog.Owner = this;

        if (dialog.ShowDialog() == true && dialog.ApprovedRenames.Count > 0)
        {
            int renamed = 0;
            int failed = 0;
            foreach (var rename in dialog.ApprovedRenames)
            {
                try
                {
                    var directory = Path.GetDirectoryName(rename.OriginalPath) ?? string.Empty;
                    var newPath = Path.Combine(directory, rename.SuggestedName);

                    if (File.Exists(newPath) && !string.Equals(rename.OriginalPath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        failed++;
                        continue;
                    }

                    File.Move(rename.OriginalPath, newPath);
                    renamed++;

                    // Update stored paths in documents and revision history
                    foreach (var doc in _project.Documents)
                    {
                        if (string.Equals(doc.FilePath, rename.OriginalPath, StringComparison.OrdinalIgnoreCase))
                        {
                            doc.FilePath = newPath;
                        }

                        foreach (var rev in doc.RevisionHistory)
                        {
                            if (string.Equals(rev.Value.FilePath, rename.OriginalPath, StringComparison.OrdinalIgnoreCase))
                            {
                                rev.Value.FilePath = newPath;
                            }
                        }
                    }
                }
                catch
                {
                    failed++;
                }
            }

            _project.SaveProjectData();

            var message = $"Renamed {renamed} file(s).";
            if (failed > 0)
                message += $" {failed} file(s) could not be renamed.";
            MessageBox.Show(message, "Rename Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void DocumentGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Find the row that was clicked
        var dep = (DependencyObject)e.OriginalSource;
        while (dep != null && !(dep is DataGridRow))
        {
            dep = VisualTreeHelper.GetParent(dep);
        }

        if (dep is DataGridRow row)
        {
            if (!row.IsSelected)
            {
                // Select the right-clicked row
                DocumentGrid.SelectedItem = row.DataContext;
            }

            e.Handled = true;
            // Manually show context menu
            DocumentGrid.ContextMenu.PlacementTarget = DocumentGrid;
            DocumentGrid.ContextMenu.IsOpen = true;
        }
    }

    private void SetPaperSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string size)
        {
            var selectedItems = DocumentGrid.SelectedItems.Cast<Models.DocumentMetadata>().ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more documents first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var doc in selectedItems)
            {
                doc.Size = size;
            }

            _project.SaveProjectData();
            DocumentGrid.Items.Refresh();

            MessageBox.Show($"Paper size set to {size} for {selectedItems.Count} document(s).", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

            // Filter documents that have revisions with exact issue dates in the range
            var filteredDocuments = _project.Documents
                .Where(d => d.RevisionHistory.Keys
                    .Any(issueDate => issueDate.Date >= startDate && issueDate.Date <= endDate))
                .OrderBy(d => d.DocumentNumber)
                .ToList();

            DocumentGrid.ItemsSource = filteredDocuments;
            
            // Show a message with the filter results
            MessageBox.Show($"Showing {filteredDocuments.Count} documents issued between {startDate:dd/MM/yyyy} and {endDate:dd/MM/yyyy}", 
                "Filter Applied", MessageBoxButton.OK, MessageBoxImage.Information);
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
        
        // Reset to show all documents
        DocumentGrid.ItemsSource = _project.Documents.OrderBy(d => d.DocumentNumber).ToList();
        
        // Inform the user
        MessageBox.Show("Date filter cleared. Showing all documents.", 
            "Filter Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
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

            var reportDate = GetSelectedReportDate();
            var reportMode = GetSelectedPdfReportMode();
            var reportIdentity = PdfReportIdentityBuilder.Create(
                reportMode,
                _project.ProjectNumber,
                _project.RegisterNumber,
                reportDate);

            string? selectedSubfolderPathForReport = null;
            if (reportIdentity.IsTransmittal && SubfolderFilterCombo.Visibility == Visibility.Visible && SubfolderFilterCombo.SelectedItem is ComboBoxItem folderItem && folderItem.Tag is string folderPathTag && folderPathTag != "ALL")
            {
                selectedSubfolderPathForReport = folderPathTag;
            }

            string fileNamePrefix = reportIdentity.FileNamePrefix;
            if (reportIdentity.IsTransmittal && !string.IsNullOrEmpty(selectedSubfolderPathForReport))
                fileNamePrefix += $"_{new DirectoryInfo(selectedSubfolderPathForReport).Name.Replace(" ", "_")}";

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
                    var outputPath = PdfReportFilePathResolver.GetWritablePath(saveDialog.FileName);

                    // Create and save the document
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(1, Unit.Centimetre);
                            page.DefaultTextStyle(x => x.FontSize(9));

                            // Use a single Element call for each section
                            page.Header().Element(header => ComposeHeader(header, reportIdentity));
                            page.Content().Element(content => ComposeContent(content, reportMode, selectedSubfolderPathForReport));
                            
                            // Only add page numbers in the footer, no transmittal confirmation here
                            page.Footer().AlignCenter().Text(text =>
                            {
                                text.CurrentPageNumber();
                                text.Span(" of ");
                                text.TotalPages();
                            });
                        });
                    })
                    .GeneratePdf(outputPath);

                    var saveMessage = outputPath == saveDialog.FileName
                        ? $"PDF saved successfully to:\n{outputPath}"
                        : $"The selected PDF was open in another program, so the report was saved as:\n{outputPath}";

                    MessageBox.Show(saveMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Open the PDF after saving
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = outputPath,
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

    private DateTime GetSelectedReportDate()
    {
        return ReportDatePicker.SelectedDate?.Date ?? DateTime.Today;
    }

    private PdfReportMode GetSelectedPdfReportMode()
    {
        if (ReportModeCombo.SelectedItem is ComboBoxItem modeItem &&
            string.Equals(modeItem.Tag?.ToString(), "DocReg", StringComparison.OrdinalIgnoreCase))
        {
            return PdfReportMode.DocReg;
        }

        return HasSelectedIssueDate()
            ? PdfReportMode.Transmittal
            : PdfReportMode.Register;
    }

    private bool HasSelectedIssueDate()
    {
        return IssueDateFilter.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Content.ToString() != "All Dates";
    }

    private List<Models.DocumentMetadata> GetCurrentGridDocuments()
    {
        return (DocumentGrid.ItemsSource as IEnumerable<Models.DocumentMetadata>)?
            .OrderBy(d => d.DocumentNumber)
            .ToList()
            ?? _project.Documents.OrderBy(d => d.DocumentNumber).ToList();
    }

    private void ComposeHeader(IContainer container, PdfReportIdentity reportIdentity)
    {
        container.Padding(10).Column(column =>
        {
            // Title Row with logo
            column.Item().Row(row =>
            {
                row.RelativeItem(3).Text(reportIdentity.Title)
                    .FontSize(18)
                    .Bold()
                    .FontColor("#000000");

                // Load logo with improved path resolution and error handling
                try
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using (var stream = assembly.GetManifestResourceStream("DrawingRegister.App.Resources.company-logo.png"))
                    {
                        if (stream != null)
                        {
                            row.RelativeItem().AlignRight().Height(35).Image(stream).FitHeight();
                        }
                        else
                        {
                            // Try alternate logo
                            using (var altStream = assembly.GetManifestResourceStream("DrawingRegister.App.Resources.WHITE LOGO RED BACKGROUND.jpg"))
                            {
                                if (altStream != null)
                                {
                                    row.RelativeItem().AlignRight().Height(35).Image(altStream).FitHeight();
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("Could not load either logo resource");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading logo: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
                row.RelativeItem(2).Column(rightCol =>
                {
                    rightCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("REG NO:").Bold();
                        r.RelativeItem(3).AlignLeft().Text(reportIdentity.HeaderRegisterNumber);
                    });

                    rightCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("CLIENT NO:").Bold();
                        r.RelativeItem(3).AlignLeft().Text((_project.ClientNumber ?? "").ToUpper());
                    });
                    
                    // Add transmittal number if this is a transmittal
                    if (reportIdentity.IsTransmittal && !string.IsNullOrWhiteSpace(reportIdentity.TransmittalNumber))
                    {
                        rightCol.Item().Row(r =>
                        {
                            r.RelativeItem().AlignLeft().Text("TRANSMITTAL NO:").Bold();
                            r.RelativeItem(3).AlignLeft().Text(reportIdentity.TransmittalNumber.ToUpper());
                        });
                    }
                });
            });

            // Bottom separator line
            column.Item().PaddingTop(2).LineHorizontal(1).LineColor("#eb1845");
        });
    }

    private void ComposeContent(IContainer container, PdfReportMode reportMode, string? selectedSubfolderPathForReport = null)
    {
        container.Column(column =>
        {
            var isTransmittal = reportMode == PdfReportMode.Transmittal;
            var isDocReg = reportMode == PdfReportMode.DocReg;

            // Get documents to display - either all or filtered by date
            List<Models.DocumentMetadata> documentsToDisplay;
            DateTime? selectedDate = null;
            
            if (isTransmittal && IssueDateFilter.SelectedItem is ComboBoxItem selectedItem)
            {
                if (DateTime.TryParseExact(selectedItem.Content.ToString(), "dd/MM/yyyy", null, 
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    selectedDate = parsedDate;
                    documentsToDisplay = _project.Documents
                        .Where(d => d.RevisionHistory.Any(r => r.Key.Date == selectedDate.Value.Date))
                        .ToList();

                    if (!string.IsNullOrEmpty(selectedSubfolderPathForReport))
                    {
                        documentsToDisplay = documentsToDisplay
                            .Where(d => d.RevisionHistory.Any(r => 
                                r.Key.Date == selectedDate.Value.Date &&
                                !string.IsNullOrEmpty(r.Value.FilePath) &&
                                string.Equals(Path.GetDirectoryName(r.Value.FilePath), selectedSubfolderPathForReport, StringComparison.OrdinalIgnoreCase)
                            ))
                            .ToList();
                    }
                    documentsToDisplay = documentsToDisplay.OrderBy(d => d.DocumentNumber).ToList();
                }
                else { documentsToDisplay = _project.Documents.OrderBy(d => d.DocumentNumber).ToList(); } // Fallback
            }
            else if (isDocReg)
            {
                documentsToDisplay = GetCurrentGridDocuments();
            }
            else
            {
                documentsToDisplay = _project.Documents.OrderBy(d => d.DocumentNumber).ToList();
            }
            
            var fullRegisterRows = isTransmittal
                ? Array.Empty<DrawingRegisterPdfReportBuilder.DrawingRegisterPdfReportRow>()
                : DrawingRegisterPdfReportBuilder.BuildFullRegisterRows(documentsToDisplay);

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
                         .Text(x => x.Span("DISTRIBUTION :").Bold());
                    });
                    
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff").Padding(5).Column(distributionColumn =>
                        {
                            // Get distribution text
                            string distributionText = "NO RECIPIENTS SELECTED";
                            if (selectedDate.HasValue)
                            {
                                distributionText = GetDistributionTextForPdf(selectedDate.Value, selectedSubfolderPathForReport).ToUpper();
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
                         .Text(x => x.Span("PURPOSE OF ISSUE :").Bold());
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
                            string purpose = "NOT SPECIFIED";
                            if (PurposeOfIssueFilter.SelectedItem is ComboBoxItem purposeItem && 
                                purposeItem.Content.ToString() != "All")
                            {
                                purpose = purposeItem.Content.ToString().ToUpper();
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
                         .Text(x => x.Span("METHOD OF ISSUE :").Bold());
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
                            string method = "NOT SPECIFIED";
                            if (MethodOfIssueFilter.SelectedItem is ComboBoxItem methodItem && 
                                methodItem.Content.ToString() != "All")
                            {
                                method = methodItem.Content.ToString().ToUpper();
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
                         .Text(x => x.Span("ISSUED BY :").Bold());
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
                            issuedByTable.Cell().Element(c => c.AlignLeft().Text(issuedByText.ToUpper()));
                        });
                    });
                });
            }
            // Add the main document table
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
                });

                // Add data rows with alternating background
                bool isAlternate = false;

                if (isTransmittal)
                {
                    foreach (var doc in documentsToDisplay)
                    {
                        var latestRev = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                        var rowColor = isAlternate ? "#f5f5f5" : "#ffffff";

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(doc.DocumentNumber.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(doc.Description.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(doc.Package.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(doc.DocumentType.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(doc.Size.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text((latestRev.Value?.Revision ?? "").ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(latestRev.Key.ToString("yyyy-MM-dd"));
                        });

                        isAlternate = !isAlternate;
                    }
                }
                else
                {
                    foreach (var row in fullRegisterRows)
                    {
                        var rowColor = isAlternate ? "#f5f5f5" : "#ffffff";

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(row.DocumentNumber.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(row.Description.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(row.Package.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(row.DocumentType.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(row.Size.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(row.LatestRevision.ToUpper());
                        });

                        table.Cell().Element(c =>
                        {
                            c.Background(rowColor)
                             .Padding(5)
                             .AlignLeft()
                             .AlignMiddle()
                             .Text(row.LatestIssueDate?.ToString("dd/MM/yyyy") ?? "");
                        });

                        isAlternate = !isAlternate;
                    }
                }
            });
            
            // Add transmittal footer if this is a transmittal, but only on the last page
            /*
            if (isTransmittal)
            {
                column.Item().PageBreak();  // Force a page break before the confirmation
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
            */
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
        string? currentSubfolderFilterPath = null;
        if (SubfolderFilterCombo.Visibility == Visibility.Visible && SubfolderFilterCombo.SelectedItem is ComboBoxItem folderItem && folderItem.Tag is string folderPathTag && folderPathTag != "ALL")
        {
            currentSubfolderFilterPath = folderPathTag;
        }

        // Get all documents distributed on this date
        var docsForDate = _project.Documents
            .Where(d => d.DistributionCompanyIds.ContainsKey(selectedDate))
            .ToList();
        
        if (!docsForDate.Any())
        {
            DistributionInfoText.Text = "No recipients selected";
            return;
        }

        // Further filter by subfolder if applicable
        if (!string.IsNullOrEmpty(currentSubfolderFilterPath))
        {
            docsForDate = docsForDate.Where(d => d.RevisionHistory.Any(r => 
                r.Key.Date == selectedDate.Date && 
                !string.IsNullOrEmpty(r.Value.FilePath) && 
                string.Equals(Path.GetDirectoryName(r.Value.FilePath), currentSubfolderFilterPath, StringComparison.OrdinalIgnoreCase)
            )).ToList();
        }

        if (!docsForDate.Any()) {
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
            if (disposing)
            {
                PdfWebView?.Dispose();
            }
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

    private string GetDistributionTextForPdf(DateTime selectedDate, string? selectedSubfolderPath)
    {
        // Get all documents distributed on this date
        var docsForDate = _project.Documents
            .Where(d => d.DistributionCompanyIds.ContainsKey(selectedDate))
            .ToList();
        
        // Further filter by subfolder if applicable
        if (!string.IsNullOrEmpty(selectedSubfolderPath))
        {
            docsForDate = docsForDate.Where(d => d.RevisionHistory.Any(r => 
                r.Key.Date == selectedDate.Date &&
                !string.IsNullOrEmpty(r.Value.FilePath) && 
                string.Equals(Path.GetDirectoryName(r.Value.FilePath), selectedSubfolderPath, StringComparison.OrdinalIgnoreCase)
            )).ToList();
        }
        
        if (!docsForDate.Any())
        {
            return "No recipients selected for this specific issue/subfolder";
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
        
        // Format the distribution information text
        var distributionText = new System.Text.StringBuilder();
        
        foreach (var categoryGroup in companiesByCategory)
        {
            distributionText.AppendLine($"{categoryGroup.Key}: {string.Join(", ", categoryGroup.Select(c => c.Name))}");
        }
        
        return distributionText.ToString().TrimEnd();
    }

    private void DocumentGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F2 && DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDoc)
        {
            EditDocument(selectedDoc);
            e.Handled = true;
        }
    }

    private void RemoveDocumentButton_Click(object sender, RoutedEventArgs e)
    {
        // Assuming your DataGrid is named DocumentGrid
        if (DocumentGrid.SelectedItem is Models.DocumentMetadata selectedDocument)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to remove the entry for '{selectedDocument.DocumentNumber} - {selectedDocument.Description}'?\nThis will remove its record from the register and project data file.\nThe physical file will NOT be deleted from your computer.",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _project.Documents.Remove(selectedDocument);
                    _project.SaveProjectData(); // Save changes to project_data.json

                    // Refresh the grid by re-applying filters and sorting
                    FilterDocuments(); // Corrected: Call FilterDocuments()

                    // The DocumentGrid_SelectionChanged event will handle updating SelectedDocument,
                    // which in turn should update the RevisionTimeline via its binding.

                    MessageBox.Show("Document entry removed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing document entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Please select a document from the grid to remove.", "No Document Selected", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenChecking_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_project._currentBasePath) || _project._currentStorage == null)
        {
            MessageBox.Show("Load a project before opening check prints.", "Check Prints", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        new CheckingWindow(_project) { Owner = this }.Show();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutDialog = new AboutDialog();
        aboutDialog.Owner = this;
        aboutDialog.ShowDialog();
    }

    private void CollectLatestPdfs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_project._currentBasePath))
            {
                MessageBox.Show("No project loaded. Please scan a folder first.",
                    "Collect Latest PDFs", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var documentsToExport = (DocumentGrid.ItemsSource as IEnumerable<Models.DocumentMetadata>)?.ToList();
            if (documentsToExport == null || documentsToExport.Count == 0)
            {
                MessageBox.Show("No documents are currently visible in the grid.",
                    "Collect Latest PDFs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select where to create the latest drawings folder",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var exportResult = Helpers.FileOperations.CollectLatestPdfs(documentsToExport, dialog.SelectedPath, DateTime.Now);
            var summary = $"Copied {exportResult.CopiedCount} latest PDF(s) to:\n{exportResult.ExportFolderPath}";

            if (exportResult.SkippedCount > 0)
            {
                var skippedList = exportResult.SkippedFiles
                    .Take(20)
                    .Select(file => $"  • {file.DocumentNumber}\n    {file.Reason}");

                summary += $"\n\nSkipped {exportResult.SkippedCount} file(s):\n\n{string.Join("\n\n", skippedList)}";
                if (exportResult.SkippedCount > 20)
                {
                    summary += $"\n\n... and {exportResult.SkippedCount - 20} more.";
                }

                MessageBox.Show(summary, "Collect Latest PDFs - With Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(summary, "Collect Latest PDFs Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error collecting latest PDFs: {ex.Message}",
                "Collect Latest PDFs", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the currently displayed documents from the grid
            var documentsToExport = DocumentGrid.ItemsSource as IEnumerable<Models.DocumentMetadata>;
            if (documentsToExport == null || !documentsToExport.Any())
            {
                MessageBox.Show("No documents to export.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create filename based on whether a date filter is applied
            string fileNamePrefix = $"DrawingRegister_{_project.ProjectNumber ?? "Export"}_{DateTime.Now:yyyyMMdd}";
            if (IssueDateFilter.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content.ToString() != "All Dates")
            {
                fileNamePrefix = $"Transmittal_{_project.ProjectNumber ?? "Export"}_{selectedItem.Content.ToString().Replace("/", "")}";
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = fileNamePrefix
            };

            if (saveDialog.ShowDialog() == true)
            {
                // Get all unique issue dates for column headers
                var allIssueDates = documentsToExport
                    .SelectMany(d => d.RevisionHistory.Keys)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                using (var writer = new StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                {
                    // Write header row
                    var headers = new List<string>
                    {
                        "Document No",
                        "Description",
                        "Package",
                        "Type",
                        "Size",
                        "Latest Rev",
                        "Latest Date"
                    };

                    // Add date columns
                    foreach (var date in allIssueDates)
                    {
                        headers.Add(date.ToString("dd/MM/yyyy"));
                    }

                    writer.WriteLine(string.Join(",", headers.Select(EscapeCsvField)));

                    // Write data rows
                    foreach (var doc in documentsToExport)
                    {
                        var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();

                        var row = new List<string>
                        {
                            doc.DocumentNumber,
                            doc.Description,
                            doc.Package,
                            doc.DocumentType,
                            doc.Size,
                            latestRevision.Value?.Revision ?? "",
                            latestRevision.Key != default ? latestRevision.Key.ToString("dd/MM/yyyy") : ""
                        };

                        // Add revision for each date column
                        foreach (var date in allIssueDates)
                        {
                            var revision = doc.RevisionHistory.TryGetValue(date, out var revInfo)
                                ? revInfo.Revision
                                : "";
                            row.Add(revision);
                        }

                        writer.WriteLine(string.Join(",", row.Select(EscapeCsvField)));
                    }
                }

                MessageBox.Show($"CSV exported successfully to:\n{saveDialog.FileName}\n\n{documentsToExport.Count()} documents exported.",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Optionally open the file
                var result = MessageBox.Show("Would you like to open the CSV file now?", "Open File",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting CSV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // If the field contains a comma, newline, or quote, it needs to be quoted
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            // Escape quotes by doubling them and wrap in quotes
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
