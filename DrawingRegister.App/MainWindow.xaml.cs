using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DrawingRegister.App.Data;
using DrawingRegister.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DrawingRegister.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DocumentContext _context;
    private string _searchText = string.Empty;
    private ObservableCollection<DocumentMetadata> _documents;
    private List<DateTime> _allIssueDates = new();

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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _context = new DocumentContext();
        InitializeDatabase();
        _documents = new ObservableCollection<DocumentMetadata>();
        LoadDocuments();
    }

    private void InitializeDatabase()
    {
        try
        {
            _context.Database.EnsureDeleted(); // For testing, remove in production
            _context.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Database initialization error: {ex.Message}\n\nInner Exception: {ex.InnerException?.Message}", 
                "Database Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    private void LoadDocuments()
    {
        _documents.Clear();
        var docs = _context.Documents.OrderBy(d => d.DocumentNumber).ToList();
        foreach (var doc in docs)
        {
            _documents.Add(doc);
        }
        DocumentGrid.ItemsSource = _documents;
    }

    private void FilterDocuments()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            DocumentGrid.ItemsSource = _documents;
            return;
        }

        var filtered = _documents.Where(d =>
            d.DocumentNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            d.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            d.Package.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        DocumentGrid.ItemsSource = filtered;
    }

    private void ImportDocuments_Click(object sender, RoutedEventArgs e)
    {
        var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select the drawings folder",
            UseDescriptionForTitle = true
        };

        if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var path = folderBrowserDialog.SelectedPath;
            ImportDocumentsFromFolder(path);
        }
    }

    private void ImportDocumentsFromFolder(string folderPath)
    {
        try
        {
            var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.AllDirectories);
            int importCount = 0;
            var errors = new List<string>();

            // Get project info from UI
            var projectNumber = ProjectNoBox.Text;
            var projectName = ProjectNameBox.Text;
            var discipline = DisciplineBox.Text;
            var regNo = RegNoBox.Text;
            var clientNo = ClientNoBox.Text;

            foreach (var filePath in pdfFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileName = Path.GetFileNameWithoutExtension(filePath);

                    // New regex-based parsing to handle 5-6 digit project numbers and optional revision/description
                    var regex = new Regex(@"^(?<projectNo>\d{5,6})-(?<code1>[^-]+)-(?<volume>[^-]+)-(?<code2>[^-]+)-(?<docType>[^-]+)-(?<docDiscipline>[^-]+)-(?<package>[^-]+)(-(?<extra>.+))?$");
                    var match = regex.Match(fileName);
                    if (!match.Success)
                    {
                        errors.Add($"Filename does not match expected format: {fileName}");
                        continue;
                    }

                    var projectNo = match.Groups["projectNo"].Value;
                    var documentNumber = $"{match.Groups["projectNo"].Value}-{match.Groups["code1"].Value}-{match.Groups["volume"].Value}-{match.Groups["code2"].Value}-{match.Groups["docType"].Value}-{match.Groups["docDiscipline"].Value}-{match.Groups["package"].Value}";

                    string revision = "A";
                    string description = "";
                    if (match.Groups["extra"].Success)
                    {
                        var extraParts = match.Groups["extra"].Value.Split('-');
                        if (extraParts.Length > 0 && extraParts.Last().Length == 1 && char.IsLetter(extraParts.Last()[0]))
                        {
                            revision = extraParts.Last();
                            if (extraParts.Length > 1)
                                description = string.Join(" ", extraParts.Take(extraParts.Length - 1)).Replace("_", " ").Trim();
                        }
                        else
                        {
                            description = string.Join(" ", extraParts).Replace("_", " ").Trim();
                        }
                    }

                    // Existing issue date extraction based on folder name remains below
                    var folderName = Path.GetFileName(Path.GetDirectoryName(filePath));
                    DateTime issueDate;
                    if (!DateTime.TryParseExact(folderName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out issueDate))
                    {
                        issueDate = fileInfo.CreationTime;
                    }

                    // Create document metadata using parsed values
                    var doc = new DocumentMetadata
                    {
                        DocumentNumber = documentNumber,
                        FilePath = filePath,
                        ProjectNumber = projectNo,
                        ProjectName = projectName,
                        Discipline = match.Groups["docDiscipline"].Value,
                        Package = match.Groups["package"].Value,
                        DocumentType = match.Groups["docType"].Value,
                        RegisterNumber = regNo,
                        ClientNumber = clientNo,
                        Description = string.IsNullOrWhiteSpace(description) ? ParseDescription(fileName) : description,
                        Size = DetermineDrawingSize(filePath)
                    };

                    // Add revision info
                    var revInfo = new RevisionInfo
                    {
                        Revision = revision,
                        Purpose = DeterminePurpose(filePath),
                        Method = "E",
                        IssuedBy = DetermineIssuedBy(filePath),
                        IsDistributed = true
                    };

                    doc.RevisionHistory[issueDate] = revInfo;

                    var existingDoc = _context.Documents.FirstOrDefault(d => d.DocumentNumber == doc.DocumentNumber);
                    if (existingDoc != null)
                    {
                        // Update existing document with new revision
                        existingDoc.RevisionHistory[issueDate] = revInfo;
                        _context.Update(existingDoc);
                    }
                    else
                    {
                        _context.Documents.Add(doc);
                    }
                    
                    importCount++;
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    errors.Add($"Error processing {filePath}: {ex.Message}");
                }
            }

            LoadDocuments();
            UpdateIssueGrid();

            if (errors.Any())
            {
                var errorMessage = string.Join("\n", errors);
                System.Windows.MessageBox.Show($"Imported {importCount} documents with some errors:\n\n{errorMessage}", 
                    "Import Partially Complete", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
            }
            else
            {
                System.Windows.MessageBox.Show($"Successfully imported {importCount} new documents.", 
                    "Import Complete", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error importing documents: {ex.Message}\n\nInner Exception: {ex.InnerException?.Message}", 
                "Import Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    private string DeterminePurpose(string filePath)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(filePath))?.ToLower();
        if (folder?.Contains("construction") == true) return "C";
        if (folder?.Contains("tender") == true) return "T";
        if (folder?.Contains("warrant") == true) return "W";
        if (folder?.Contains("planning") == true) return "P";
        if (folder?.Contains("information") == true) return "I";
        if (folder?.Contains("approval") == true) return "A";
        if (folder?.Contains("draft") == true) return "D";
        return "I"; // Default to Information
    }

    private string DetermineIssuedBy(string filePath)
    {
        // Extract initials from path or metadata if available
        // For now, return default
        return "MJ";
    }

    private void UpdateIssueGrid()
    {
        _allIssueDates = _documents
            .SelectMany(d => d.RevisionHistory.Keys)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        IssueDatesControl.ItemsSource = _allIssueDates;

        var purposes = _allIssueDates.Select(d => 
            _documents
                .Where(doc => doc.RevisionHistory.ContainsKey(d))
                .Select(doc => doc.RevisionHistory[d].Purpose)
                .FirstOrDefault() ?? "");
        PurposeControl.ItemsSource = purposes;

        var methods = _allIssueDates.Select(d => 
            _documents
                .Where(doc => doc.RevisionHistory.ContainsKey(d))
                .Select(doc => doc.RevisionHistory[d].Method)
                .FirstOrDefault() ?? "");
        MethodControl.ItemsSource = methods;

        var issuedBy = _allIssueDates.Select(d => 
            _documents
                .Where(doc => doc.RevisionHistory.ContainsKey(d))
                .Select(doc => doc.RevisionHistory[d].IssuedBy)
                .FirstOrDefault() ?? "");
        IssuedByControl.ItemsSource = issuedBy;
    }

    private void RefreshView_Click(object sender, RoutedEventArgs e)
    {
        LoadDocuments();
    }

    private void DocumentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentGrid.SelectedItem is DocumentMetadata selectedDoc)
        {
            try
            {
                if (File.Exists(selectedDoc.FilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedDoc.FilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show("File not found: " + selectedDoc.FilePath, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public List<DateTime> GetAllIssueDates()
    {
        return _allIssueDates;
    }

    private string ParseDescription(string fileName)
    {
        try
        {
            // Split by hyphen and get the description part
            var parts = fileName.Split('-');
            if (parts.Length > 8)
            {
                var descParts = parts.Skip(8).ToList();
                if (descParts.Count > 0 && descParts.Last().Length == 1 && char.IsLetter(descParts.Last()[0]))
                {
                    descParts.RemoveAt(descParts.Count - 1);
                }
                return string.Join(" ", descParts).Replace("_", " ").Trim();
            }

            // If no description found in filename, try to parse from standard naming
            if (parts.Length >= 7)
            {
                var type = parts[4]; // DR, SK, etc.
                var discipline = parts[5]; // S, A, etc.
                var category = parts[6]; // 00, 20, etc.

                // Map common types and categories to descriptions
                var descriptions = new Dictionary<string, string>
                {
                    {"00", "GENERAL"},
                    {"20", "STRUCTURAL"},
                    {"28", "STEELWORK"},
                    {"16", "FOUNDATION"},
                    {"23", "SLAB"},
                };

                if (descriptions.TryGetValue(category, out var categoryDesc))
                {
                    return $"{categoryDesc} {GetDrawingTypeDescription(type)}";
                }
            }

            return "GENERAL ARRANGEMENT";
        }
        catch
        {
            return "GENERAL ARRANGEMENT";
        }
    }

    private string GetDrawingTypeDescription(string type)
    {
        switch (type.ToUpper())
        {
            case "DR": return "DRAWING";
            case "SK": return "SKETCH";
            case "SP": return "SPECIFICATION";
            default: return type;
        }
    }

    private string DetermineDrawingSize(string filePath)
    {
        try
        {
            // Try to get size from filename
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var parts = fileName.Split('-');
            
            // Check if size is encoded in the filename
            foreach (var part in parts)
            {
                if (part.StartsWith("A") && part.Length <= 2)
                {
                    return part; // A0, A1, A2, etc.
                }
            }

            // If not found in filename, try to determine from standard sizes
            using (var stream = File.OpenRead(filePath))
            {
                // TODO: Implement PDF size detection if needed
                // For now, return default size
                return "A1";
            }
        }
        catch
        {
            return "A1"; // Default to A1 if size cannot be determined
        }
    }
}