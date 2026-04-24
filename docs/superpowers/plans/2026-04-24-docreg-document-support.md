# DocReg Document Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Accept `DocReg-<projectNo>-<yyyyMMdd>.pdf` (SER-mandated format) as a first-class document during import, so Scottish warrant-issue batches register correctly.

**Architecture:** Extract DocReg filename parsing into a small static helper `DocRegFilenameParser` for isolation and testability. Wire it into `ProjectManager`'s per-file import loop as a pre-check before the existing drawing regex. DocReg matches short-circuit into a dedicated metadata-build branch; non-matches fall through unchanged.

**Tech Stack:** .NET 8, xUnit, existing `UglyToad.PdfPig` for size detection.

**Spec:** `docs/superpowers/specs/2026-04-24-docreg-document-support-design.md`

---

## File Structure

- **Create:** `DrawingRegister.App/Services/DocRegFilenameParser.cs` — static parser with `TryParse` + `DocRegMatch` record struct. Single responsibility: recognise the SER filename format.
- **Modify:** `DrawingRegister.App/Models/ProjectManager.cs` — new branch in the per-file loop (~line 508, before the drawing regex) and a new case in `GetDrawingTypeDescription` (line 909).
- **Create:** `DrawingRegister.App.Tests/Services/DocRegFilenameParserTests.cs` — xUnit theories covering the parser contract.
- **Create:** `DrawingRegister.App.Tests/Models/ProjectManagerDocRegImportTests.cs` — integration test scanning a temp folder that contains a real (fixture) DocReg PDF alongside a standard drawing, verifying the resulting `DocumentMetadata`.

---

## Task 1: DocRegFilenameParser (TDD)

Isolates the SER filename grammar from the import loop. The parser is pure (no IO), making the contract easy to lock in.

**Files:**
- Create: `DrawingRegister.App/Services/DocRegFilenameParser.cs`
- Test: `DrawingRegister.App.Tests/Services/DocRegFilenameParserTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `DrawingRegister.App.Tests/Services/DocRegFilenameParserTests.cs`:

```csharp
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

