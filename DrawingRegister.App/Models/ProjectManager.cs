using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using DrawingRegister.App.Helpers;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Models;

public class ProjectManager : INotifyPropertyChanged
{
    private const string STORAGE_FILENAME = "project_data.json";
    public string _currentBasePath = string.Empty;
    private ProjectInfo? _projectInfo;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Captured on whatever thread constructs ProjectManager (UI thread in
    // practice). Used to marshal PropertyChanged back to the UI so that
    // ImportDocuments can run on a background thread without WPF bindings
    // throwing "calling thread cannot access this object".
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;

    private void RaisePropertyChanged(string name)
    {
        var handler = PropertyChanged;
        if (handler == null) return;
        if (_syncContext != null && _syncContext != SynchronizationContext.Current)
            _syncContext.Post(_ => handler(this, new PropertyChangedEventArgs(name)), null);
        else
            handler(this, new PropertyChangedEventArgs(name));
    }

    private string _projectNumber = string.Empty;
    private string _projectName = string.Empty;
    private string _discipline = string.Empty;
    private string _registerNumber = string.Empty;
    private string _clientNumber = string.Empty;
    private bool _useNumericRevisions;
    private RevisionScheme _revisionScheme = RevisionScheme.Legacy;

    public ObservableCollection<DocumentMetadata> Documents { get; } = new();
    public List<DateTime> IssueDates { get; } = new();

    public ProjectStorage? _currentStorage;
    public Action<string, string>? OnFolderStatusUpdated { get; set; }
    
    // Add DistributionManager property
    public DistributionManager DistributionManager { get; private set; } = new DistributionManager(string.Empty);

    public string ProjectNumber
    {
        get => _projectNumber;
        set
        {
            _projectNumber = value;
            RaisePropertyChanged(nameof(ProjectNumber));
        }
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            _projectName = value;
            RaisePropertyChanged(nameof(ProjectName));
        }
    }

    public string Discipline
    {
        get => _discipline;
        set
        {
            _discipline = value;
            RaisePropertyChanged(nameof(Discipline));
        }
    }

    public string RegisterNumber
    {
        get => _registerNumber;
        set
        {
            _registerNumber = value;
            RaisePropertyChanged(nameof(RegisterNumber));
        }
    }

    public string ClientNumber
    {
        get => _clientNumber;
        set
        {
            _clientNumber = value;
            RaisePropertyChanged(nameof(ClientNumber));
        }
    }

    public bool UseNumericRevisions
    {
        get => _useNumericRevisions;
        set
        {
            _useNumericRevisions = value;
            RaisePropertyChanged(nameof(UseNumericRevisions));
        }
    }

    public RevisionScheme RevisionScheme
    {
        get => _revisionScheme;
        set
        {
            _revisionScheme = value;
            // Keep the legacy bool in sync so any callers (and the saved JSON) that still look at
            // UseNumericRevisions see a consistent value.
            _useNumericRevisions = value == RevisionScheme.Numeric;
            RaisePropertyChanged(nameof(RevisionScheme));
            RaisePropertyChanged(nameof(UseNumericRevisions));
        }
    }

    public ImportResult ImportDocuments(string folderPath, string? specificFolderToRescanFullPath = null)
    {
        using var _perf = PerfLog.Begin($"ProjectManager.ImportDocuments(specific={specificFolderToRescanFullPath != null})");
        var importResult = new ImportResult();
        bool isSpecificRescan = !string.IsNullOrEmpty(specificFolderToRescanFullPath);

        if (!isSpecificRescan) // Full import/refresh
        {
            Documents.Clear();
            // IssueDates will be rebuilt at the end from all documents.
            ProjectNumber = string.Empty;
            ProjectName = string.Empty;
            Discipline = string.Empty;
            RegisterNumber = string.Empty;
            ClientNumber = string.Empty;
        }

        _currentBasePath = folderPath;
        var storageFile = Path.Combine(folderPath, STORAGE_FILENAME);

        // Initialize the DistributionManager with the project path
        DistributionManager = new DistributionManager(folderPath);

        // Load project info (static metadata) - persists separately from dynamic data
        _projectInfo = ProjectInfo.Load(folderPath);

        // Try to load existing project data if it exists
        _currentStorage = ProjectStorage.Load(storageFile);
        var processedFolders = new HashSet<string>();

        if (_currentStorage == null)
        {
            _currentStorage = new ProjectStorage { BaseFolderPath = folderPath, Projects = new List<DrawingProject>() };
        }

        // Build the paper-size cache from the ORIGINAL storage before the
        // specific-rescan block below purges entries for the folder being
        // rescanned. Without this, a "Rescan Folder" never hits the cache.
        var knownSizes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var docInfo in _currentStorage.Documents)
        {
            if (string.IsNullOrEmpty(docInfo.Size)) continue;
            foreach (var rev in docInfo.RevisionHistory.Values)
            {
                if (!string.IsNullOrEmpty(rev.FilePath))
                    knownSizes[rev.FilePath] = docInfo.Size;
            }
        }

