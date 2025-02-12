using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Text.Json;

namespace DrawingRegister.App.Models;

public class ProjectManager : INotifyPropertyChanged
{
    private const string STORAGE_FILENAME = "project_data.json";
    private string _currentBasePath = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _projectNumber = string.Empty;
    private string _projectName = string.Empty;
    private string _discipline = string.Empty;
    private string _registerNumber = string.Empty;
    private string _clientNumber = string.Empty;

    public ObservableCollection<DocumentMetadata> Documents { get; } = new();
    public List<DateTime> IssueDates { get; } = new();

    public string ProjectNumber
    {
        get => _projectNumber;
        set
        {
            _projectNumber = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProjectNumber)));
        }
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            _projectName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProjectName)));
        }
    }

    public string Discipline
    {
        get => _discipline;
        set
        {
            _discipline = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Discipline)));
        }
    }

    public string RegisterNumber
    {
        get => _registerNumber;
        set
        {
            _registerNumber = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RegisterNumber)));
        }
    }

    public string ClientNumber
    {
        get => _clientNumber;
        set
        {
            _clientNumber = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClientNumber)));
        }
    }

    public void ImportDocuments(string folderPath)
    {
        _currentBasePath = folderPath;
        var storageFile = Path.Combine(folderPath, STORAGE_FILENAME);
        ProjectStorage? existingData = null;

        // Try to load existing data
        if (File.Exists(storageFile))
        {
            try
            {
                var json = File.ReadAllText(storageFile);
                existingData = JsonSerializer.Deserialize<ProjectStorage>(json);
                
                // Load existing project info
                if (existingData != null)
                {
                    ProjectNumber = existingData.ProjectNumber;
                    ProjectName = existingData.ProjectName;
                    Discipline = existingData.Discipline;
                    RegisterNumber = existingData.RegisterNumber;
                    ClientNumber = existingData.ClientNumber;

                    // Load existing documents
                    foreach (var doc in existingData.Documents)
                    {
                        var metadata = new DocumentMetadata
                        {
                            DocumentNumber = doc.DocumentNumber,
                            Description = doc.Description,
                            Package = doc.Package,
                            DocumentType = doc.DocumentType,
                            Size = doc.Size,
                            ProjectNumber = ProjectNumber,
                            ProjectName = ProjectName,
                            Discipline = Discipline,
                            RegisterNumber = RegisterNumber,
                            ClientNumber = ClientNumber
                        };

                        foreach (var rev in doc.RevisionHistory)
                        {
                            metadata.RevisionHistory[rev.Key] = new RevisionInfo
                            {
                                Revision = rev.Value.Revision,
                                Purpose = rev.Value.Purpose,
                                Method = rev.Value.Method,
                                IssuedBy = rev.Value.IssuedBy,
                                IsDistributed = rev.Value.IsDistributed,
                                FilePath = rev.Value.FilePath
                            };
                        }

                        Documents.Add(metadata);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with fresh scan
                System.Diagnostics.Debug.WriteLine($"Error loading storage: {ex.Message}");
            }
        }

        // Scan for new files
        var subDirectories = Directory.GetDirectories(folderPath);
        if (!subDirectories.Any())
        {
            throw new Exception("No subdirectories found. Please select a folder containing project subfolders.");
        }

        // Get all subdirectories that match the date format YYYYMMDD
        var dateDirectories = subDirectories
            .Where(dir => 
            {
                var dirName = Path.GetFileName(dir)?.Split('_')[0];
                return dirName != null && DateTime.TryParseExact(dirName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out _);
            })
            .ToList();

        if (!dateDirectories.Any())
        {
            throw new Exception("No valid date folders found. Folders should be named in format YYYYMMDD.");
        }

        // Only get PDF files from date directories
        var pdfFiles = dateDirectories
            .SelectMany(dir => Directory.GetFiles(dir, "*.pdf"))
            .ToList();

        if (!pdfFiles.Any())
        {
            throw new Exception("No PDF files found in the date folders.");
        }

        // Try to detect project number from the first valid PDF name
        string? detectedProjectNo = null;
        foreach (var pdf in pdfFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(pdf);
            var regex = new Regex(@"^(?<projectNo>\d{5,6})-");
            var match = regex.Match(fileName);
            if (match.Success)
            {
                detectedProjectNo = match.Groups["projectNo"].Value;
                break;
            }
        }

        if (string.IsNullOrEmpty(detectedProjectNo))
        {
            throw new Exception("Could not detect project number from PDF filenames. Please ensure files follow the naming convention.");
        }

        // Update project number
        ProjectNumber = detectedProjectNo;

        Documents.Clear();
        IssueDates.Clear();

        // First collect all issue dates from folder names
        var allIssueDates = dateDirectories
            .Select(dir => 
            {
                var dirName = Path.GetFileName(dir)?.Split('_')[0];
                DateTime.TryParseExact(dirName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date);
                return date;
            })
            .OrderBy(d => d)
            .ToList();

        IssueDates.AddRange(allIssueDates);

        foreach (var filePath in pdfFiles)
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Get issue date from parent folder name
            var parentFolder = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (!DateTime.TryParseExact(parentFolder?.Split('_')[0], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var issueDate))
            {
                // Skip files not in properly named date folders
                continue;
            }

            // Updated regex pattern to better handle drawing numbers and revisions
            var regex = new Regex(@"^(?<projectNo>\d{5,6})-(?<code1>[^-]+)-(?<volume>[^-]+)-(?<code2>[^-]+)-(?<docType>[^-]+)-(?<docDiscipline>[^-]+)-(?<package>\d+)(-(?<number>\d+))?(-(?<revision>[A-Z]))?(-(?<description>.+))?$");
            var match = regex.Match(fileName);
            
            if (!match.Success)
            {
                continue;
            }

            // Verify project number matches
            var fileProjectNo = match.Groups["projectNo"].Value;
            if (fileProjectNo != ProjectNumber)
            {
                continue;
            }

            var documentNumber = $"{match.Groups["projectNo"].Value}-{match.Groups["code1"].Value}-{match.Groups["volume"].Value}-{match.Groups["code2"].Value}-{match.Groups["docType"].Value}-{match.Groups["docDiscipline"].Value}-{match.Groups["package"].Value}";
            if (match.Groups["number"].Success)
            {
                documentNumber += $"-{match.Groups["number"].Value}";
            }
            
            // Get revision - if not in filename, try to detect from folder name
            string revision = "-";
            if (match.Groups["revision"].Success)
            {
                revision = match.Groups["revision"].Value;
            }
            else
            {
                // Look for revision in folder name
                var folderRevMatch = Regex.Match(parentFolder ?? "", @"REV[_\s-]*([A-Z])$", RegexOptions.IgnoreCase);
                if (folderRevMatch.Success)
                {
                    revision = folderRevMatch.Groups[1].Value;
                }
            }

            // Get description - remove the revision letter if it's at the end
            string description = "";
            if (match.Groups["description"].Success)
            {
                description = match.Groups["description"].Value
                    .Replace("-", " ")
                    .Replace("_", " ")
                    .Trim();
            }
            else
            {
                description = ParseDescription(fileName);
            }

            var doc = new DocumentMetadata
            {
                DocumentNumber = documentNumber,
                FilePath = filePath,
                ProjectNumber = ProjectNumber,
                ProjectName = ProjectName,
                Discipline = match.Groups["docDiscipline"].Value,
                Package = match.Groups["package"].Value,
                DocumentType = match.Groups["docType"].Value,
                RegisterNumber = RegisterNumber,
                ClientNumber = ClientNumber,
                Description = string.IsNullOrWhiteSpace(description) ? ParseDescription(fileName) : description,
                Size = DetermineDrawingSize(filePath)
            };

            var revInfo = new RevisionInfo
            {
                Revision = revision,
                Purpose = DeterminePurpose(filePath),
                Method = "E",
                IssuedBy = DetermineIssuedBy(filePath),
                IsDistributed = true,
                FilePath = filePath
            };

            doc.RevisionHistory[issueDate] = revInfo;

            var existingDoc = Documents.FirstOrDefault(d => d.DocumentNumber == doc.DocumentNumber);
            if (existingDoc != null)
            {
                // Update existing document metadata with new revision
                existingDoc.RevisionHistory[issueDate] = revInfo;
                existingDoc.FilePath = filePath;  // Keep latest file path in document for grid double-click
            }
            else
            {
                Documents.Add(doc);
            }
        }

        // After processing, save updated data
        SaveProjectData();
    }

    public void SaveProjectData()
    {
        if (string.IsNullOrEmpty(_currentBasePath)) return;

        var storage = new ProjectStorage
        {
            ProjectNumber = ProjectNumber,
            ProjectName = ProjectName,
            Discipline = Discipline,
            RegisterNumber = RegisterNumber,
            ClientNumber = ClientNumber,
            BaseFolderPath = _currentBasePath,
            LastScanDate = DateTime.Now,
            Documents = Documents.Select(d => new DocumentStorageInfo
            {
                DocumentNumber = d.DocumentNumber,
                Description = d.Description,
                Package = d.Package,
                DocumentType = d.DocumentType,
                Size = d.Size,
                RevisionHistory = d.RevisionHistory.ToDictionary(
                    kv => kv.Key,
                    kv => new RevisionStorageInfo
                    {
                        Revision = kv.Value.Revision,
                        Purpose = kv.Value.Purpose,
                        Method = kv.Value.Method,
                        IssuedBy = kv.Value.IssuedBy,
                        IsDistributed = kv.Value.IsDistributed,
                        FilePath = kv.Value.FilePath
                    })
            }).ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(storage, options);
        File.WriteAllText(Path.Combine(_currentBasePath, STORAGE_FILENAME), json);
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