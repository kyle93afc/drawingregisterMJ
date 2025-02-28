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
        
        private void FilterOption_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Filter option changed: Date={FilterByDate?.IsChecked}, Folder={FilterByFolder?.IsChecked}");
                
                // Show/hide appropriate selection panels
                if (DateSelectionPanel != null && FilterByDate != null)
                    DateSelectionPanel.Visibility = FilterByDate.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                
                if (FolderSelectionPanel != null && FilterByFolder != null)
                    FolderSelectionPanel.Visibility = FilterByFolder.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                
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
                System.Diagnostics.Debug.WriteLine($"IssueDateCombo selected index: {IssueDateCombo?.SelectedIndex}");
                System.Diagnostics.Debug.WriteLine($"SubfolderCombo selected index: {SubfolderCombo?.SelectedIndex}");
                System.Diagnostics.Debug.WriteLine("=====================");
                
                if (FilterByDate != null && FilterByDate.IsChecked == true)
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
                else if (FilterByFolder != null && FilterByFolder.IsChecked == true)
                {
                    // Filter by folder
                    if (SubfolderCombo != null && SubfolderCombo.SelectedItem is ComboBoxItem selectedFolderItem)
                    {
                        string folderPath = selectedFolderItem.Tag as string;
                        
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            // Try to find the folder in the dictionary (case-sensitive)
                            if (_folderDocuments.TryGetValue(folderPath, out var docsInFolder))
                            {
                                System.Diagnostics.Debug.WriteLine($"Found {docsInFolder.Count} documents in folder {folderPath}");
                                
                                foreach (var doc in docsInFolder)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Adding document {doc.DocumentNumber} from folder {folderPath}");
                                    
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
                            else
                            {
                                // Try case-insensitive match as fallback
                                var matchingKey = _folderDocuments.Keys.FirstOrDefault(k => 
                                    string.Equals(k, folderPath, StringComparison.OrdinalIgnoreCase));
                                    
                                if (matchingKey != null && _folderDocuments.TryGetValue(matchingKey, out var docsInMatchingFolder))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Found case-insensitive match: {matchingKey}");
                                    
                                    foreach (var doc in docsInMatchingFolder)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Adding document {doc.DocumentNumber} from folder {matchingKey}");
                                        
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
                            }
                        }
                    }
                    else
                    {
                        // No folder selected, show a message
                        System.Diagnostics.Debug.WriteLine("No folder selected or invalid folder selection");
                        if (SubfolderCombo != null)
                            System.Diagnostics.Debug.WriteLine($"SubfolderCombo.SelectedItem: {SubfolderCombo.SelectedItem}");
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
                        doc.SetCompanyDistributions(selectedCompanyIds, _selectedIssueDate);
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