        // Migration: if project_info is empty but project_data.json has legacy static fields
        if (string.IsNullOrEmpty(_projectInfo.ProjectNumber))
        {
            var legacyData = LoadLegacyStaticFields(storageFile);
            if (legacyData != null && !string.IsNullOrEmpty(legacyData.ProjectNumber))
            {
                Console.WriteLine("\n=== Migrating static fields from project_data.json to project_info.json ===");
                _projectInfo.ProjectNumber = legacyData.ProjectNumber;
                _projectInfo.ProjectName = legacyData.ProjectName;
                _projectInfo.Discipline = legacyData.Discipline;
                _projectInfo.RegisterNumber = legacyData.RegisterNumber;
                _projectInfo.ClientNumber = legacyData.ClientNumber;
                _projectInfo.Save(folderPath);
                Console.WriteLine($"Migrated: {_projectInfo.ProjectNumber} - {_projectInfo.ProjectName}");
            }
        }

        // Apply static fields from ProjectInfo to ProjectManager properties
        if (!isSpecificRescan)
        {
            ProjectNumber = _projectInfo.ProjectNumber;
            ProjectName = _projectInfo.ProjectName;
            Discipline = _projectInfo.Discipline;
            RegisterNumber = _projectInfo.RegisterNumber;
            ClientNumber = _projectInfo.ClientNumber;
            RevisionScheme = _projectInfo.RevisionScheme;
            Console.WriteLine($"\n=== Loaded project info: {ProjectNumber} - {ProjectName} - {Discipline} (RevisionScheme={RevisionScheme}) ===");
        }