/// <summary>
/// Lock-in tests for the SER DocReg filename grammar
/// (DocReg-&lt;projectNo&gt;-&lt;yyyyMMdd&gt;.pdf).
/// </summary>
public sealed class DocRegFilenameParserTests
{
    [Theory]
    [InlineData("DocReg-124379-20260422", "124379", "20260422")]
    [InlineData("DOCREG-124379-20260422", "124379", "20260422")]
    [InlineData("docreg-124379-20260422", "124379", "20260422")]
    [InlineData("DocReg-99999-20260422", "99999", "20260422")]     // 5-digit project
    [InlineData("DocReg-124379-20261231", "124379", "20261231")]
    public void TryParse_valid_filename_returns_true(
        string fileStem,
        string expectedProject,
        string expectedDate)
    {
        var ok = DocRegFilenameParser.TryParse(fileStem, out var match);

        Assert.True(ok);
        Assert.Equal(expectedProject, match.ProjectNumber);
        Assert.Equal(expectedDate, match.FileDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DocReg-124379-2026042")]          // 7-digit date, too short
    [InlineData("DocReg-124379-202604221")]        // 9-digit date, too long
    [InlineData("DocReg-12-20260422")]             // project too short
    [InlineData("DocReg-1234567-20260422")]        // project too long
    [InlineData("DocReg-124379")]                  // missing date
    [InlineData("124379-DocReg-20260422")]         // wrong order
    [InlineData("DocRegistry-124379-20260422")]    // wrong literal
    [InlineData("DocReg_124379_20260422")]         // underscores instead of hyphens
    [InlineData("DocReg-124379-20260422-extra")]   // trailing content
    [InlineData("124379-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN & DETAILS")] // standard drawing
    public void TryParse_invalid_filename_returns_false(string fileStem)
    {
        var ok = DocRegFilenameParser.TryParse(fileStem, out var match);

        Assert.False(ok);
        Assert.Equal(default, match);
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

Run:
```bash
dotnet test DrawingRegister.App.Tests --filter FullyQualifiedName~DocRegFilenameParserTests
```

Expected: compile error — `DocRegFilenameParser` type does not exist.

- [ ] **Step 3: Create the parser**

Create `DrawingRegister.App/Services/DocRegFilenameParser.cs`:

```csharp
using System.Text.RegularExpressions;

namespace DrawingRegister.App.Services;

/// <summary>
/// Parses SER-mandated Document Register filenames of the form
/// <c>DocReg-&lt;projectNo&gt;-&lt;yyyyMMdd&gt;</c> (extension already stripped).
/// The grammar is strict because the Scottish SER process requires the exact
/// format on the file that appears in the warrant bundle.
/// </summary>
public static class DocRegFilenameParser
{
    private static readonly Regex Pattern = new(
        @"^DocReg-(?<projectNo>\d{5,6})-(?<fileDate>\d{8})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParse(string fileNameWithoutExtension, out DocRegMatch result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return false;

        var m = Pattern.Match(fileNameWithoutExtension);
        if (!m.Success)
            return false;

        result = new DocRegMatch(
            ProjectNumber: m.Groups["projectNo"].Value,
            FileDate: m.Groups["fileDate"].Value);
        return true;
    }
}

public readonly record struct DocRegMatch(string ProjectNumber, string FileDate);
```

- [ ] **Step 4: Run tests, verify they pass**

Run:
```bash
dotnet test DrawingRegister.App.Tests --filter FullyQualifiedName~DocRegFilenameParserTests
```

Expected: all 16 test cases pass (5 valid theories + 11 invalid theories).

- [ ] **Step 5: Commit**

```bash
git add DrawingRegister.App/Services/DocRegFilenameParser.cs \
        DrawingRegister.App.Tests/Services/DocRegFilenameParserTests.cs
git commit -m "feat: add DocRegFilenameParser for SER filename format

Strict parser for DocReg-<projectNo>-<yyyyMMdd>.pdf. Case-insensitive
on the 'DocReg' literal; 8-digit date and 5-6 digit project enforced."
```

---

## Task 2: Wire DocReg branch into ProjectManager import loop

Insert the DocReg branch before the existing drawing regex at `ProjectManager.cs:509`. On match, build a `DocumentMetadata` using the DocReg defaults (from the spec's field-mapping table), run the project-number check, then `continue` the loop. On no match, fall through to the existing drawing-regex logic unchanged.

**Files:**
- Modify: `DrawingRegister.App/Models/ProjectManager.cs` (~line 504-509 and line 909)

- [ ] **Step 1: Read the current shape**

Open `DrawingRegister.App/Models/ProjectManager.cs` and confirm the per-file loop structure around lines 477-683:
- Line 504-509: sanitisation + drawing regex.
- Line 536: `documentNumber` constructed from match groups.
- Line 590-605: `new DocumentMetadata { … }` block.
- Line 617-654: `revisionKey` computation + add-or-update existing doc.
- Line 656: `importResult.SuccessfullyParsed++;`.
- Line 658-682: canonical rename suggestion (skip for DocReg — filename is already canonical).
- Line 909: `GetDrawingTypeDescription` switch.

- [ ] **Step 2: Add the DocReg branch**

In `DrawingRegister.App/Models/ProjectManager.cs`, replace the block beginning at line 504 (`// Sanitize: collapse multiple consecutive hyphens…`) down to the end of the skip-on-no-match block (originally line 521, just after `continue;`) with the following. The new DocReg branch runs **before** the existing drawing regex, and non-matches fall through to the existing logic.

```csharp
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
```

- [ ] **Step 3: Add `DOCREG` case to `GetDrawingTypeDescription`**

In `DrawingRegister.App/Models/ProjectManager.cs` at line 911, inside `GetDrawingTypeDescription`:

Current:
```csharp
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
```

Change to:
```csharp
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
```

- [ ] **Step 4: Add the using directive**

Near the top of `ProjectManager.cs` (currently around line 10 after `using DrawingRegister.App.Helpers;`), add:

```csharp
using DrawingRegister.App.Services;
```

- [ ] **Step 5: Build the solution**

Run:
```bash
dotnet build DrawingRegister.App --nologo
```

Expected: `Build succeeded. 0 Error(s)`. Warnings acceptable if they already existed.

- [ ] **Step 6: Commit**

```bash
git add DrawingRegister.App/Models/ProjectManager.cs
git commit -m "feat: import DocReg-<projectNo>-<yyyyMMdd>.pdf as DOCREG type

Adds DocReg branch before the drawing regex in ImportDocuments.
Uses the full base filename as DocumentNumber so each dated issue
is its own register row. SER filename is preserved verbatim."
```

---

## Task 3: Integration test — DocReg is imported end-to-end

Exercise the real `ProjectManager.ImportDocuments` pipeline against a temp folder that mimics a warrant-issue batch: one standard drawing and one DocReg file. Confirms the branch is wired, project-mismatch is enforced, and the other documents aren't affected.

**Files:**
- Create: `DrawingRegister.App.Tests/Models/ProjectManagerDocRegImportTests.cs`

- [ ] **Step 1: Confirm how `ImportDocuments` is invoked**

Open `DrawingRegister.App/Models/ProjectManager.cs` and find the `ImportDocuments` entry point (the method containing the `dateDirectories` and `pdfFiles` logic shown in the plan context). Confirm the public signature and any required setup (e.g. `_currentBasePath`, `_currentStorage`) the test needs. Note the method name and parameters — some projects wrap this as `ImportDocuments(string basePath)` and some as a parameterless method that reads `_currentBasePath`. Use whichever signature the file actually exposes.

If the entry point isn't reachable without UI glue, skip this task and rely on Task 4 (manual verification). Document the skip in the final commit message.

- [ ] **Step 2: Build a minimal PDF fixture helper**

Option A (preferred if already available): reuse any existing test helper that writes a minimal PDF. Search:
```bash
grep -rn "PdfDocumentBuilder\|MinimalPdf\|WritePdf" DrawingRegister.App.Tests
```

Option B (if no helper exists): write a tiny 1-page PDF using `UglyToad.PdfPig.Writer.PdfDocumentBuilder` (already a dependency of the app). Add this static helper inside the new test class:

```csharp
using UglyToad.PdfPig.Writer;

private static void WriteMinimalA4Pdf(string path)
{
    var builder = new PdfDocumentBuilder();
    builder.AddPage(PageSize.A4);
    File.WriteAllBytes(path, builder.Build());
}
```

- [ ] **Step 3: Write the failing test**

Create `DrawingRegister.App.Tests/Models/ProjectManagerDocRegImportTests.cs`:

```csharp
using System.IO;
using System.Linq;
using DrawingRegister.App.Models;
using UglyToad.PdfPig.Writer;

namespace DrawingRegister.App.Tests.Models;

public sealed class ProjectManagerDocRegImportTests : IDisposable
{
    private readonly string _basePath;

    public ProjectManagerDocRegImportTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dr-docreg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    [Fact]
    public void ImportDocuments_registers_DocReg_file_as_DOCREG_type()
    {
        var dateFolder = Path.Combine(_basePath, "20260424-WARRANT ISSUE - STRUCTURAL");
        Directory.CreateDirectory(dateFolder);

        WriteMinimalA4Pdf(Path.Combine(dateFolder, "124379-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf"));
        WriteMinimalA4Pdf(Path.Combine(dateFolder, "DocReg-124379-20260422.pdf"));

        var pm = new ProjectManager { ProjectNumber = "124379" };
        var result = InvokeImport(pm, _basePath);

        Assert.Equal(2, result.SuccessfullyParsed);
        Assert.Empty(result.SkippedFiles);

        var docReg = pm.Documents.SingleOrDefault(d => d.DocumentType == "DOCREG");
        Assert.NotNull(docReg);
        Assert.Equal("DocReg-124379-20260422", docReg!.DocumentNumber);
        Assert.Equal("Document Register", docReg.Description);
        Assert.Equal("-", docReg.RevisionHistory.Values.Single().Revision);
        Assert.Equal(string.Empty, docReg.Discipline);
        Assert.Equal(string.Empty, docReg.Package);
        Assert.Equal(
            new DateTime(2026, 4, 24),
            docReg.RevisionHistory.Keys.Single().Date);
        Assert.EndsWith("DocReg-124379-20260422.pdf", docReg.FilePath);
    }

    [Fact]
    public void ImportDocuments_skips_DocReg_with_project_mismatch()
    {
        var dateFolder = Path.Combine(_basePath, "20260424-WARRANT ISSUE - STRUCTURAL");
        Directory.CreateDirectory(dateFolder);

        WriteMinimalA4Pdf(Path.Combine(dateFolder, "124379-M+J-V1-XX-DR-S-16-01-T01-FOUNDATION PLAN.pdf"));
        WriteMinimalA4Pdf(Path.Combine(dateFolder, "DocReg-999999-20260422.pdf"));

        var pm = new ProjectManager { ProjectNumber = "124379" };
        var result = InvokeImport(pm, _basePath);

        Assert.Equal(1, result.SuccessfullyParsed);
        var skipped = Assert.Single(result.SkippedFiles);
        Assert.Contains("Project number mismatch", skipped.Reason);
        Assert.Contains("999999", skipped.Reason);
        Assert.DoesNotContain(pm.Documents, d => d.DocumentType == "DOCREG");
    }

    [Fact]
    public void ImportDocuments_accepts_case_variant_DOCREG_filename()
    {
        var dateFolder = Path.Combine(_basePath, "20260424-WARRANT ISSUE - STRUCTURAL");
        Directory.CreateDirectory(dateFolder);

        WriteMinimalA4Pdf(Path.Combine(dateFolder, "DOCREG-124379-20260422.pdf"));

        var pm = new ProjectManager { ProjectNumber = "124379" };
        var result = InvokeImport(pm, _basePath);

        Assert.Equal(1, result.SuccessfullyParsed);
        Assert.Single(pm.Documents);
        Assert.Equal("DOCREG", pm.Documents[0].DocumentType);
        Assert.Equal("DOCREG-124379-20260422", pm.Documents[0].DocumentNumber);
    }

    // ---- helpers ---------------------------------------------------------

    private static ImportResult InvokeImport(ProjectManager pm, string basePath)
    {
        // Replace this body with whatever signature ImportDocuments actually exposes.
        // If it takes (string basePath): return pm.ImportDocuments(basePath);
        // If it uses _currentBasePath: pm._currentBasePath = basePath; return pm.ImportDocuments();
        return pm.ImportDocuments(basePath);
    }

    private static void WriteMinimalA4Pdf(string path)
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        File.WriteAllBytes(path, builder.Build());
    }
}
```

Adjust `InvokeImport` to match the actual `ImportDocuments` signature you confirmed in Step 1.

- [ ] **Step 4: Run tests, confirm results**

Run:
```bash
dotnet test DrawingRegister.App.Tests --filter FullyQualifiedName~ProjectManagerDocRegImportTests
```

Expected: all 3 tests pass. If `ImportDocuments` throws because of missing storage / project-info setup, add the minimum required setup (e.g. calling whatever initialises `_currentStorage`) to the test constructor. Look at how other `ProjectManager` tests in the repo bootstrap, if any, and mirror them.

If after reasonable effort the pipeline can't be driven from a test without significant mocking, delete this test file and rely on Task 4 manual verification. Commit message must note the skip.

- [ ] **Step 5: Commit**

```bash
git add DrawingRegister.App.Tests/Models/ProjectManagerDocRegImportTests.cs
git commit -m "test: integration test for DocReg import

Covers happy path, project mismatch, and case-variant filename against
the real ProjectManager.ImportDocuments pipeline."
```

---

## Task 4: Manual end-to-end verification

The specced acceptance criterion: the real warrant-issue folder imports correctly.

- [ ] **Step 1: Launch the app**

Run:
```bash
dotnet run --project DrawingRegister.App -c Debug
```

- [ ] **Step 2: Scan the test folder**

In the app:
1. Create or open a project with `ProjectNumber = 124379`.
2. Point the scanner at: `G:\My Drive\M+J\DIROOTS\PDF\test pdf\` (the parent; the app walks date-prefixed subfolders).
3. Let the scan complete.

- [ ] **Step 3: Verify DocReg row**

Find the row with `DocumentNumber = DocReg-124379-20260422`. Confirm:

| Column | Expected value |
|---|---|
| Document Number | `DocReg-124379-20260422` |
| Type | `DOCREG` |
| Description | `Document Register` |
| Discipline | (empty) |
| Package | (empty) |
| Revision | `-` |
| Issue Date | `24 Apr 2026` *(from folder, not filename)* |
| Size | `A4` *(PdfPig-detected)* |

- [ ] **Step 4: Verify regression safety**

Confirm the other 15 PDFs in that folder still appear as they did before:
- 12 drawings (`-DR-S-…`) with their proper revisions.
- 1 certificate (`-CE-S-…`).
- 2 letters (`-LT-S-…`).

If any existing drawing now shows differently, the DocReg branch has broken the fall-through — investigate before declaring done.

- [ ] **Step 5: Commit any doc updates** (only if anything changed during verification)

```bash
git status
# If nothing to commit, skip this step.
```

---

## Self-Review

**Spec coverage**
- Strict matching rule → Task 1 (parser) + Task 2 (wired into loop before drawing regex) ✅
- Field mapping table → Task 2 metadata construction matches spec row-for-row ✅
- `DOCUMENT REGISTER` type label → Task 2 Step 3 ✅
- Revision-per-row behaviour → Task 2 stores one revision entry per DocReg file; Task 3 asserts it ✅
- Edge cases (case variants, malformed date, project mismatch) → Task 1 tests cover case/date/project shape; Task 3 covers mismatch and case variant at pipeline level ✅
- No UI/report changes → no tasks touch UI or report builder ✅

**Placeholder scan**
- No "TBD", "TODO", "handle edge cases" left. Task 3 Step 1 explicitly tells the engineer to check the real `ImportDocuments` signature because I couldn't confirm it without reading the full method — that's an actionable instruction, not a placeholder.

**Type consistency**
- `DocRegFilenameParser.TryParse(string, out DocRegMatch)` referenced identically in Task 1 Step 3 and Task 2 Step 2 ✅
- `DocRegMatch` fields `ProjectNumber` / `FileDate` consistent across parser + usage ✅
- `"DOCREG"` string used for `DocumentType` and in `GetDrawingTypeDescription` switch ✅
- `"Document Register"` used for `Description` in both Task 2 and Task 3 assertions ✅
