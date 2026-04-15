# Drawing Register Scan Validation and Quarantine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add scan-time validation, quarantine, in-app fixes, and SQLite-backed project storage so invalid folder/file data is blocked from the live register without breaking historical issue dates.

**Architecture:** Introduce a SQLite repository as the project’s operational store, with automatic migration from legacy `project_data.json`. Build a validation layer that evaluates raw scan candidates before import, persists quarantined items separately, and lets `ProjectManager` import only validated records while still showing fix suggestions in a dedicated results dialog.

**Tech Stack:** C# 12, .NET 8 WPF, xUnit, `Microsoft.Data.Sqlite`

---

## File Map

**Storage and migration**
- Create: `DrawingRegister.App/Models/ProjectStoreSnapshot.cs`
- Create: `DrawingRegister.App/Services/ProjectStorageRepository.cs`
- Create: `DrawingRegister.App/Services/ProjectStorageMigrator.cs`
- Modify: `DrawingRegister.App/DrawingRegister.App.csproj`
- Modify: `DrawingRegister.App/Models/ProjectStorage.cs`

**Validation and quarantine**
- Create: `DrawingRegister.App/Models/ScanCandidate.cs`
- Create: `DrawingRegister.App/Models/ScanValidationIssue.cs`
- Create: `DrawingRegister.App/Models/QuarantineItem.cs`
- Create: `DrawingRegister.App/Services/ScanValidationService.cs`
- Modify: `DrawingRegister.App/Models/ImportResult.cs`
- Modify: `DrawingRegister.App/Models/DocumentMetadata.cs`

**Import integration**
- Modify: `DrawingRegister.App/Models/ProjectManager.cs`
- Modify: `DrawingRegister.App/Services/DocumentFilterService.cs`

**Fix application and UI**
- Create: `DrawingRegister.App/Services/ValidationFixService.cs`
- Create: `DrawingRegister.App/ScanValidationDialog.xaml`
- Create: `DrawingRegister.App/ScanValidationDialog.xaml.cs`
- Modify: `DrawingRegister.App/MainWindow.xaml.cs`

**Tests**
- Create: `DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs`
- Create: `DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs`
- Create: `DrawingRegister.App.Tests/Services/ValidationFixServiceTests.cs`
- Create: `DrawingRegister.App.Tests/Services/ProjectManagerImportTests.cs`

## Scope Check

The spec spans storage, validation, import, and UI, but these parts are sequential rather than independent products. One plan is appropriate because each task leaves the codebase in a working, testable state:

- storage first
- validation second
- import integration third
- fix-and-review UI last

### Task 1: Add failing tests for SQLite storage and JSON migration

**Files:**
- Create: `DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs`
- Modify: `DrawingRegister.App/DrawingRegister.App.csproj`
- Test: `DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs`

- [ ] **Step 1: Add the SQLite package reference**

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.8" />
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2903.40" />
  <PackageReference Include="PdfPig" Version="0.1.13" />
  <PackageReference Include="QuestPDF" Version="2023.12.1" />
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
  <PackageReference Include="Serilog" Version="3.1.1" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  <PackageReference Include="Serilog.Sinks.Debug" Version="2.0.0" />
  <PackageReference Include="Serilog.Extensions.Logging" Version="8.0.0" />
  <PackageReference Include="Velopack" Version="0.0.1298" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing repository and migration tests**

