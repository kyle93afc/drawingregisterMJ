using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DrawingRegister.App.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace DrawingRegister.App
{
    public partial class BatchEditDialog : Window
    {
        private readonly ProjectManager _project;
        private List<DocumentMetadata> _filteredDocuments = new();
        private Dictionary<string, List<DocumentMetadata>> _folderDocuments = new();
        private Dictionary<DateTime, Dictionary<string, List<DocumentMetadata>>> _dateAndFolderDocuments = new();
        private ObservableCollection<DistributionCompanyViewModel> _distributionCompanies = new();
        private DateTime _selectedIssueDate;

        public BatchEditDialog(ProjectManager project)
        {
            try
            {
                InitializeComponent();
                _project = project;
                
                // Initialize date combo
                PopulateIssueDates();
                
                // Initialize folder combo
                PopulateSubfolders();
                
                // Initialize date and folder combo
                PopulateDateAndFolderCombos();
                
                // Set initial filtered documents
                UpdateFilteredDocuments();
                
                // Set default values for edit fields
                if (PurposeCombo.Items.Count > 0)
                    PurposeCombo.SelectedIndex = 0;
                    
                if (MethodCombo.Items.Count > 0)
                    MethodCombo.SelectedIndex = 0;

                // Set default visibility
                DateSelectionPanel.Visibility = Visibility.Visible;
                FolderSelectionPanel.Visibility = Visibility.Collapsed;
                DateAndFolderSelectionPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing BatchEditDialog: {ex.Message}");
                System.Windows.MessageBox.Show($"Error initializing dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateIssueDates()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("==== POPULATING ISSUE DATES ====");
                
                if (IssueDateCombo != null)
                {
                    IssueDateCombo.Items.Clear();
                    
                    // Add all unique issue dates from documents
                    var issueDates = _project.Documents
                        .SelectMany(d => d.RevisionHistory.Keys)
                        .Select(d => d.Date) // Use only the date part, not time
                        .Distinct()
                        .OrderByDescending(d => d)
                        .ToList();
                        
                    // Debug output to verify dates
                    System.Diagnostics.Debug.WriteLine($"Found {issueDates.Count} unique issue dates");
                    
                    foreach (var date in issueDates)
                    {
                        string dateStr = date.ToString("dd/MM/yyyy");
                        var item = new ComboBoxItem { Content = dateStr, Tag = date };
                        IssueDateCombo.Items.Add(item);
                        
                        // Count documents for this date for debugging
                        int docCount = _project.Documents.Count(d => d.RevisionHistory.Any(r => r.Key.Date == date));
                        System.Diagnostics.Debug.WriteLine($"Date {dateStr} has {docCount} documents");
                    }
                    
                    if (IssueDateCombo.Items.Count > 0)
                    {
                        IssueDateCombo.SelectedIndex = 0;
                        
                        // Initialize the selected issue date
                        if (IssueDateCombo.SelectedItem is ComboBoxItem selectedItem && 
                            selectedItem.Tag is DateTime selectedDate)
                        {
                            _selectedIssueDate = selectedDate;
                            
                            // Initialize distribution companies for this date
                            PopulateDistributionCompanies();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No issue dates found");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("IssueDateCombo is null");
                }
                
                System.Diagnostics.Debug.WriteLine("==== ISSUE DATES POPULATED ====");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateIssueDates: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private void PopulateSubfolders()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("==== POPULATING SUBFOLDERS ====");
                
                if (SubfolderCombo != null)
                {
                    SubfolderCombo.Items.Clear();
                    _folderDocuments.Clear();
                    
                    // Group documents by subfolder
                    foreach (var doc in _project.Documents)
                    {
                        if (string.IsNullOrEmpty(doc.FilePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping document {doc.DocumentNumber} - no file path");
                            continue;
                        }
                            
                        try
                        {
                            string folder = Path.GetDirectoryName(doc.FilePath) ?? string.Empty;
                            if (string.IsNullOrEmpty(folder))
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping document {doc.DocumentNumber} - empty folder path");
                                continue;
                            }
                                
                            // Get the subfolder name (last directory in path)
                            string subfolderName = new DirectoryInfo(folder).Name;
                            
                            if (!_folderDocuments.ContainsKey(folder))
                            {
                                _folderDocuments[folder] = new List<DocumentMetadata>();
                                System.Diagnostics.Debug.WriteLine($"Created new folder entry for {folder}");
                            }
                                
                            _folderDocuments[folder].Add(doc);
                            
                            // Debug output
                            System.Diagnostics.Debug.WriteLine($"Added document {doc.DocumentNumber} to folder {folder}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error processing folder for document {doc.DocumentNumber}: {ex.Message}");
                            // Continue with next document
                        }
                    }
                    
                    // Debug output to verify folder grouping
                    System.Diagnostics.Debug.WriteLine($"Grouped documents into {_folderDocuments.Count} folders");
                    foreach (var folder in _folderDocuments.Keys)
                    {
                        System.Diagnostics.Debug.WriteLine($"Folder {folder} has {_folderDocuments[folder].Count} documents");
                    }
                    
                    // Add folders to combo box
                    foreach (var folder in _folderDocuments.Keys.OrderByDescending(f => 
                    {
                        try { return new DirectoryInfo(f).Name; }
                        catch { return string.Empty; }
                    }))
                    {
                        try
                        {
                            string displayName = new DirectoryInfo(folder).Name;
                            
                            // Try to extract date and description
                            var match = System.Text.RegularExpressions.Regex.Match(displayName, @"^(\d{8})(.*)");
                            if (match.Success)
                            {
                                string dateStr = match.Groups[1].Value;
                                string description = match.Groups[2].Value.Trim('-', '_', ' ');
                                
                                // Format as "YYYYMMDD - Description" if there is a description
                                if (!string.IsNullOrWhiteSpace(description))
                                {
                                    displayName = $"{dateStr} - {description}";
                                }
                                else
                                {
                                    displayName = dateStr;
                                }
                            }
                            
                            // Add to combo box with full folder path as Tag
                            var item = new ComboBoxItem { Content = displayName, Tag = folder };
                            SubfolderCombo.Items.Add(item);
                            
                            // Debug output to verify folder paths and document counts
                            System.Diagnostics.Debug.WriteLine($"Added folder: {displayName} with path {folder} containing {_folderDocuments[folder].Count} documents");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error adding folder to combo box: {ex.Message}");
                            // Continue with next folder
                        }
                    }
                    
                    if (SubfolderCombo.Items.Count > 0)
                    {
                        SubfolderCombo.SelectedIndex = 0;
                        System.Diagnostics.Debug.WriteLine($"Set SubfolderCombo.SelectedIndex to 0");
                        
                        if (SubfolderCombo.SelectedItem is ComboBoxItem selectedItem)
                        {
                            System.Diagnostics.Debug.WriteLine($"Selected folder item: Content={selectedItem.Content}, Tag={selectedItem.Tag}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No subfolders found");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("SubfolderCombo is null");
                }
                
                System.Diagnostics.Debug.WriteLine("==== SUBFOLDERS POPULATED ====");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateSubfolders: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private void PopulateDistributionCompanies()
        {
            _distributionCompanies.Clear();
            
            // Create a view model for each company
            foreach (var company in _project.DistributionManager.Companies)
            {
                _distributionCompanies.Add(new DistributionCompanyViewModel(company));
            }
            
            // Set up grouping by category
            var cvs = new CollectionViewSource();
            cvs.Source = _distributionCompanies;
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            
            // Set the ItemsControl's ItemsSource to the grouped view
            DistributionList.ItemsSource = cvs.View;
        }
        
        private void PopulateDateAndFolderCombos()
        {
            try
            {
                DateAndFolderDateCombo.Items.Clear();
                _dateAndFolderDocuments.Clear();

                var issueDates = _project.Documents
                    .SelectMany(d => d.RevisionHistory.Keys)
                    .Select(d => d.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                foreach (var date in issueDates)
                {
                    var item = new ComboBoxItem { Content = date.ToString("dd/MM/yyyy"), Tag = date };
                    DateAndFolderDateCombo.Items.Add(item);

                    // Store the physical folders found for this date, but DON'T link documents yet.
                    _dateAndFolderDocuments[date] = new Dictionary<string, List<DocumentMetadata>>(); 

                    string basePath = _project._currentBasePath;
                    System.Diagnostics.Debug.WriteLine($"PopulateDateAndFolderCombos: Using base path: {basePath}");

                    // Log existing FilePaths (can be removed later if desired)
                    System.Diagnostics.Debug.WriteLine("PopulateDateAndFolderCombos: Document FilePaths before processing date {date:dd/MM/yyyy}:");
                    foreach(var doc in _project.Documents)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - Doc: {doc.DocumentNumber}, FilePath: {(string.IsNullOrEmpty(doc.FilePath) ? "<NULL_OR_EMPTY>" : doc.FilePath)}");
                    }
                    System.Diagnostics.Debug.WriteLine("---- END Document FilePaths Logging ----");
                    
                    if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                    {
                        var subfolders = Directory.GetDirectories(basePath, $"{date:yyyyMMdd}*");
                        System.Diagnostics.Debug.WriteLine($"PopulateDateAndFolderCombos: Found {subfolders.Length} physical subfolders for date {date:dd/MM/yyyy} pattern {date:yyyyMMdd}*");

                        foreach (var folder in subfolders)
                        {
                            // Check if any document revision for this date actually exists in this physical folder
                            bool folderHasRelevantRevisions = false;
                            foreach (var doc in _project.Documents)
                            {
                                if (doc.RevisionHistory.Any(r => r.Key.Date == date && 
                                                              !string.IsNullOrEmpty(r.Value.FilePath) && 
                                                              r.Value.FilePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
                                {
                                    folderHasRelevantRevisions = true;
                                    break; // Found one, no need to check other docs for this folder
                                }
                            }

                            if (folderHasRelevantRevisions)
                            {
                                System.Diagnostics.Debug.WriteLine($"PopulateDateAndFolderCombos: Adding relevant folder key: {folder}");
                                // Add the folder path as a key only if it contains relevant revisions
                                if (!_dateAndFolderDocuments[date].ContainsKey(folder))
                                { 
                                    _dateAndFolderDocuments[date][folder] = new List<DocumentMetadata>(); // Initialize with empty list
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"PopulateDateAndFolderCombos: Skipping folder (no relevant revisions found): {folder}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Base path is invalid or does not exist: {basePath}");
                    }
                }

                if (DateAndFolderDateCombo.Items.Count > 0)
                {
                    DateAndFolderDateCombo.SelectedIndex = 0;
                    if (DateAndFolderDateCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is DateTime selectedDate)
                        PopulateDateAndFolderSubfolderCombo(selectedDate);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateDateAndFolderCombos: {ex.Message}");
            }
        }
        
        private void PopulateDateAndFolderSubfolderCombo(DateTime selectedDate)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"==== POPULATING DATE AND FOLDER SUBFOLDER COMBO FOR {selectedDate.ToString("dd/MM/yyyy")} ====");
                
                if (DateAndFolderSubfolderCombo != null)
                {
                    DateAndFolderSubfolderCombo.Items.Clear();
                    
                    if (_dateAndFolderDocuments.TryGetValue(selectedDate, out var foldersForDate))
                    {
                        // Debug output to verify folder grouping
                        System.Diagnostics.Debug.WriteLine($"Found {foldersForDate.Count} folders for date {selectedDate.ToString("dd/MM/yyyy")}");
                        
                        // Add folders to combo box
                        foreach (var folder in foldersForDate.Keys.OrderByDescending(f => 
                        {
                            try { return new DirectoryInfo(f).Name; }
                            catch { return string.Empty; }
                        }))
                        {
                            try
                            {
                                string folderName = new DirectoryInfo(folder).Name;
                                
                                // Try to extract date and description
                                var match = System.Text.RegularExpressions.Regex.Match(folderName, @"^(\d{8})(.*)");
                                string displayName = folderName;
                                
                                if (match.Success)
                                {
                                    string dateStr = match.Groups[1].Value;
                                    string description = match.Groups[2].Value.Trim('-', '_', ' ');
                                    
                                    // Format as "YYYYMMDD - Description" if there is a description
                                    if (!string.IsNullOrWhiteSpace(description))
                                    {
                                        displayName = $"{dateStr} - {description}";
                                    }
                                    else
                                    {
                                        displayName = dateStr;
                                    }
                                }
                                
                                // Add to combo box with full folder path as Tag
                                var item = new ComboBoxItem { Content = displayName, Tag = folder };
                                DateAndFolderSubfolderCombo.Items.Add(item);
                                
                                // Debug output to verify folder paths and document counts
                                int docCount = foldersForDate[folder].Count;
                                System.Diagnostics.Debug.WriteLine($"Added folder: {displayName} with path {folder} containing {docCount} documents");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error adding folder to combo box: {ex.Message}");
                                // Continue with next folder
                            }
                        }
                        
                        if (DateAndFolderSubfolderCombo.Items.Count > 0)
                        {
                            DateAndFolderSubfolderCombo.SelectedIndex = 0;
                            System.Diagnostics.Debug.WriteLine($"Set DateAndFolderSubfolderCombo.SelectedIndex to 0");
                            
                            if (DateAndFolderSubfolderCombo.SelectedItem is ComboBoxItem selectedItem)
                            {
                                System.Diagnostics.Debug.WriteLine($"Selected folder item: Content={selectedItem.Content}, Tag={selectedItem.Tag}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("No subfolders found for the selected date");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"No folders found for date {selectedDate.ToString("dd/MM/yyyy")}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("DateAndFolderSubfolderCombo is null");
                }
                
                System.Diagnostics.Debug.WriteLine("==== DATE AND FOLDER SUBFOLDER COMBO POPULATED ====");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateDateAndFolderSubfolderCombo: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private void DateAndFolderDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (DateAndFolderDateCombo != null && DateAndFolderDateCombo.SelectedItem is ComboBoxItem selectedItem && 
                    selectedItem.Tag is DateTime selectedDate)
                {
                    System.Diagnostics.Debug.WriteLine($"Date and folder date changed to {selectedDate.ToString("dd/MM/yyyy")}");
                    
                    // Update the subfolder combo for the selected date
                    PopulateDateAndFolderSubfolderCombo(selectedDate);
                    
                    // Update filtered documents
                    UpdateFilteredDocuments();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DateAndFolderDate_Changed: {ex.Message}");
                System.Windows.MessageBox.Show($"Error in DateAndFolderDate_Changed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void FilterOption_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Filter option changed: Date={FilterByDate?.IsChecked}, Folder={FilterByFolder?.IsChecked}, DateAndFolder={FilterByDateAndFolder?.IsChecked}");
                
                // Show/hide appropriate selection panels
                if (DateSelectionPanel != null && FilterByDate != null)
                    DateSelectionPanel.Visibility = FilterByDate.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                
                if (FolderSelectionPanel != null && FilterByFolder != null)
                    FolderSelectionPanel.Visibility = FilterByFolder.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                
                if (DateAndFolderSelectionPanel != null && FilterByDateAndFolder != null)
                    DateAndFolderSelectionPanel.Visibility = FilterByDateAndFolder.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                
                // Clear the preview grid first
                if (PreviewGrid != null)
                {
                    PreviewGrid.ItemsSource = null;
                    _filteredDocuments.Clear();
                }
                
                // Update selected issue date if date filter is active
                if (FilterByDate != null && FilterByDate.IsChecked == true && 
                    IssueDateCombo != null && IssueDateCombo.SelectedItem is ComboBoxItem selectedItem && 
                    selectedItem.Tag is DateTime selectedDate)
                {
                    _selectedIssueDate = selectedDate;
                }
                
                // Update filtered documents based on new filter option
                UpdateFilteredDocuments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in filter option change: {ex.Message}");
                System.Windows.MessageBox.Show($"Error in filter option change: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void FilterCriteria_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Log which control triggered the change
                if (sender is System.Windows.Controls.ComboBox comboBox)
                {
                    System.Diagnostics.Debug.WriteLine($"Selection changed in {comboBox.Name}, selected index: {comboBox.SelectedIndex}");
                    
                    if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"Selected item content: {selectedItem.Content}, tag: {selectedItem.Tag}");
                    }
                    
                    // Only update if the combo box has a valid selection
                    if (comboBox.SelectedIndex >= 0)
                    {
                        // Clear the preview grid first
                        if (PreviewGrid != null)
                        {
                            PreviewGrid.ItemsSource = null;
                            _filteredDocuments.Clear();
                        }
                        
                        // Update filtered documents based on new selection
                        UpdateFilteredDocuments();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FilterCriteria_Changed: {ex.Message}");
                System.Windows.MessageBox.Show($"Error in FilterCriteria_Changed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateFilteredDocuments()
        {
            try
            {
                _filteredDocuments.Clear();
                
                // Log the current filter state
                System.Diagnostics.Debug.WriteLine("==== FILTER STATE ====");
                System.Diagnostics.Debug.WriteLine($"FilterByDate: {FilterByDate?.IsChecked}");
                System.Diagnostics.Debug.WriteLine($"FilterByFolder: {FilterByFolder?.IsChecked}");
                System.Diagnostics.Debug.WriteLine($"FilterByDateAndFolder: {FilterByDateAndFolder?.IsChecked}");
                System.Diagnostics.Debug.WriteLine($"IssueDateCombo selected index: {IssueDateCombo?.SelectedIndex}");
                System.Diagnostics.Debug.WriteLine($"SubfolderCombo selected index: {SubfolderCombo?.SelectedIndex}");
                System.Diagnostics.Debug.WriteLine($"DateAndFolderDateCombo selected index: {DateAndFolderDateCombo?.SelectedIndex}");
                System.Diagnostics.Debug.WriteLine($"DateAndFolderSubfolderCombo selected index: {DateAndFolderSubfolderCombo?.SelectedIndex}");
                System.Diagnostics.Debug.WriteLine("=====================");
                
                if (FilterByDate?.IsChecked == true)
                {
                    // Filter by date
                    if (IssueDateCombo != null && IssueDateCombo.SelectedItem is ComboBoxItem selectedDateItem && selectedDateItem.Tag != null)
                    {
                        DateTime selectedDate;
                        
                        // Get the selected date, either from Tag or by parsing Content
                        if (selectedDateItem.Tag is DateTime tagDate)
                        {
                            selectedDate = tagDate.Date; // Use only the date part, not time
                        }
                        else if (selectedDateItem.Content is string selectedDateStr && 
                                DateTime.TryParseExact(selectedDateStr, "dd/MM/yyyy", null, 
                                System.Globalization.DateTimeStyles.None, out var parsedDate))
                        {
                            selectedDate = parsedDate.Date; // Use only the date part, not time
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Invalid date selection");
                            return;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Filtering by date: {selectedDate.ToString("dd/MM/yyyy")}");
                        
                        // Count documents before filtering for debugging
                        int totalDocs = _project.Documents.Count;
                        int docsWithRevisions = _project.Documents.Count(d => d.RevisionHistory.Any());
                        System.Diagnostics.Debug.WriteLine($"Total documents: {totalDocs}, with revisions: {docsWithRevisions}");
                        
                        // Log all document revision dates for debugging
                        System.Diagnostics.Debug.WriteLine("==== DOCUMENT REVISIONS ====");
                        foreach (var doc in _project.Documents)
                        {
                            if (doc.RevisionHistory.Any())
                            {
                                var revDates = string.Join(", ", doc.RevisionHistory.Keys.Select(d => d.ToString("dd/MM/yyyy HH:mm:ss")));
                                System.Diagnostics.Debug.WriteLine($"Doc {doc.DocumentNumber} has revisions on: {revDates}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Doc {doc.DocumentNumber} has NO revisions");
                            }
                        }
                        System.Diagnostics.Debug.WriteLine("==========================");
                        
                        foreach (var doc in _project.Documents)
                        {
                            // Find revisions that match the selected date (comparing only the date part)
                            var matchingRevisions = doc.RevisionHistory
                                .Where(r => r.Key.Date.Equals(selectedDate.Date)) // Ensure exact date comparison
                                .ToList();
                                
                            if (matchingRevisions.Any())
                            {
                                System.Diagnostics.Debug.WriteLine($"Found matching revision for doc {doc.DocumentNumber}");
                                
                                // Make sure the document has the current values from the latest revision
                                var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                                if (latestRevision.Value != null)
                                {
                                    doc.PurposeOfIssue = latestRevision.Value.Purpose;
                                    doc.MethodOfIssue = latestRevision.Value.Method;
                                    doc.IssuedBy = latestRevision.Value.IssuedBy;
                                }
                                
                                _filteredDocuments.Add(doc);
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Filtered documents count: {_filteredDocuments.Count}");
                    }
                    else
                    {
                        // No date selected, show a message
                        System.Diagnostics.Debug.WriteLine("No date selected or invalid date selection");
                        if (IssueDateCombo != null)
                            System.Diagnostics.Debug.WriteLine($"IssueDateCombo.SelectedItem: {IssueDateCombo.SelectedItem}");
                    }
                }
                else if (FilterByFolder?.IsChecked == true)
                {
                    // Filter by folder
                    if (SubfolderCombo?.SelectedItem is ComboBoxItem selectedFolderItem)
                    {
                        string folderPath = selectedFolderItem.Tag as string;
                        System.Diagnostics.Debug.WriteLine($"Filtering by folder path: {folderPath}");
                        
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            // Normalize the selected folder path
                            folderPath = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
                            System.Diagnostics.Debug.WriteLine($"Normalized folder path: {folderPath}");
                            
                            // Try to find the folder in the dictionary (case-sensitive)
                            if (_folderDocuments.TryGetValue(folderPath, out var docsInFolder))
                            {
                                System.Diagnostics.Debug.WriteLine($"Found {docsInFolder.Count} documents in folder {folderPath}");
                                
                                foreach (var doc in docsInFolder)
                                {
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Processing document {doc.DocumentNumber} from folder {folderPath}");
                                        
                                        // Normalize document path for comparison
                                        string docPath = !string.IsNullOrEmpty(doc.FilePath) 
                                            ? Path.GetDirectoryName(Path.GetFullPath(doc.FilePath))?.TrimEnd('\\', '/') 
                                            : null;
                                            
                                        System.Diagnostics.Debug.WriteLine($"Document path: {docPath}");
                                        
                                        if (string.Equals(docPath, folderPath, StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Make sure the document has the current values from the latest revision
                                            var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                                            if (latestRevision.Value != null)
                                            {
                                                doc.PurposeOfIssue = latestRevision.Value.Purpose;
                                                doc.MethodOfIssue = latestRevision.Value.Method;
                                                doc.IssuedBy = latestRevision.Value.IssuedBy;
                                            }
                                            
                                            _filteredDocuments.Add(doc);
                                            System.Diagnostics.Debug.WriteLine($"Added document {doc.DocumentNumber} to filtered list");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error processing document {doc.DocumentNumber}: {ex.Message}");
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"No documents found in folder dictionary for path: {folderPath}");
                                
                                // Fallback: Search through all documents for matching paths
                                foreach (var doc in _project.Documents)
                                {
                                    try
                                    {
                                        if (string.IsNullOrEmpty(doc.FilePath))
                                            continue;
                                            
                                        string docPath = Path.GetDirectoryName(Path.GetFullPath(doc.FilePath))?.TrimEnd('\\', '/');
                                        
                                        if (string.Equals(docPath, folderPath, StringComparison.OrdinalIgnoreCase))
                                        {
                                            var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                                            if (latestRevision.Value != null)
                                            {
                                                doc.PurposeOfIssue = latestRevision.Value.Purpose;
                                                doc.MethodOfIssue = latestRevision.Value.Method;
                                                doc.IssuedBy = latestRevision.Value.IssuedBy;
                                            }
                                            
                                            _filteredDocuments.Add(doc);
                                            System.Diagnostics.Debug.WriteLine($"Added document {doc.DocumentNumber} to filtered list (from fallback search)");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error in fallback search for document {doc.DocumentNumber}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Selected folder path is null or empty");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No folder selected in SubfolderCombo");
                    }
                }
                else if (FilterByDateAndFolder?.IsChecked == true)
                {
                    // Filter by date and folder
                    if (DateAndFolderDateCombo != null && DateAndFolderDateCombo.SelectedItem is ComboBoxItem selectedDateItem && 
                        DateAndFolderSubfolderCombo != null && DateAndFolderSubfolderCombo.SelectedItem is ComboBoxItem selectedFolderItem)
                    {
                        if (selectedDateItem.Tag is DateTime selectedDate && selectedFolderItem.Tag is string folderPath)
                        {
                            System.Diagnostics.Debug.WriteLine($"Filtering by date {selectedDate.ToString("dd/MM/yyyy")} and folder {folderPath}");
                            
                            if (_dateAndFolderDocuments.TryGetValue(selectedDate, out var foldersForDate))
                            {
                                // ---- START NEW LOGGING ----
                                System.Diagnostics.Debug.WriteLine($"UpdateFilteredDocuments: Keys available in foldersForDate for {selectedDate:dd/MM/yyyy}:");
                                foreach (var key in foldersForDate.Keys)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  - Key: {key} (Has {foldersForDate[key].Count} docs)");
                                }
                                System.Diagnostics.Debug.WriteLine("---- END NEW LOGGING ----");
                                // ---- END NEW LOGGING ----

                                if (foldersForDate.TryGetValue(folderPath, out var docsInFolder))
                                {
                                    // Replace dictionary lookup with direct iteration and filtering logic:
                                    System.Diagnostics.Debug.WriteLine($"UpdateFilteredDocuments: Dictionary key found. Iterating documents for {selectedDate:dd/MM/yyyy} and folder {folderPath}");
                                    foreach (var doc in _project.Documents)
                                    {
                                        var revisionOnDate = doc.RevisionHistory
                                            .FirstOrDefault(r => r.Key.Date == selectedDate.Date);

                                        if (revisionOnDate.Value != null && !string.IsNullOrEmpty(revisionOnDate.Value.FilePath))
                                        {
                                            try
                                            {
                                                string revisionFolderPath = Path.GetDirectoryName(revisionOnDate.Value.FilePath);
                                                if (string.Equals(revisionFolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"  -> Match found: Doc {doc.DocumentNumber}, RevDate {revisionOnDate.Key}, RevPath {revisionOnDate.Value.FilePath}");
                                                    var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).First();
                                                    doc.PurposeOfIssue = latestRevision.Value.Purpose;
                                                    doc.MethodOfIssue = latestRevision.Value.Method;
                                                    doc.IssuedBy = latestRevision.Value.IssuedBy;
                                                    _filteredDocuments.Add(doc);
                                                }
                                            }
                                            catch (Exception pathEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Error getting directory for {revisionOnDate.Value.FilePath}: {pathEx.Message}");
                                            }
                                        }
                                    }
                                    _selectedIssueDate = selectedDate; 
                                    System.Diagnostics.Debug.WriteLine($"UpdateFilteredDocuments: Iteration complete. Found {_filteredDocuments.Count} documents.");
                                }
                                else
                                {
                                     // This case should ideally not happen if PopulateDateAndFolderCombos added the physical folder key
                                    System.Diagnostics.Debug.WriteLine($"Dictionary key {folderPath} NOT found for date {selectedDate.ToString("dd/MM/yyyy")}. This indicates an issue in PopulateDateAndFolderCombos.");
                                }
                            }
                            else
                            { 
                                System.Diagnostics.Debug.WriteLine($"No folders found in dictionary for date {selectedDate.ToString("dd/MM/yyyy")}. Check PopulateDateAndFolderCombos.");
                            }
                        }
                        else
                        { 
                            System.Diagnostics.Debug.WriteLine("Selected date or folder path is invalid.");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No date or folder selected for date and folder filter");
                    }
                }
                else
                {
                    // Neither date nor folder filter is selected, show all documents
                    System.Diagnostics.Debug.WriteLine("No filter selected, showing all documents");
                    foreach (var doc in _project.Documents)
                    {
                        var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                        if (latestRevision.Value != null)
                        {
                            doc.PurposeOfIssue = latestRevision.Value.Purpose;
                            doc.MethodOfIssue = latestRevision.Value.Method;
                            doc.IssuedBy = latestRevision.Value.IssuedBy;
                        }
                        
                        _filteredDocuments.Add(doc);
                    }
                }
                
                // Update preview grid
                if (PreviewGrid != null)
                {
                    PreviewGrid.ItemsSource = null; // Clear first to force refresh
                    PreviewGrid.ItemsSource = _filteredDocuments;
                    System.Diagnostics.Debug.WriteLine($"Updated preview grid with {_filteredDocuments.Count} documents");
                    
                    // Force the grid to refresh its view
                    if (PreviewGrid.Items != null)
                    {
                        System.Windows.Data.CollectionViewSource.GetDefaultView(PreviewGrid.ItemsSource)?.Refresh();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("PreviewGrid is null");
                }
                
                // Try to detect common values
                DetectCommonValues();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating filtered documents: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Windows.MessageBox.Show($"Error updating filtered documents: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void DetectCommonValues()
        {
            try
            {
                if (_filteredDocuments.Count == 0)
                    return;
                    
                // Find common purpose
                var commonPurpose = _filteredDocuments
                    .Select(d => d.PurposeOfIssue)
                    .GroupBy(p => p)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;
                    
                // Find common method
                var commonMethod = _filteredDocuments
                    .Select(d => d.MethodOfIssue)
                    .GroupBy(m => m)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;
                    
                // Find common issued by
                var commonIssuedBy = _filteredDocuments
                    .Select(d => d.IssuedBy)
                    .GroupBy(i => i)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;

                // Set detected values in UI
                if (!string.IsNullOrEmpty(commonPurpose) && PurposeCombo != null)
                {
                    foreach (ComboBoxItem item in PurposeCombo.Items)
                    {
                        if (item.Content?.ToString()?.StartsWith(commonPurpose.Substring(0, 1)) == true)
                        {
                            PurposeCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(commonMethod) && MethodCombo != null)
                {
                    foreach (ComboBoxItem item in MethodCombo.Items)
                    {
                        if (item.Content?.ToString()?.StartsWith(commonMethod.Substring(0, 1)) == true)
                        {
                            MethodCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(commonIssuedBy) && IssuedByTextBox != null)
                {
                    IssuedByTextBox.Text = commonIssuedBy;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error detecting common values: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_filteredDocuments.Count == 0)
                {
                    System.Windows.MessageBox.Show("No documents selected to update.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Get values to apply
                string purpose = null;
                string method = null;
                string issuedBy = null;
                
                if (UpdatePurposeCheck != null && UpdatePurposeCheck.IsChecked == true && 
                    PurposeCombo != null && PurposeCombo.SelectedItem is ComboBoxItem purposeItem)
                {
                    purpose = purposeItem.Content?.ToString();
                    if (!string.IsNullOrEmpty(purpose) && purpose.Contains(" - "))
                        purpose = purpose.Split(new[] { " - " }, StringSplitOptions.None)[0];
                }
                
                if (UpdateMethodCheck != null && UpdateMethodCheck.IsChecked == true && 
                    MethodCombo != null && MethodCombo.SelectedItem is ComboBoxItem methodItem)
                {
                    method = methodItem.Content?.ToString();
                    if (!string.IsNullOrEmpty(method) && method.Contains(" - "))
                        method = method.Split(new[] { " - " }, StringSplitOptions.None)[0];
                }
                
                if (UpdateIssuedByCheck != null && UpdateIssuedByCheck.IsChecked == true &&
                    IssuedByTextBox != null && !string.IsNullOrEmpty(IssuedByTextBox.Text))
                {
                    issuedBy = IssuedByTextBox.Text.Trim().ToUpper();
                }

                if (purpose == null && method == null && issuedBy == null)
                {
                    System.Windows.MessageBox.Show("Please select at least one property to update.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Apply changes
                int updatedCount = 0;
                DateTime? selectedDate = null;
                
                // If filtering by date, get the selected date
                if (FilterByDate != null && FilterByDate.IsChecked == true && 
                    IssueDateCombo != null && IssueDateCombo.SelectedItem is ComboBoxItem selectedDateItem)
                {
                    string selectedDateStr = selectedDateItem.Content?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(selectedDateStr) &&
                        DateTime.TryParseExact(selectedDateStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                    {
                        selectedDate = parsedDate.Date;
                    }
                }
                
                // Get selected companies
                var selectedCompanyIds = _distributionCompanies
                    .Where(c => c.IsSelected)
                    .Select(c => c.Id)
                    .ToList();
                
                foreach (var doc in _filteredDocuments)
                {
                    bool docUpdated = false;
                    
                    // If filtering by date, update only revisions on that date
                    if (selectedDate.HasValue)
                    {
                        var matchingRevisions = doc.RevisionHistory
                            .Where(r => r.Key.Date == selectedDate.Value)
                            .ToList();
                            
                        foreach (var revision in matchingRevisions)
                        {
                            var revInfo = revision.Value;
                            if (revInfo == null) continue;
                            
                            bool revUpdated = false;
                            
                            // Update revision properties
                            if (purpose != null)
                            {
                                revInfo.Purpose = purpose;
                                revUpdated = true;
                            }
                            
                            if (method != null)
                            {
                                revInfo.Method = method;
                                revUpdated = true;
                            }
                            
                            if (issuedBy != null)
                            {
                                revInfo.IssuedBy = issuedBy;
                                revUpdated = true;
                            }
                            
                            // Update document's current values if this is the latest revision
                            if (revUpdated && doc.RevisionHistory.Max(r => r.Key) == revision.Key)
                            {
                                if (purpose != null)
                                    doc.PurposeOfIssue = purpose;

                                if (method != null)
                                    doc.MethodOfIssue = method;

                                if (issuedBy != null)
                                    doc.IssuedBy = issuedBy;

                                docUpdated = true;
                            }
                        }
                    }
                    else
                    {
                        // Update the latest revision and document properties
                        var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                        if (latestRevision.Value != null)
                        {
                            if (purpose != null)
                            {
                                latestRevision.Value.Purpose = purpose;
                                doc.PurposeOfIssue = purpose;
                                docUpdated = true;
                            }
                            
                            if (method != null)
                            {
                                latestRevision.Value.Method = method;
                                doc.MethodOfIssue = method;
                                docUpdated = true;
                            }
                            
                            if (issuedBy != null)
                            {
                                latestRevision.Value.IssuedBy = issuedBy;
                                doc.IssuedBy = issuedBy;
                                docUpdated = true;
                            }
                        }
                    }

                    // Update distribution if any companies are selected
                    if (selectedCompanyIds.Any())
                    {
                        DateTime distributionTargetDate;

                        if (selectedDate.HasValue)
                        {
                            // When filtering by a specific issue date (selectedDate.Value),
                            // use that date for the distribution update, as it corresponds
                            // to the specific revision(s) being modified.
                            distributionTargetDate = selectedDate.Value;
                        }
                        else
                        {
                            // When not filtering by a specific issue date (e.g., filtering by folder or no filter),
                            // the property updates (Purpose, Method, IssuedBy) target the latest revision.
                            // Therefore, the distribution should also apply to the date of that latest revision.
                            var latestRevision = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                            if (latestRevision.Key != default(DateTime)) // Check if a valid revision date exists
                            {
                                distributionTargetDate = latestRevision.Key;
                            }
                            else
                            {
                                // This document has no revisions. If "updating a previous issue", this is an edge case.
                                // Fallback to _selectedIssueDate (the general batch context date from UI).
                                // This might occur if batch editing is used to add initial issue information (including distribution)
                                // to documents that previously had no issue records.
                                System.Diagnostics.Debug.WriteLine($"Warning: Document {doc.DocumentNumber} has no revisions. Distribution will use batch context date: {_selectedIssueDate.ToShortDateString()}");
                                distributionTargetDate = _selectedIssueDate;
                            }
                        }
                        doc.SetCompanyDistributions(selectedCompanyIds, distributionTargetDate);
                    }
                    
                    if (docUpdated)
                        updatedCount++;
                }
                
                // Save changes
                _project.SaveProjectData();
                
                System.Windows.MessageBox.Show($"Successfully updated {updatedCount} documents.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var company in _distributionCompanies)
            {
                company.IsSelected = true;
            }
        }
        
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var company in _distributionCompanies)
            {
                company.IsSelected = false;
            }
        }
    }
} 