        if (isSpecificRescan)
        {
            Console.WriteLine($"\n=== Preparing to rescan: {Path.GetFileName(specificFolderToRescanFullPath)} ===");
            var docsToRemove = Documents.Where(doc =>
                doc.RevisionHistory.Any(rev => !string.IsNullOrEmpty(rev.Value.FilePath) && Path.GetDirectoryName(rev.Value.FilePath)?.Equals(specificFolderToRescanFullPath, StringComparison.OrdinalIgnoreCase) == true) ||
                (!string.IsNullOrEmpty(doc.FilePath) && Path.GetDirectoryName(doc.FilePath)?.Equals(specificFolderToRescanFullPath, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();
            foreach (var doc in docsToRemove) { Documents.Remove(doc); }

            _currentStorage.Documents.RemoveAll(docInfo =>
                 docInfo.RevisionHistory.Any(rev => !string.IsNullOrEmpty(rev.Value.FilePath) && Path.GetDirectoryName(rev.Value.FilePath)?.Equals(specificFolderToRescanFullPath, StringComparison.OrdinalIgnoreCase) == true) ||
                (!string.IsNullOrEmpty(docInfo.FilePath) && Path.GetDirectoryName(docInfo.FilePath)?.Equals(specificFolderToRescanFullPath, StringComparison.OrdinalIgnoreCase) == true));
            _currentStorage.Projects.RemoveAll(p => p.FolderPath.Equals(specificFolderToRescanFullPath, StringComparison.OrdinalIgnoreCase));
        }

        // Populate Documents from _currentStorage (which might have been modified if isSpecificRescan)
        if (_currentStorage != null && _currentStorage.Documents != null)
        {
            Console.WriteLine($"\n=== Loading Documents From Storage (Count: {_currentStorage.Documents.Count}) ===");
            foreach (var docStorageInfo in _currentStorage.Documents)
            {
                // Avoid re-adding if it's somehow still in Documents (should only happen if logic elsewhere is flawed)
                if (!Documents.Any(d => d.DocumentNumber == docStorageInfo.DocumentNumber))
                {
                    var metadata = new DocumentMetadata
                    {
                        DocumentNumber = docStorageInfo.DocumentNumber,
                        Description = docStorageInfo.Description,
                        Package = docStorageInfo.Package,
                        DocumentType = docStorageInfo.DocumentType,
                        Size = docStorageInfo.Size,
                        ProjectNumber = this.ProjectNumber, // Use current ProjectManager's properties
                        ProjectName = this.ProjectName,
                        Discipline = this.Discipline,
                        RegisterNumber = this.RegisterNumber,
                        ClientNumber = this.ClientNumber,
                        DistributionCompanyIds = docStorageInfo.DistributionCompanyIds
                    };

                    foreach (var rev in docStorageInfo.RevisionHistory)
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

                    if (metadata.RevisionHistory.Any())
                    {
                        var latestRevisionEntry = metadata.RevisionHistory.OrderByDescending(kv => kv.Key).First();
                        metadata.FilePath = latestRevisionEntry.Value.FilePath;
                    }

                    // Recalculate paper size if missing and file exists
                    if (string.IsNullOrEmpty(metadata.Size) && !string.IsNullOrEmpty(metadata.FilePath) && File.Exists(metadata.FilePath))
                    {
                        metadata.Size = DetermineDrawingSize(metadata.FilePath);
                    }
                }
            }
        }

        processedFolders = _currentStorage != null && _currentStorage.Projects != null
            ? new HashSet<string>(_currentStorage.Projects.Select(p => p.FolderPath))
            : new HashSet<string>();
        Console.WriteLine($"Found {processedFolders.Count} previously processed folders (based on current storage state)");


        // Scan for new files
        var allSubDirectories = Directory.GetDirectories(folderPath);
        List<string> directoriesToScan;

        if (isSpecificRescan)
        {
            directoriesToScan = allSubDirectories.Contains(specificFolderToRescanFullPath, StringComparer.OrdinalIgnoreCase)
                ? new List<string> { specificFolderToRescanFullPath }
                : new List<string>();
            Console.WriteLine(directoriesToScan.Any() ? $"Targeting specific folder for rescan: {Path.GetFileName(specificFolderToRescanFullPath)}" : $"Specified folder for rescan not found or invalid: {specificFolderToRescanFullPath}. Skipping scan for this path.");
        }
        else
        {
            directoriesToScan = allSubDirectories.ToList();
        }

        Console.WriteLine($"\n=== Starting Directory Scan (Source: {(isSpecificRescan ? "Specific Rescan" : "All Eligible")}) ===");
        // Filter directories that need processing - only process unprocessed folders
        var dateDirectories = directoriesToScan
            .Where(dir => 
            {
                var dirInfo = new DirectoryInfo(dir);
                
                // If NOT a specific rescan, skip if folder is already processed.
                // If it IS a specific rescan, processedFolders will not contain specificFolderToRescanFullPath due to earlier removal.
                if (!isSpecificRescan && processedFolders.Contains(dir))
                {
                    var folderName = Path.GetFileName(dir);
                    Console.WriteLine($"⏩ Skipping previously processed directory: {folderName}");
                    OnFolderStatusUpdated?.Invoke(folderName, "Skipped");
                    return false;
                }
                
                Console.WriteLine($"\n🔍 Scanning directory: {Path.GetFileName(dir)}");
                // Remove any leading non-digit characters and handle underscore/dash separators
                var dirNameForLog = Path.GetFileName(dir);
                var dirNameToParse = Path.GetFileName(dir).Replace("_", "-").Trim();
                Console.WriteLine($"  Normalized name for parsing: {dirNameToParse}");
                
                var dateMatch = Regex.Match(dirNameToParse, @"(\d{8})");
                if (dateMatch.Success)
                {
                    var potentialDate = dateMatch.Groups[1].Value;
                    Console.WriteLine($"  ✅ Found date match: {potentialDate}");
                    
                    if (DateTime.TryParseExact(potentialDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        Console.WriteLine($"  ✅ Valid date folder: {dirNameForLog} (parsed {date:yyyy-MM-dd})");
                        OnFolderStatusUpdated?.Invoke(dirNameForLog, "Processed");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"  ❌ Failed parsing date from: {potentialDate}");
                        OnFolderStatusUpdated?.Invoke(dirNameForLog, "Error");
                    }
                }
                else
                {
                    Console.WriteLine($"  ❌ No 8-digit date found in directory name: {dirNameToParse}");
                }

                // Then try with common separators removed
                var cleanName = new string(dirNameToParse.TakeWhile(char.IsDigit).ToArray());
                Console.WriteLine($"  🧹 Cleaned name attempt: '{cleanName}'");
                
                if (cleanName.Length == 8 && DateTime.TryParseExact(cleanName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    Console.WriteLine($"  ✅ Cleaned name valid: {parsedDate:yyyy-MM-dd}");
                    OnFolderStatusUpdated?.Invoke(Path.GetFileName(dir), "Processed");
                    return true;
                }
                
                Console.WriteLine($"  ❌ Failed to parse date from directory: {Path.GetFileName(dir)}");
                OnFolderStatusUpdated?.Invoke(dirNameForLog, "Error");
                return false;
            })
            .ToList();

        Console.WriteLine($"\n=== Directory Processing Results ===");
        Console.WriteLine($"Found {dateDirectories.Count} valid date directories:");
        foreach (var dir in dateDirectories)
        {
            Console.WriteLine($"✓ {Path.GetFileName(dir)}");
        }
        
        // Log directories that were skipped
        var skippedDirs = directoriesToScan.Except(dateDirectories).ToList();
        if (skippedDirs.Any())
        {
            Console.WriteLine($"\nSkipped {skippedDirs.Count} directories:");
            foreach (var dir in skippedDirs)
            {
                Console.WriteLine($"❌ {Path.GetFileName(dir)}");
            }
        }

        if (!isSpecificRescan && !dateDirectories.Any())
        {
            // If we already have loaded projects, every date folder was skipped as
            // already-processed — that's a valid "nothing new to scan" refresh, not
            // an error. Only throw when the user pointed at a folder with no date
            // subfolders at all and no prior scan.
            if (_currentStorage?.Projects?.Count > 0)
            {
                Console.WriteLine("No new date folders found — all existing folders already processed.");
                return importResult;
            }
            throw new Exception("No valid new date folders found. Folders should start with a date in format YYYYMMDD.");
        }

        // Only get PDF files from date directories
        var pdfFiles = dateDirectories
            .SelectMany(dir => Directory.GetFiles(dir, "*.pdf"))
            .ToList();

        importResult.TotalPdfFiles = pdfFiles.Count;
        Console.WriteLine($"\n=== PDF File Processing ===");
        Console.WriteLine($"Found {pdfFiles.Count} PDF files in date directories");

        if (!isSpecificRescan && !pdfFiles.Any() && dateDirectories.Any()) // If there were date dirs but no PDFs in them (full scan)
        {
            throw new Exception("No PDF files found in any of the processed date folders.");
        }

        // Pre-compute paper sizes: knownSizes was populated from pre-purge storage
        // above; anything not in it is genuinely new and needs PdfPig to open it.
        // Parallel.ForEach brings cold-scan from ~75ms/file sequential down to
        // whatever the disk + 4-8 cores can handle.
        var pathsNeedingSize = pdfFiles.Where(fp => !knownSizes.ContainsKey(fp)).ToList();
        var cacheHits = pdfFiles.Count - pathsNeedingSize.Count;
        if (pathsNeedingSize.Count > 0)
        {
            var sizeScanSw = Stopwatch.StartNew();
            var computed = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Parallel.ForEach(pathsNeedingSize, fp =>
            {
                computed[fp] = DetermineDrawingSize(fp);
            });
            foreach (var kv in computed) knownSizes[kv.Key] = kv.Value;
            PerfLog.Event("ProjectManager.PdfPig.ParallelScan", sizeScanSw.ElapsedMilliseconds, pathsNeedingSize.Count);
        }
        PerfLog.Event("ProjectManager.PdfPig.CacheHits", 0, cacheHits);

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

        if (string.IsNullOrEmpty(this.ProjectNumber) && !string.IsNullOrEmpty(detectedProjectNo))
        {
            ProjectNumber = detectedProjectNo;
        }
        else if (!string.IsNullOrEmpty(detectedProjectNo) && ProjectNumber != detectedProjectNo)
        {
            throw new Exception($"Project number mismatch. Storage has {ProjectNumber} but found {detectedProjectNo} in PDF files.");
        }
        var allIssueDates = dateDirectories
            .Select(dir => 
            {
                var dirName = Path.GetFileName(dir);
                var dateMatch = Regex.Match(dirName, @"(\d{8})");
                DateTime date;
                
                if (dateMatch.Success && 
                    DateTime.TryParseExact(dateMatch.Groups[1].Value, 
                                         "yyyyMMdd", 
                                         null, 
                                         System.Globalization.DateTimeStyles.None, 
                                         out date))
                {
                    // Preserve original folder date string including any suffix
                    var folderDatePart = dirName.Split(new[] { '_', ' ', '-' })[0];
                    if (folderDatePart.Length >= 8)
                    {
                        return DateTime.ParseExact(folderDatePart.Substring(0, 8), 
                                                 "yyyyMMdd", 
                                                 null, 
                                                 System.Globalization.DateTimeStyles.None);
                    }
                    return date;
                }
                return Directory.GetCreationTime(dir);
            })
            .OrderBy(d => d)
            .ToList();

        foreach (var filePath in pdfFiles)
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Get issue date from parent folder name or file date
            var parentFolder = Path.GetFileName(Path.GetDirectoryName(filePath));
            var issueDate = DateTime.Now; // Default to now
            
            if (parentFolder != null)
            {
                var folderDatePart = parentFolder.Split(new[] { '_', ' ', '-' })[0];
                if (!DateTime.TryParseExact(folderDatePart.Length >= 8 ? folderDatePart.Substring(0, 8) : folderDatePart, 
                                          "yyyyMMdd", 
                                          null, 
                                          System.Globalization.DateTimeStyles.None, 
                                          out issueDate))
                {
                    // If folder name doesn't have date, use file creation date
                    issueDate = fileInfo.CreationTime;
                }
            }
            else
            {
                issueDate = fileInfo.CreationTime;
            }

            // Sanitize: collapse multiple consecutive hyphens to single, trim spaces around hyphens
            var sanitizedFileName = Regex.Replace(fileName, @"-{2,}", "-");
            sanitizedFileName = Regex.Replace(sanitizedFileName, @"\s*-\s*", "-");

            // SER Document Register files (DocReg-<projectNo>-<yyyyMMdd>.pdf) — short-circuit
            // before the drawing regex. The SER process in Scotland requires this exact filename
            // format; see docs/superpowers/specs/2026-04-24-docreg-document-support-design.md.
            if (DocRegFilenameParser.TryParse(sanitizedFileName, out var docRegMatch))
            {
                if (docRegMatch.ProjectNumber != ProjectNumber)
                {
                    importResult.SkippedFiles.Add(new SkippedFileInfo
                    {
                        FileName = fileName,
                        FilePath = filePath,
                        Reason = $"Project number mismatch (expected {ProjectNumber}, found {docRegMatch.ProjectNumber})"
                    });
                    continue;
                }

                var docRegNumber = fileName; // DocumentNumber = full base filename (see spec)
                var docRegPurpose = DeterminePurpose(filePath);
                var docRegMethod = DetermineMethodOfIssue(filePath);
                var docRegIssuedBy = DetermineIssuedBy(filePath);

                var docRegMetadata = new DocumentMetadata
                {
                    DocumentNumber = docRegNumber,
                    Description = "Document Register",
                    Package = string.Empty,
                    DocumentType = "DOCREG",
                    Size = knownSizes.TryGetValue(filePath, out var docRegCachedSize) ? docRegCachedSize : "A",
                    ProjectNumber = ProjectNumber,
                    ProjectName = ProjectName,
                    Discipline = string.Empty,
                    RegisterNumber = RegisterNumber,
                    ClientNumber = ClientNumber,
                    PurposeOfIssue = docRegPurpose,
                    MethodOfIssue = docRegMethod,
                    IssuedBy = docRegIssuedBy
                };

                var docRegRevInfo = new RevisionInfo
                {
                    Revision = "-",
                    Purpose = docRegPurpose,
                    Method = docRegMethod,
                    IssuedBy = docRegIssuedBy,
                    IsDistributed = true,
                    FilePath = filePath
                };

                var docRegParentFolder = Path.GetDirectoryName(filePath);
                var docRegFolderHash = docRegParentFolder?.GetHashCode() ?? 0;
                var docRegUniqueTicks = Math.Abs(docRegFolderHash) % TimeSpan.TicksPerDay;
                var docRegRevisionKey = issueDate.Date.AddTicks(docRegUniqueTicks);

                var existingDocReg = Documents.FirstOrDefault(d => d.DocumentNumber == docRegMetadata.DocumentNumber);
                if (existingDocReg != null)
                {
                    if (!existingDocReg.RevisionHistory.ContainsKey(docRegRevisionKey))
                    {
                        existingDocReg.RevisionHistory[docRegRevisionKey] = docRegRevInfo;
                    }

                    var docRegLatest = existingDocReg.RevisionHistory.OrderByDescending(kv => kv.Key).First();
                    existingDocReg.FilePath = docRegLatest.Value.FilePath;
                    existingDocReg.PurposeOfIssue = docRegLatest.Value.Purpose;
                    existingDocReg.MethodOfIssue = docRegLatest.Value.Method;
                    existingDocReg.IssuedBy = docRegLatest.Value.IssuedBy;
                }
                else
                {
                    docRegMetadata.RevisionHistory[docRegRevisionKey] = docRegRevInfo;
                    docRegMetadata.FilePath = filePath;
                    Documents.Add(docRegMetadata);
                }

                importResult.SuccessfullyParsed++;
                continue;
            }

            // Updated regex pattern to better handle drawing numbers and revisions
            var regex = new Regex(@"^(?<projectNo>\d{5,6})-\s*(?<code1>[^-]+)-\s*(?<volume>[^-]+)-\s*(?<code2>[^-]+)-\s*(?<docType>[^-]+)-\s*(?<docDiscipline>[^-]+)-\s*(?<package>\d+)(?:-\s*(?<number>\d+)(?=[_\s-]|$))?(?:-\s*(?<revision>[A-Z]\d{2}|\d+[A-Z]|[A-Z]|\d+)(?=[_\s-]|$))?(?:[_\s-]\s*(?<description>.+))?$");
            var match = regex.Match(sanitizedFileName);

            if (!match.Success)
            {
                importResult.SkippedFiles.Add(new SkippedFileInfo
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Reason = "Filename format not recognised"
                });
                continue;
            }

            // Verify project number matches
            var fileProjectNo = match.Groups["projectNo"].Value;
            if (fileProjectNo != ProjectNumber)
            {
                importResult.SkippedFiles.Add(new SkippedFileInfo
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Reason = $"Project number mismatch (expected {ProjectNumber}, found {fileProjectNo})"
                });
                continue;
            }

            var documentNumber = $"{match.Groups["projectNo"].Value.Trim()}-{match.Groups["code1"].Value.Trim()}-{match.Groups["volume"].Value.Trim()}-{match.Groups["code2"].Value.Trim()}-{match.Groups["docType"].Value.Trim()}-{match.Groups["docDiscipline"].Value.Trim()}-{match.Groups["package"].Value.Trim()}";
            if (match.Groups["number"].Success)
            {
                documentNumber += $"-{match.Groups["number"].Value.Trim()}";
            }
            
            // Get revision - if not in filename, try to detect from folder name
            string revision = "-";
            if (match.Groups["revision"].Success)
            {
                revision = match.Groups["revision"].Value.Trim();
            }
            else
            {
                // Look for revision in folder name
                var folderRevMatch = Regex.Match(parentFolder ?? "", @"REV[_\s-]*([A-Z]\d{2}|\d+[A-Z]|[A-Z]|\d+)$", RegexOptions.IgnoreCase);
                if (folderRevMatch.Success)
                {
                    revision = folderRevMatch.Groups[1].Value;
                }
                else
                {
                    revision = "-";  // Explicitly set to "-" if no revision found
                }
            }

            // Determine purpose based on revision code
            string purpose = DeterminePurpose(filePath);
            if (revision.Length > 0 && char.IsLetter(revision[0]))
            {
                if (revision.StartsWith("I") && revision.Length == 3)
                    purpose = "Information";
                else if (revision.StartsWith("C") && revision.Length == 3)
                    purpose = "Construction";
                else if (revision.StartsWith("T") && revision.Length == 3)
                    purpose = "Tender";
                else if (revision.StartsWith("P") && revision.Length == 3)
                    purpose = "Planning";
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

            var metadata = new DocumentMetadata
            {
                DocumentNumber = documentNumber,
                Description = description,
                Package = match.Groups["package"].Value,
                DocumentType = match.Groups["docType"].Value,
                Size = knownSizes.TryGetValue(filePath, out var cachedSize) ? cachedSize : "A",
                ProjectNumber = ProjectNumber,
                ProjectName = ProjectName,
                Discipline = Discipline,
                RegisterNumber = RegisterNumber,
                ClientNumber = ClientNumber,
                PurposeOfIssue = purpose,
                MethodOfIssue = DetermineMethodOfIssue(filePath),
                IssuedBy = DetermineIssuedBy(filePath)
            };

            var revInfo = new RevisionInfo
            {
                Revision = revision,
                Purpose = purpose,
                Method = DetermineMethodOfIssue(filePath),
                IssuedBy = DetermineIssuedBy(filePath),
                IsDistributed = true,
                FilePath = filePath
            };

            // Get the full folder path to ensure unique key for same-date folders
            var parentFolderPath = Path.GetDirectoryName(filePath);
            var folderHash = parentFolderPath?.GetHashCode() ?? 0;
            // Create unique tick value based on folder hash (within same day)
            var uniqueTicks = Math.Abs(folderHash) % TimeSpan.TicksPerDay;
            var revisionKey = issueDate.Date.AddTicks(uniqueTicks);

            var existingDoc = Documents.FirstOrDefault(d => d.DocumentNumber == metadata.DocumentNumber);
            if (existingDoc != null)
            {
                // Always add new revision
                if (!existingDoc.RevisionHistory.ContainsKey(revisionKey))
                {
                    existingDoc.RevisionHistory[revisionKey] = revInfo;
                }
                // If same date/key exists, only update if this new one has a non-'-' revision 
                // (assuming '-' is placeholder for initial/undetermined revision)
                else if (revision != "-" && existingDoc.RevisionHistory[revisionKey].Revision == "-")
                {
                     existingDoc.RevisionHistory[revisionKey] = revInfo;
                }
                
                // After potentially adding/updating a revision, always ensure the 
                // document's main FilePath points to the one from the *latest* revision overall.
                var latestOverallRevision = existingDoc.RevisionHistory.OrderByDescending(kv => kv.Key).First();
                existingDoc.FilePath = latestOverallRevision.Value.FilePath;
                
                // Also update other metadata fields from the latest revision if needed
                existingDoc.PurposeOfIssue = latestOverallRevision.Value.Purpose;
                existingDoc.MethodOfIssue = latestOverallRevision.Value.Method;
                existingDoc.IssuedBy = latestOverallRevision.Value.IssuedBy;
            }
            else
            {
                metadata.RevisionHistory[revisionKey] = revInfo;
                metadata.FilePath = filePath; // Set initial FilePath
                Documents.Add(metadata);
            }

            importResult.SuccessfullyParsed++;

            // Check if file should be renamed to match canonical naming convention
            // Only flag files with real structural differences, not just separator style (- vs _ vs space)
            if (!string.IsNullOrEmpty(description))
            {
                var revisionPart = (revision != "-" && !string.IsNullOrEmpty(revision)) ? $"-{revision}" : "";
                var canonicalName = $"{documentNumber}{revisionPart}_{description.Replace(" ", "_")}{Path.GetExtension(filePath)}";
                var actualName = Path.GetFileName(filePath);
                // Normalize separators for comparison: treat -, _, and space as equivalent
                // Do NOT collapse multiples — double hyphens (e.g. 20--10) are a real problem to flag
                var normalizedActual = actualName.ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
                var normalizedCanonical = canonicalName.ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
                if (normalizedActual != normalizedCanonical)
                {
                    // Avoid duplicates (same file processed via multiple revisions)
                    if (!importResult.SuggestedRenames.Any(r => string.Equals(r.OriginalPath, filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        importResult.SuggestedRenames.Add(new FileRenameInfo
                        {
                            OriginalPath = filePath,
                            SuggestedName = canonicalName,
                            DocumentNumber = documentNumber
                        });
                    }
                }
            }
        }

        // After processing, update storage with processed directories
        foreach (var dir in dateDirectories)
        {
            var dirInfo = new DirectoryInfo(dir);
            var existingProject = _currentStorage?.Projects.FirstOrDefault(p => p.FolderPath == dir);
            
            if (existingProject == null)
            {
                _currentStorage?.Projects.Add(new DrawingProject
                {
                    FolderPath = dir,
                    LastModified = dirInfo.LastWriteTime
                });
            }
            else
            {
                existingProject.LastModified = dirInfo.LastWriteTime;
            }
        }

        // Auto-detect revision scheme when it hasn't been set explicitly (Legacy = default/unset).
        // - If any revision matches \d+[A-Z] (e.g. 1A, 2B) → SubLetterNumeric
        // - Else if numeric-only revisions exist and no letter-prefixed revisions → Numeric
        if (RevisionScheme == RevisionScheme.Legacy)
        {
            var allRevisions = Documents
                .SelectMany(d => d.RevisionHistory.Values)
                .Select(r => r.Revision)
                .Where(r => !string.IsNullOrEmpty(r) && r != "-")
                .ToList();

            bool hasSubLetterRevisions = allRevisions.Any(r => Regex.IsMatch(r, "^\\d+[A-Z]$"));
            bool hasNumericRevisions = allRevisions.Any(r => r.All(char.IsDigit));
            bool hasLetterPrefixedRevisions = allRevisions.Any(r => r.Length > 0 && char.IsLetter(r[0]));

            if (hasSubLetterRevisions)
            {
                RevisionScheme = RevisionScheme.SubLetterNumeric;
                Console.WriteLine("\n=== Auto-detected sub-letter revision scheme (1A/1B → 1) ===");
            }
            else if (hasNumericRevisions && !hasLetterPrefixedRevisions)
            {
                RevisionScheme = RevisionScheme.Numeric;
                Console.WriteLine("\n=== Auto-detected numeric revision scheme (SSEN-style) ===");
            }
        }

        // Rebuild IssueDates from all documents currently in the collection
        IssueDates.Clear();
        var uniqueDatesFromDocs = Documents
            .SelectMany(d => d.RevisionHistory.Keys)
            .Select(dt => dt.Date) // Ensure only date part
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        IssueDates.AddRange(uniqueDatesFromDocs);

        PerfLog.Event("ProjectManager.Documents.Count", 0, Documents.Count);

        SaveProjectData();

        return importResult;
    }

    public void SaveProjectData()
    {
        using var _perf = PerfLog.Begin("ProjectManager.SaveProjectData");
        if (string.IsNullOrEmpty(_currentBasePath) || _currentStorage == null) return;

        // Save static project info to separate file (project_info.json)
        if (_projectInfo == null) _projectInfo = new ProjectInfo();
        _projectInfo.ProjectNumber = ProjectNumber;
        _projectInfo.ProjectName = ProjectName;
        _projectInfo.Discipline = Discipline;
        _projectInfo.RegisterNumber = RegisterNumber;
        _projectInfo.ClientNumber = ClientNumber;
        _projectInfo.UseNumericRevisions = UseNumericRevisions;
        _projectInfo.RevisionScheme = RevisionScheme;
        _projectInfo.Save(_currentBasePath);

        // Save dynamic data only (static fields removed from ProjectStorage)
        _currentStorage.BaseFolderPath = _currentBasePath;
        _currentStorage.LastScanDate = DateTime.Now;
        _currentStorage.LastProcessedDate = DateTime.Now;

        _currentStorage.Documents = Documents.Select(d => new DocumentStorageInfo
        {
            DocumentNumber = d.DocumentNumber,
            Description = d.Description,
            Package = d.Package,
            DocumentType = d.DocumentType,
            Size = d.Size,
            // Ensure FilePath on the main DocumentStorageInfo is also the latest
            FilePath = d.RevisionHistory.Any() ? d.RevisionHistory.OrderByDescending(kv => kv.Key).First().Value.FilePath : string.Empty,
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
                }),
            DistributionCompanyIds = d.DistributionCompanyIds
        }).ToList();

        _currentStorage.Save(Path.Combine(_currentBasePath, STORAGE_FILENAME));

        // Save distribution data
        DistributionManager?.SaveCompanies();
    }

    private string DeterminePurpose(string filePath)
    {
        var folder = Path.GetFileName(filePath)?.ToUpper() ?? "";
        Console.WriteLine($"\n🔍 Determining purpose for folder: {folder}");
        
        // First check for exact matches with folder names from 01-DRAWING ISSUES.txt
        if (folder.Contains("WILL_REVIEW")) { Console.WriteLine("  ✓ Matched: WILL_REVIEW"); return "Review"; }
        if (folder.Contains("DSC ISSUE")) { Console.WriteLine("  ✓ Matched: DSC ISSUE"); return "DSC"; }
        if (folder.Contains("SETTING OUT")) { Console.WriteLine("  ✓ Matched: SETTING OUT"); return "Setting Out"; }
        if (folder.Contains("STRUCTURES")) { Console.WriteLine("  ✓ Matched: STRUCTURES"); return "Structures"; }
        if (folder.Contains("TENDER_ISSUE") || folder.Contains("TENDER ISSUE") || folder.Contains("TENDER-ISSUE")) { Console.WriteLine("  ✓ Matched: TENDER ISSUE"); return "Tender"; }
        if (folder.Contains("WARRANT-ISSUE") || folder.Contains("WARRANT ISSUE") || folder.Contains("WARRANT_ISSUE")) { Console.WriteLine("  ✓ Matched: WARRANT ISSUE"); return "Warrant"; }
        if (folder.Contains("TENDER_ISSUE_CIVIL") || folder.Contains("TENDER ISSUE CIVIL")) { Console.WriteLine("  ✓ Matched: TENDER ISSUE CIVIL"); return "Tender Civil"; }
        if (folder.Contains("WARRANT-ISSUE CIVIL") || folder.Contains("WARRANT ISSUE CIVIL")) { Console.WriteLine("  ✓ Matched: WARRANT ISSUE CIVIL"); return "Warrant Civil"; }
        
        // Then check for partial matches
        if (folder.Contains("WARRANT")) { Console.WriteLine("  ✓ Matched partial: WARRANT"); return "Warrant"; }
        if (folder.Contains("DRAFT")) { Console.WriteLine("  ✓ Matched partial: DRAFT"); return "Draft"; }
        if (folder.Contains("DSC")) { Console.WriteLine("  ✓ Matched partial: DSC"); return "DSC"; }
        if (folder.Contains("RFI")) { Console.WriteLine("  ✓ Matched partial: RFI"); return "Information"; }
        if (folder.Contains("INFORMATION")) { Console.WriteLine("  ✓ Matched partial: INFORMATION"); return "Information"; }
        if (folder.Contains("CONSTRUCTION")) { Console.WriteLine("  ✓ Matched partial: CONSTRUCTION"); return "Construction"; }
        if (folder.Contains("TENDER")) { Console.WriteLine("  ✓ Matched partial: TENDER"); return "Tender"; }
        if (folder.Contains("PLANNING")) { Console.WriteLine("  ✓ Matched partial: PLANNING"); return "Planning"; }
        if (folder.Contains("APPROVAL")) { Console.WriteLine("  ✓ Matched partial: APPROVAL"); return "Approval"; }
        if (folder.Contains("FEASIBILITY")) { Console.WriteLine("  ✓ Matched partial: FEASIBILITY"); return "Feasibility"; }
        
        Console.WriteLine("  ❌ No specific purpose match found, checking context...");
        
        // If no specific purpose found in folder name, try to determine from context
        if (folder.Contains("CONNECTION")) { Console.WriteLine("  ✓ Matched context: CONNECTION"); return "Information"; }
        if (folder.Contains("COMMENTS")) { Console.WriteLine("  ✓ Matched context: COMMENTS"); return "Information"; }
        if (folder.Contains("LOADS")) { Console.WriteLine("  ✓ Matched context: LOADS"); return "Information"; }
        if (folder.Contains("REPLACEMENT")) { Console.WriteLine("  ✓ Matched context: REPLACEMENT"); return "Construction"; }
        
        Console.WriteLine("  ⚠️ No matches found, defaulting to Information");
        return "Information"; // Default to Information
    }

    private string DetermineMethodOfIssue(string filePath)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(filePath))?.ToLower();
        if (folder?.Contains("sharepoint") == true) return "Sharepoint";
        return "Email"; // Default to Email
    }

    private string DetermineIssuedBy(string filePath)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(filePath))?.ToLower();
        
        // Try to extract initials from folder name
        if (folder?.Contains("chris_davidson") == true) return "CD";
        if (folder?.Contains("chivas") == true) return "CH";
        
        return "MJ"; // Default to MJ
    }

    private string ParseDescription(string fileName)
    {
        try
        {
            // Remove file extension and trim any trailing spaces
            fileName = Path.GetFileNameWithoutExtension(fileName).TrimEnd();

            // Split by hyphen and get the description part
            var parts = fileName.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()) // Trim each part
                .ToArray();

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
                var type = parts[4].Trim(); // DR, SK, etc.
                var discipline = parts[5].Trim(); // S, A, etc.
                var category = parts[6].Trim(); // 00, 20, etc.

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
            case "DOCREG": return "DOCUMENT REGISTER";
            default: return type;
        }
    }

    // ISO A-series paper sizes in mm (short edge x long edge)
    private static readonly (string Name, double Width, double Height)[] PaperSizes =
    {
        ("A0", 841, 1189),
        ("A1", 594, 841),
        ("A2", 420, 594),
        ("A3", 297, 420),
        ("A4", 210, 297),
    };

    private string DetermineDrawingSize(string filePath)
    {
        try
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(filePath);
            if (document.NumberOfPages == 0) return "A";

            var page = document.GetPage(1);
            // Convert points to mm (1 point = 25.4/72 mm)
            var widthMm = page.Width * 25.4 / 72.0;
            var heightMm = page.Height * 25.4 / 72.0;

            // Normalise to portrait (short edge x long edge)
            var shortEdge = Math.Min(widthMm, heightMm);
            var longEdge = Math.Max(widthMm, heightMm);

            // Find closest matching paper size
            string bestMatch = "A";
            double bestDistance = double.MaxValue;

            foreach (var (name, w, h) in PaperSizes)
            {
                var distance = Math.Abs(shortEdge - w) + Math.Abs(longEdge - h);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = name;
                }
            }

            // Only return a match if within reasonable tolerance (20mm total)
            return bestDistance <= 20 ? bestMatch : "A";
        }
        catch
        {
            return "A";
        }
    }

    public void ScanDirectory(string directoryPath)
    {
        var storage = ProjectStorage.Load(Path.Combine(directoryPath, "project_data.json"));
        
        foreach (var folder in Directory.GetDirectories(directoryPath))
        {
            var dirInfo = new DirectoryInfo(folder);
            
            // Skip already processed folders that haven't changed
            if (dirInfo.LastWriteTime < storage.LastProcessedDate && 
                storage.Projects.Any(p => p.FolderPath == folder))
            {
                continue;
            }
            
            // Existing processing logic here...
        }
        
        storage.Save(Path.Combine(directoryPath, "project_data.json"));
    }

    object? GetPropertyValue(object obj, string propertyName)
    {
        return obj.GetType().GetProperty(propertyName)?.GetValue(obj);
    }

    /// <summary>
    /// Loads legacy static fields from project_data.json for migration to project_info.json.
    /// This supports backward compatibility with older project files.
    /// </summary>
    private LegacyProjectData? LoadLegacyStaticFields(string storageFilePath)
    {
        try
        {
            if (!File.Exists(storageFilePath)) return null;

            var json = File.ReadAllText(storageFilePath);
            return JsonSerializer.Deserialize<LegacyProjectData>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Helper class to deserialize legacy static fields from old project_data.json format.
    /// </summary>
    private class LegacyProjectData
    {
        public string ProjectNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string RegisterNumber { get; set; } = string.Empty;
        public string ClientNumber { get; set; } = string.Empty;
    }
} 