```csharp
using System.Text.Json;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class ProjectStorageRepositoryTests
{
    [Fact]
    public void OpenOrMigrate_creates_database_from_legacy_json_and_keeps_json_file()
    {
        using var workspace = new TestWorkspace();
        var baseFolder = workspace.RootPath;

        var legacyStorage = new ProjectStorage
        {
            BaseFolderPath = baseFolder,
            Documents =
            [
                new DocumentStorageInfo
                {
                    DocumentNumber = "10000-MJ-00-XX-DR-S-20-0001",
                    Description = "GENERAL ARRANGEMENT",
                    Package = "00",
                    DocumentType = "DR",
                    Size = "A1",
                    FilePath = Path.Combine(baseFolder, "20260415", "10000-MJ-00-XX-DR-S-20-0001.pdf"),
                    RevisionHistory = new Dictionary<DateTime, RevisionStorageInfo>
                    {
                        [new DateTime(2026, 4, 15)] = new()
                        {
                            Revision = "C01",
                            Purpose = "Construction",
                            Method = "Email",
                            IssuedBy = "MJ",
                            FilePath = Path.Combine(baseFolder, "20260415", "10000-MJ-00-XX-DR-S-20-0001.pdf")
                        }
                    }
                }
            ]
        };

        var legacyPath = Path.Combine(baseFolder, "project_data.json");
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacyStorage));

        var repository = new ProjectStorageRepository();
        var snapshot = repository.OpenOrMigrate(baseFolder);

        Assert.True(File.Exists(Path.Combine(baseFolder, "project_data.db")));
        Assert.True(File.Exists(legacyPath));
        Assert.Single(snapshot.Documents);
        Assert.Equal("GENERAL ARRANGEMENT", snapshot.Documents[0].CurrentDescription);
        Assert.Single(snapshot.Documents[0].Revisions);
    }

    [Fact]
    public void Save_persists_quarantine_items_and_latest_description_precedence()
    {
        using var workspace = new TestWorkspace();
        var repository = new ProjectStorageRepository();

        var snapshot = new ProjectStoreSnapshot
        {
            BaseFolderPath = workspace.RootPath,
            Documents =
            [
                new StoredDocument
                {
                    DocumentNumber = "10000-MJ-00-XX-DR-S-20-0002",
                    CurrentDescription = "UPDATED SETTING OUT PLAN",
                    Package = "00",
                    DocumentType = "DR",
                    Size = "A1",
                    LatestFilePath = @"C:\Project\20260420\10000-MJ-00-XX-DR-S-20-0002.pdf",
                    Revisions =
                    [
                        new StoredRevision
                        {
                            IssueDate = new DateTime(2026, 4, 15),
                            Revision = "P01",
                            Description = "SETTING OUT PLAN",
                            FilePath = @"C:\Project\20260415\10000-MJ-00-XX-DR-S-20-0002.pdf"
                        },
                        new StoredRevision
                        {
                            IssueDate = new DateTime(2026, 4, 20),
                            Revision = "C01",
                            Description = "UPDATED SETTING OUT PLAN",
                            FilePath = @"C:\Project\20260420\10000-MJ-00-XX-DR-S-20-0002.pdf"
                        }
                    ]
                }
            ],
            QuarantineItems =
            [
                new QuarantineItem
                {
                    RawFolderPath = @"C:\Project\15-04-2026",
                    RawFilePath = @"C:\Project\15-04-2026\broken-name.pdf",
                    IntendedIssueDate = new DateTime(2026, 4, 15),
                    SuggestedFolderName = "20260415",
                    ResolutionState = QuarantineResolutionState.Pending
                }
            ]
        };

        repository.Save(workspace.RootPath, snapshot);
        var reloaded = repository.OpenOrMigrate(workspace.RootPath);

        Assert.Equal("UPDATED SETTING OUT PLAN", reloaded.Documents[0].CurrentDescription);
        Assert.Equal("SETTING OUT PLAN", reloaded.Documents[0].Revisions[0].Description);
        Assert.Single(reloaded.QuarantineItems);
        Assert.Equal("20260415", reloaded.QuarantineItems[0].SuggestedFolderName);
    }

    private sealed class TestWorkspace : IDisposable
    {
        public TestWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "DrawingRegisterRepoTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 3: Run the repository tests to verify they fail**

Run: `dotnet test DrawingRegister.App.Tests --filter ProjectStorageRepositoryTests`

Expected: FAIL with compile errors because `ProjectStorageRepository`, `ProjectStoreSnapshot`, `StoredDocument`, `StoredRevision`, and `QuarantineItem` do not exist yet.

- [ ] **Step 4: Commit**

```bash
git add DrawingRegister.App/DrawingRegister.App.csproj DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs
git commit -m "test: add storage migration coverage"
```

### Task 2: Implement the SQLite repository and migration path

**Files:**
- Create: `DrawingRegister.App/Models/ProjectStoreSnapshot.cs`
- Create: `DrawingRegister.App/Services/ProjectStorageRepository.cs`
- Create: `DrawingRegister.App/Services/ProjectStorageMigrator.cs`
- Modify: `DrawingRegister.App/Models/ProjectStorage.cs`
- Test: `DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs`

- [ ] **Step 1: Add the repository snapshot models**

```csharp
namespace DrawingRegister.App.Models;

public sealed class ProjectStoreSnapshot
{
    public string BaseFolderPath { get; set; } = string.Empty;
    public DateTime LastScanDate { get; set; }
    public DateTime LastProcessedDate { get; set; }
    public List<StoredDocument> Documents { get; set; } = [];
    public List<QuarantineItem> QuarantineItems { get; set; } = [];
}

public sealed class StoredDocument
{
    public string DocumentNumber { get; set; } = string.Empty;
    public string CurrentDescription { get; set; } = string.Empty;
    public string Package { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string LatestFilePath { get; set; } = string.Empty;
    public List<StoredRevision> Revisions { get; set; } = [];
    public Dictionary<DateTime, List<string>> DistributionCompanyIds { get; set; } = [];
}

public sealed class StoredRevision
{
    public DateTime IssueDate { get; set; }
    public string Revision { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public bool IsDistributed { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Implement migration from JSON into SQLite**

```csharp
using DrawingRegister.App.Models;
using Microsoft.Data.Sqlite;

namespace DrawingRegister.App.Services;

internal sealed class ProjectStorageMigrator
{
    private const string LegacyFileName = "project_data.json";

    public bool NeedsMigration(string baseFolderPath)
    {
        return !File.Exists(ProjectStorageRepository.GetDatabasePath(baseFolderPath))
            && File.Exists(Path.Combine(baseFolderPath, LegacyFileName));
    }

    public ProjectStoreSnapshot LoadLegacySnapshot(string baseFolderPath)
    {
        var storage = ProjectStorage.Load(Path.Combine(baseFolderPath, LegacyFileName));

        return new ProjectStoreSnapshot
        {
            BaseFolderPath = storage.BaseFolderPath,
            LastProcessedDate = storage.LastProcessedDate,
            LastScanDate = storage.LastScanDate,
            Documents = storage.Documents.Select(document => new StoredDocument
            {
                DocumentNumber = document.DocumentNumber,
                CurrentDescription = document.Description,
                Package = document.Package,
                DocumentType = document.DocumentType,
                Size = document.Size,
                LatestFilePath = document.FilePath,
                DistributionCompanyIds = document.DistributionCompanyIds,
                Revisions = document.RevisionHistory
                    .OrderBy(entry => entry.Key)
                    .Select(entry => new StoredRevision
                    {
                        IssueDate = entry.Key,
                        Revision = entry.Value.Revision,
                        Description = document.Description,
                        Purpose = entry.Value.Purpose,
                        Method = entry.Value.Method,
                        IssuedBy = entry.Value.IssuedBy,
                        IsDistributed = entry.Value.IsDistributed,
                        FilePath = entry.Value.FilePath
                    })
                    .ToList()
            }).ToList()
        };
    }
}
```

- [ ] **Step 3: Implement the repository schema and round-trip logic**

```csharp
using DrawingRegister.App.Models;
using Microsoft.Data.Sqlite;

namespace DrawingRegister.App.Services;

public sealed class ProjectStorageRepository
{
    public ProjectStoreSnapshot OpenOrMigrate(string baseFolderPath)
    {
        var migrator = new ProjectStorageMigrator();
        if (migrator.NeedsMigration(baseFolderPath))
        {
            var legacySnapshot = migrator.LoadLegacySnapshot(baseFolderPath);
            Save(baseFolderPath, legacySnapshot);
        }

        EnsureDatabase(baseFolderPath);
        return Load(baseFolderPath);
    }

    public void Save(string baseFolderPath, ProjectStoreSnapshot snapshot)
    {
        EnsureDatabase(baseFolderPath);
        using var connection = new SqliteConnection(BuildConnectionString(baseFolderPath));
        connection.Open();
        using var transaction = connection.BeginTransaction();

        ExecuteNonQuery(connection, transaction, "delete from Revisions; delete from Documents; delete from QuarantineItems;");

        foreach (var document in snapshot.Documents)
        {
            InsertDocument(connection, transaction, document);
        }

        foreach (var item in snapshot.QuarantineItems)
        {
            InsertQuarantineItem(connection, transaction, item);
        }

        transaction.Commit();
    }

    public static string GetDatabasePath(string baseFolderPath) =>
        Path.Combine(baseFolderPath, "project_data.db");

    private static string BuildConnectionString(string baseFolderPath) =>
        $"Data Source={GetDatabasePath(baseFolderPath)}";

    private static void EnsureDatabase(string baseFolderPath)
    {
        Directory.CreateDirectory(baseFolderPath);
        using var connection = new SqliteConnection(BuildConnectionString(baseFolderPath));
        connection.Open();
        ExecuteNonQuery(connection, transaction: null, """
            create table if not exists Documents (
                DocumentNumber text primary key,
                CurrentDescription text not null,
                Package text not null,
                DocumentType text not null,
                Size text not null,
                LatestFilePath text not null
            );

            create table if not exists Revisions (
                DocumentNumber text not null,
                IssueDate text not null,
                Revision text not null,
                Description text not null,
                Purpose text not null,
                Method text not null,
                IssuedBy text not null,
                IsDistributed integer not null,
                FilePath text not null
            );

            create table if not exists QuarantineItems (
                Id integer primary key autoincrement,
                RawFolderPath text not null,
                RawFilePath text not null,
                IntendedIssueDate text null,
                SuggestedFolderName text null,
                SuggestedFileName text null,
                ResolutionState integer not null
            );
            """);
    }
}
```

- [ ] **Step 4: Run the repository tests to verify they pass**

Run: `dotnet test DrawingRegister.App.Tests --filter ProjectStorageRepositoryTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DrawingRegister.App/Models/ProjectStoreSnapshot.cs DrawingRegister.App/Services/ProjectStorageRepository.cs DrawingRegister.App/Services/ProjectStorageMigrator.cs DrawingRegister.App/Models/ProjectStorage.cs DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs
git commit -m "feat: add sqlite project storage"
```

### Task 3: Add failing tests for scan validation and quarantine decisions

**Files:**
- Create: `DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs`
- Modify: `DrawingRegister.App/Models/DocumentMetadata.cs`
- Test: `DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs`

- [ ] **Step 1: Extend revision data to preserve historical descriptions**

```csharp
public class RevisionInfo
{
    public string Revision { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public bool IsDistributed { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Write the failing validation tests**

```csharp
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class ScanValidationServiceTests
{
    [Fact]
    public void Validate_flags_noncanonical_folder_date_as_critical_with_fix_suggestion()
    {
        var candidate = new ScanCandidate
        {
            RawFolderPath = @"C:\Project\15-04-2026",
            RawFilePath = @"C:\Project\15-04-2026\10000-MJ-00-XX-DR-S-20-0001.pdf",
            DetectedFolderDateText = "15-04-2026",
            ParsedIssueDate = new DateTime(2026, 4, 15),
            ParsedDocumentNumber = "10000-MJ-00-XX-DR-S-20-0001",
            ParsedDescription = "GENERAL ARRANGEMENT",
            ParsedRevision = "C01"
        };

        var result = new ScanValidationService().Validate([candidate], []);

        var issue = Assert.Single(result.QuarantinedItems[0].Issues);
        Assert.Equal(ValidationSeverity.Critical, issue.Severity);
        Assert.Equal("FolderDateFormat", issue.RuleCode);
        Assert.Equal("20260415", issue.SuggestedValue);
        Assert.True(issue.CanAutoApply);
    }

    [Fact]
    public void Validate_allows_description_evolution_across_issue_dates()
    {
        var existing = new StoredDocument
        {
            DocumentNumber = "10000-MJ-00-XX-DR-S-20-0002",
            CurrentDescription = "SETTING OUT PLAN",
            Revisions =
            [
                new StoredRevision
                {
                    IssueDate = new DateTime(2026, 4, 15),
                    Revision = "P01",
                    Description = "SETTING OUT PLAN",
                    FilePath = @"C:\Project\20260415\10000-MJ-00-XX-DR-S-20-0002.pdf"
                }
            ]
        };

        var candidate = new ScanCandidate
        {
            RawFolderPath = @"C:\Project\20260420",
            RawFilePath = @"C:\Project\20260420\10000-MJ-00-XX-DR-S-20-0002.pdf",
            DetectedFolderDateText = "20260420",
            ParsedIssueDate = new DateTime(2026, 4, 20),
            ParsedDocumentNumber = "10000-MJ-00-XX-DR-S-20-0002",
            ParsedDescription = "UPDATED SETTING OUT PLAN",
            ParsedRevision = "C01"
        };

        var result = new ScanValidationService().Validate([candidate], [existing]);

        Assert.Single(result.ValidItems);
        Assert.Empty(result.QuarantinedItems);
    }

    [Fact]
    public void Validate_quarantines_same_date_duplicate_with_materially_different_description()
    {
        var first = new ScanCandidate
        {
            RawFolderPath = @"C:\Project\20260420",
            RawFilePath = @"C:\Project\20260420\10000-MJ-00-XX-DR-S-20-0003.pdf",
            DetectedFolderDateText = "20260420",
            ParsedIssueDate = new DateTime(2026, 4, 20),
            ParsedDocumentNumber = "10000-MJ-00-XX-DR-S-20-0003",
            ParsedDescription = "GROUND FLOOR PLAN",
            ParsedRevision = "C01"
        };

        var second = new ScanCandidate
        {
            RawFolderPath = @"C:\Project\20260420",
            RawFilePath = @"C:\Project\20260420\10000-MJ-00-XX-DR-S-20-0003-copy.pdf",
            DetectedFolderDateText = "20260420",
            ParsedIssueDate = new DateTime(2026, 4, 20),
            ParsedDocumentNumber = "10000-MJ-00-XX-DR-S-20-0003",
            ParsedDescription = "FOUNDATION PLAN",
            ParsedRevision = "C01"
        };

        var result = new ScanValidationService().Validate([first, second], []);

        Assert.Empty(result.ValidItems);
        Assert.Equal(2, result.QuarantinedItems.Count);
        Assert.All(result.QuarantinedItems, item =>
            Assert.Contains(item.Issues, issue => issue.RuleCode == "SameDateDuplicateConflict"));
    }
}
```

- [ ] **Step 3: Run the validation tests to verify they fail**

Run: `dotnet test DrawingRegister.App.Tests --filter ScanValidationServiceTests`

Expected: FAIL with compile errors because `ScanCandidate`, `ScanValidationService`, `ValidationSeverity`, and the validation result types do not exist yet.

- [ ] **Step 4: Commit**

```bash
git add DrawingRegister.App/Models/DocumentMetadata.cs DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs
git commit -m "test: add scan validation coverage"
```

### Task 4: Implement scan validation, quarantine models, and result projection

**Files:**
- Create: `DrawingRegister.App/Models/ScanCandidate.cs`
- Create: `DrawingRegister.App/Models/ScanValidationIssue.cs`
- Create: `DrawingRegister.App/Models/QuarantineItem.cs`
- Create: `DrawingRegister.App/Services/ScanValidationService.cs`
- Modify: `DrawingRegister.App/Models/ImportResult.cs`
- Modify: `DrawingRegister.App/Models/DocumentMetadata.cs`
- Test: `DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs`

- [ ] **Step 1: Add the validation and quarantine models**

```csharp
namespace DrawingRegister.App.Models;

public enum ValidationSeverity
{
    Info,
    Warning,
    Critical
}

public sealed class ScanCandidate
{
    public string RawFolderPath { get; set; } = string.Empty;
    public string RawFilePath { get; set; } = string.Empty;
    public string DetectedFolderDateText { get; set; } = string.Empty;
    public DateTime? ParsedIssueDate { get; set; }
    public string ParsedDocumentNumber { get; set; } = string.Empty;
    public string ParsedDescription { get; set; } = string.Empty;
    public string ParsedRevision { get; set; } = string.Empty;
    public string Package { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public DateTime ScanTimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ScanValidationIssue
{
    public ValidationSeverity Severity { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SuggestedValue { get; set; } = string.Empty;
    public bool CanAutoApply { get; set; }
}
```

```csharp
namespace DrawingRegister.App.Models;

public enum QuarantineResolutionState
{
    Pending,
    Fixed,
    Dismissed
}

public sealed class QuarantineItem
{
    public string RawFolderPath { get; set; } = string.Empty;
    public string RawFilePath { get; set; } = string.Empty;
    public DateTime? IntendedIssueDate { get; set; }
    public string SuggestedFolderName { get; set; } = string.Empty;
    public string SuggestedFileName { get; set; } = string.Empty;
    public QuarantineResolutionState ResolutionState { get; set; } = QuarantineResolutionState.Pending;
    public List<ScanValidationIssue> Issues { get; set; } = [];
}

public sealed class ScanValidationResult
{
    public List<ScanCandidate> ValidItems { get; } = [];
    public List<QuarantineItem> QuarantinedItems { get; } = [];
    public List<ScanValidationIssue> WarningIssues { get; } = [];
}
```

- [ ] **Step 2: Implement the validation service**

```csharp
using System.Globalization;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public sealed class ScanValidationService
{
    public ScanValidationResult Validate(
        IReadOnlyList<ScanCandidate> candidates,
        IReadOnlyList<StoredDocument> existingDocuments)
    {
        var result = new ScanValidationResult();

        foreach (var candidate in candidates)
        {
            var issues = new List<ScanValidationIssue>();
            ValidateFolderDate(candidate, issues);
            ValidateSameDateDuplicate(candidate, candidates, issues);
            ValidateDescriptionEvolution(candidate, existingDocuments, issues);

            if (issues.Any(issue => issue.Severity == ValidationSeverity.Critical))
            {
                result.QuarantinedItems.Add(new QuarantineItem
                {
                    RawFolderPath = candidate.RawFolderPath,
                    RawFilePath = candidate.RawFilePath,
                    IntendedIssueDate = candidate.ParsedIssueDate,
                    SuggestedFolderName = BuildCanonicalFolderName(candidate.ParsedIssueDate),
                    SuggestedFileName = BuildCanonicalFileName(candidate),
                    Issues = issues
                });
                continue;
            }

            result.ValidItems.Add(candidate);
            result.WarningIssues.AddRange(issues.Where(issue => issue.Severity != ValidationSeverity.Critical));
        }

        return result;
    }

    private static void ValidateFolderDate(ScanCandidate candidate, List<ScanValidationIssue> issues)
    {
        if (candidate.ParsedIssueDate is null)
        {
            issues.Add(new ScanValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                RuleCode = "FolderDateFormat",
                Message = "Issue folder date could not be parsed.",
                SuggestedValue = string.Empty,
                CanAutoApply = false
            });
            return;
        }

        var canonicalFolderName = candidate.ParsedIssueDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (!string.Equals(candidate.DetectedFolderDateText, canonicalFolderName, StringComparison.Ordinal))
        {
            issues.Add(new ScanValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                RuleCode = "FolderDateFormat",
                Message = $"Folder name '{candidate.DetectedFolderDateText}' is not in YYYYMMDD format.",
                SuggestedValue = canonicalFolderName,
                CanAutoApply = true
            });
        }
    }
}
```

- [ ] **Step 3: Update import results so the UI can show imported, quarantined, and warning sections**

```csharp
public class ImportResult
{
    public int TotalPdfFiles { get; set; }
    public int SuccessfullyParsed { get; set; }
    public List<string> ImportedFilePaths { get; set; } = new();
    public List<SkippedFileInfo> SkippedFiles { get; set; } = new();
    public List<FileRenameInfo> SuggestedRenames { get; set; } = new();
    public List<QuarantineItem> QuarantinedItems { get; set; } = new();
    public List<ScanValidationIssue> WarningIssues { get; set; } = new();

    public bool HasSkippedFiles => SkippedFiles.Count > 0;
    public bool HasSuggestedRenames => SuggestedRenames.Count > 0;
    public bool HasQuarantineItems => QuarantinedItems.Count > 0;
    public bool HasWarnings => WarningIssues.Count > 0;
}
```

- [ ] **Step 4: Run the validation tests to verify they pass**

Run: `dotnet test DrawingRegister.App.Tests --filter ScanValidationServiceTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DrawingRegister.App/Models/ScanCandidate.cs DrawingRegister.App/Models/ScanValidationIssue.cs DrawingRegister.App/Models/QuarantineItem.cs DrawingRegister.App/Services/ScanValidationService.cs DrawingRegister.App/Models/ImportResult.cs DrawingRegister.App/Models/DocumentMetadata.cs DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs
git commit -m "feat: add scan validation and quarantine models"
```

### Task 5: Integrate repository and validation into ProjectManager import

**Files:**
- Create: `DrawingRegister.App.Tests/Services/ProjectManagerImportTests.cs`
- Modify: `DrawingRegister.App/Models/ProjectManager.cs`
- Modify: `DrawingRegister.App/Models/DocumentMetadata.cs`
- Modify: `DrawingRegister.App/Services/DocumentFilterService.cs`
- Test: `DrawingRegister.App.Tests/Services/ProjectManagerImportTests.cs`

- [ ] **Step 1: Write failing integration tests around import behavior**

```csharp
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class ProjectManagerImportTests
{
    [Fact]
    public void ImportDocuments_excludes_quarantined_items_from_live_register()
    {
        using var workspace = new TestWorkspace();
        CreatePdf(workspace.RootPath, "15-04-2026", "10000-MJ-00-XX-DR-S-20-0001.pdf");
        CreatePdf(workspace.RootPath, "20260420", "10000-MJ-00-XX-DR-S-20-0002.pdf");

        var manager = new ProjectManager(new ProjectStorageRepository(), new ScanValidationService());
        var result = manager.ImportDocuments(workspace.RootPath);

        Assert.Single(manager.Documents);
        Assert.Equal("10000-MJ-00-XX-DR-S-20-0002", manager.Documents[0].DocumentNumber);
        Assert.Single(result.QuarantinedItems);
        Assert.Equal("20260415", result.QuarantinedItems[0].SuggestedFolderName);
    }

    [Fact]
    public void ImportDocuments_uses_latest_valid_description_for_document_row_and_keeps_revision_history_description()
    {
        using var workspace = new TestWorkspace();
        CreatePdf(workspace.RootPath, "20260415", "10000-MJ-00-XX-DR-S-20-0003_Plan.pdf");
        CreatePdf(workspace.RootPath, "20260420", "10000-MJ-00-XX-DR-S-20-0003_Updated_Plan.pdf");

        var manager = new ProjectManager(new ProjectStorageRepository(), new ScanValidationService());
        manager.ImportDocuments(workspace.RootPath);

        var document = Assert.Single(manager.Documents);
        Assert.Equal("UPDATED PLAN", document.Description);
        Assert.Equal("PLAN", document.RevisionHistory[new DateTime(2026, 4, 15)].Description);
        Assert.Equal("UPDATED PLAN", document.RevisionHistory[new DateTime(2026, 4, 20)].Description);
    }
}
```

- [ ] **Step 2: Refactor ProjectManager to depend on repository and validator**

```csharp
namespace DrawingRegister.App.Models;

public class ProjectManager : INotifyPropertyChanged
{
    private readonly ProjectStorageRepository _repository;
    private readonly ScanValidationService _validator;

    public ProjectManager()
        : this(new ProjectStorageRepository(), new ScanValidationService())
    {
    }

    internal ProjectManager(ProjectStorageRepository repository, ScanValidationService validator)
    {
        _repository = repository;
        _validator = validator;
    }
}
```

- [ ] **Step 3: Update import flow so only validated items become documents**

```csharp
public ImportResult ImportDocuments(string folderPath, string? specificFolderToRescanFullPath = null)
{
    var snapshot = _repository.OpenOrMigrate(folderPath);
    var candidates = ScanCandidates(folderPath, specificFolderToRescanFullPath);
    var validation = _validator.Validate(candidates, snapshot.Documents);

    ApplyValidCandidates(snapshot, validation.ValidItems);
    snapshot.QuarantineItems = validation.QuarantinedItems;
    _repository.Save(folderPath, snapshot);
    LoadDocumentsFromSnapshot(snapshot);

    return new ImportResult
    {
        TotalPdfFiles = candidates.Count,
        SuccessfullyParsed = validation.ValidItems.Count,
        ImportedFilePaths = validation.ValidItems.Select(item => item.RawFilePath).ToList(),
        QuarantinedItems = validation.QuarantinedItems,
        WarningIssues = validation.WarningIssues
    };
}
```

- [ ] **Step 4: Ensure document row description comes from the latest valid revision**

```csharp
var latestRevisionEntry = metadata.RevisionHistory.OrderByDescending(entry => entry.Key).First();
metadata.Description = latestRevisionEntry.Value.Description;
metadata.FilePath = latestRevisionEntry.Value.FilePath;
metadata.PurposeOfIssue = latestRevisionEntry.Value.Purpose;
metadata.MethodOfIssue = latestRevisionEntry.Value.Method;
metadata.IssuedBy = latestRevisionEntry.Value.IssuedBy;
```

- [ ] **Step 5: Run the ProjectManager tests to verify they pass**

Run: `dotnet test DrawingRegister.App.Tests --filter ProjectManagerImportTests`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add DrawingRegister.App/Models/ProjectManager.cs DrawingRegister.App/Models/DocumentMetadata.cs DrawingRegister.App/Services/DocumentFilterService.cs DrawingRegister.App.Tests/Services/ProjectManagerImportTests.cs
git commit -m "feat: validate scans before register import"
```

### Task 6: Add failing tests for in-app fix application

**Files:**
- Create: `DrawingRegister.App.Tests/Services/ValidationFixServiceTests.cs`
- Create: `DrawingRegister.App/Services/ValidationFixService.cs`
- Test: `DrawingRegister.App.Tests/Services/ValidationFixServiceTests.cs`

- [ ] **Step 1: Write failing tests for safe rename fixes**

```csharp
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class ValidationFixServiceTests
{
    [Fact]
    public void ApplySuggestedFolderRename_renames_folder_and_preserves_target_date()
    {
        using var workspace = new TestWorkspace();
        var oldFolder = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "15-04-2026"));
        var item = new QuarantineItem
        {
            RawFolderPath = oldFolder.FullName,
            IntendedIssueDate = new DateTime(2026, 4, 15),
            SuggestedFolderName = "20260415",
            Issues =
            [
                new ScanValidationIssue
                {
                    RuleCode = "FolderDateFormat",
                    Severity = ValidationSeverity.Critical,
                    SuggestedValue = "20260415",
                    CanAutoApply = true
                }
            ]
        };

        var service = new ValidationFixService();
        var applied = service.ApplySuggestedFix(item);

        Assert.True(applied);
        Assert.False(Directory.Exists(oldFolder.FullName));
        Assert.True(Directory.Exists(Path.Combine(workspace.RootPath, "20260415")));
        Assert.Equal(new DateTime(2026, 4, 15), item.IntendedIssueDate);
    }

    [Fact]
    public void ApplySuggestedFix_returns_false_when_destination_already_exists()
    {
        using var workspace = new TestWorkspace();
        var oldFolder = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "15-04-2026"));
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, "20260415"));

        var item = new QuarantineItem
        {
            RawFolderPath = oldFolder.FullName,
            SuggestedFolderName = "20260415",
            Issues =
            [
                new ScanValidationIssue
                {
                    RuleCode = "FolderDateFormat",
                    Severity = ValidationSeverity.Critical,
                    SuggestedValue = "20260415",
                    CanAutoApply = true
                }
            ]
        };

        var applied = new ValidationFixService().ApplySuggestedFix(item);

        Assert.False(applied);
        Assert.True(Directory.Exists(oldFolder.FullName));
    }
}
```

- [ ] **Step 2: Run the fix service tests to verify they fail**

Run: `dotnet test DrawingRegister.App.Tests --filter ValidationFixServiceTests`

Expected: FAIL because `ValidationFixService` does not exist yet.

- [ ] **Step 3: Implement the fix service**

```csharp
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public sealed class ValidationFixService
{
    public bool ApplySuggestedFix(QuarantineItem item)
    {
        var folderIssue = item.Issues.FirstOrDefault(issue =>
            issue.RuleCode == "FolderDateFormat" && issue.CanAutoApply);

        if (folderIssue is not null && !string.IsNullOrWhiteSpace(item.SuggestedFolderName))
        {
            var sourceDirectory = item.RawFolderPath;
            var destinationDirectory = Path.Combine(
                Directory.GetParent(sourceDirectory)!.FullName,
                item.SuggestedFolderName);

            if (Directory.Exists(destinationDirectory))
            {
                return false;
            }

            Directory.Move(sourceDirectory, destinationDirectory);
            item.RawFolderPath = destinationDirectory;
            item.ResolutionState = QuarantineResolutionState.Fixed;
            return true;
        }

        return false;
    }
}
```

- [ ] **Step 4: Run the fix service tests to verify they pass**

Run: `dotnet test DrawingRegister.App.Tests --filter ValidationFixServiceTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DrawingRegister.App/Services/ValidationFixService.cs DrawingRegister.App.Tests/Services/ValidationFixServiceTests.cs
git commit -m "feat: add validation fix service"
```

### Task 7: Add the validation results dialog and wire it into the main workflow

**Files:**
- Create: `DrawingRegister.App/ScanValidationDialog.xaml`
- Create: `DrawingRegister.App/ScanValidationDialog.xaml.cs`
- Modify: `DrawingRegister.App/MainWindow.xaml.cs`
- Test: manual verification

- [ ] **Step 1: Add the validation dialog layout**

```xml
<Window x:Class="DrawingRegister.App.ScanValidationDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Scan Validation Results"
        Height="720"
        Width="1100">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Margin="0,0,0,12">
            <TextBlock Text="Scan Validation Results" FontSize="20" FontWeight="SemiBold" />
            <TextBlock Text="{Binding SummaryText}" Margin="0,4,0,0" />
        </StackPanel>

        <TabControl Grid.Row="1">
            <TabItem Header="Imported">
                <DataGrid ItemsSource="{Binding ImportedItems}" IsReadOnly="True" AutoGenerateColumns="False" />
            </TabItem>
            <TabItem Header="Quarantined">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*" />
                        <RowDefinition Height="Auto" />
                    </Grid.RowDefinitions>
                    <DataGrid x:Name="QuarantineGrid" ItemsSource="{Binding QuarantineItems}" AutoGenerateColumns="False" />
                    <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
                        <Button Content="Open Folder" Click="OpenFolder_Click" Margin="0,0,8,0" />
                        <Button Content="Copy Path" Click="CopyPath_Click" Margin="0,0,8,0" />
                        <Button Content="Apply Suggested Fix" Click="ApplySuggestedFix_Click" Margin="0,0,8,0" />
                        <Button Content="Revalidate" Click="Revalidate_Click" />
                    </StackPanel>
                </Grid>
            </TabItem>
            <TabItem Header="Warnings">
                <DataGrid ItemsSource="{Binding WarningIssues}" IsReadOnly="True" AutoGenerateColumns="False" />
            </TabItem>
        </TabControl>

        <Button Grid.Row="2" Content="Close" HorizontalAlignment="Right" Width="100" Margin="0,12,0,0" Click="Close_Click" />
    </Grid>
</Window>
```

- [ ] **Step 2: Add dialog behavior using the fix service**

```csharp
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace DrawingRegister.App;

public partial class ScanValidationDialog : Window
{
    private readonly ValidationFixService _fixService = new();

    public ScanValidationDialog(ImportResult result)
    {
        InitializeComponent();
        ImportedItems = new ObservableCollection<string>(
            result.ImportedFilePaths);
        QuarantineItems = new ObservableCollection<QuarantineItem>(result.QuarantinedItems);
        WarningIssues = new ObservableCollection<ScanValidationIssue>(result.WarningIssues);
        SummaryText = $"{ImportedItems.Count} imported, {QuarantineItems.Count} quarantined, {WarningIssues.Count} warnings.";
        DataContext = this;
    }

    public ObservableCollection<string> ImportedItems { get; }
    public ObservableCollection<QuarantineItem> QuarantineItems { get; }
    public ObservableCollection<ScanValidationIssue> WarningIssues { get; }
    public string SummaryText { get; }

    private void ApplySuggestedFix_Click(object sender, RoutedEventArgs e)
    {
        if (QuarantineGrid.SelectedItem is not QuarantineItem item)
        {
            return;
        }

        if (_fixService.ApplySuggestedFix(item))
        {
            MessageBox.Show("Suggested fix applied. Run revalidate to rescan the corrected item.");
        }
    }
}
```

- [ ] **Step 3: Show the dialog after scans that produce quarantine items or warnings**

```csharp
private void ImportDocuments_Click(object sender, RoutedEventArgs e)
{
    var result = _project.ImportDocuments(selectedFolderPath);
    RefreshViewFromProject();

    if (result.HasQuarantineItems || result.HasWarnings)
    {
        var dialog = new ScanValidationDialog(result) { Owner = this };
        dialog.ShowDialog();
    }
}
```

- [ ] **Step 4: Run a solution build**

Run: `dotnet build drawingregisterMJ.sln -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add DrawingRegister.App/ScanValidationDialog.xaml DrawingRegister.App/ScanValidationDialog.xaml.cs DrawingRegister.App/MainWindow.xaml.cs
git commit -m "feat: add scan validation results dialog"
```

### Task 8: Full verification and regression check

**Files:**
- Modify: none expected unless verification finds issues
- Test: `DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs`
- Test: `DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs`
- Test: `DrawingRegister.App.Tests/Services/ValidationFixServiceTests.cs`
- Test: `DrawingRegister.App.Tests/Services/ProjectManagerImportTests.cs`

- [ ] **Step 1: Run the focused test suite**

Run: `dotnet test DrawingRegister.App.Tests --filter "ProjectStorageRepositoryTests|ScanValidationServiceTests|ValidationFixServiceTests|ProjectManagerImportTests"`

Expected: PASS for all four test classes

- [ ] **Step 2: Run the existing regression tests**

Run: `dotnet test DrawingRegister.App.Tests --filter "DocumentFilterServiceTests|DrawingRegisterPdfReportBuilderTests|FileOperationsExportTests"`

Expected: PASS

- [ ] **Step 3: Run a full solution build**

Run: `dotnet build drawingregisterMJ.sln -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 4: Manual verification checklist**

Verify in the app:

- scanning a folder named `15-04-2026` quarantines the files and suggests `20260415`
- applying the suggested folder fix renames the folder in place
- revalidating imports the files against the corrected historical date
- the main register excludes quarantined items until they are fixed
- the latest revision’s description is shown in the main grid while older revisions keep their original description in history
- same-date duplicate drawing numbers with materially different descriptions remain blocked for manual review

- [ ] **Step 5: Final commit if any verification fixes were needed**

```bash
git add DrawingRegister.App/Models/ProjectManager.cs DrawingRegister.App/MainWindow.xaml.cs DrawingRegister.App/ScanValidationDialog.xaml DrawingRegister.App/ScanValidationDialog.xaml.cs DrawingRegister.App/Services/ProjectStorageRepository.cs DrawingRegister.App/Services/ScanValidationService.cs DrawingRegister.App/Services/ValidationFixService.cs DrawingRegister.App.Tests/Services/ProjectStorageRepositoryTests.cs DrawingRegister.App.Tests/Services/ScanValidationServiceTests.cs DrawingRegister.App.Tests/Services/ValidationFixServiceTests.cs DrawingRegister.App.Tests/Services/ProjectManagerImportTests.cs
git commit -m "test: verify scan validation workflow"
```
