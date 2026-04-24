# DocReg Document Support

**Date:** 2026-04-24
**Status:** Approved

## Problem

The Scottish SER (Structural Engineer's Registration) process requires a
"Document Register" PDF to be included in each warrant issue. By SER rule the
file must be named exactly:

```
DocReg-<projectNo>-<yyyyMMdd>.pdf
```

Example from `G:\My Drive\M+J\DIROOTS\PDF\test pdf\20260424-WARRANT ISSUE - STRUCTURAL\`:

```
DocReg-124379-20260422.pdf
```

The existing import regex in `DrawingRegister.App/Models/ProjectManager.cs:509`
requires every filename to begin with a 5–6 digit project number followed by a
standard 9-part drawing reference (`PROJECT-ORIGINATOR-VOL-LEVEL-TYPE-DISC-PKG-NO-REV-DESC`).
DocReg filenames do not match that shape, so they are skipped with the reason
"Filename format not recognised" and never appear in the register.

## Goal

Accept `DocReg-<projectNo>-<yyyyMMdd>.pdf` as a first-class document during
import, alongside drawings, certificates (`-CE-`), and letters (`-LT-`),
without renaming the file or altering the SER-mandated format.

## Non-goals

- No changes to UI filters, PDF report generation, or collect-latest-PDFs logic.
  DocReg rows will flow through them automatically because they use the same
  `DocumentMetadata` shape.
- No special UI styling or filter chip for DocReg rows. That is a possible
  follow-up.
- No support for DocReg filenames outside the strict `DocReg-<projectNo>-<yyyyMMdd>.pdf`
  pattern. Anything else falls through to the standard regex and is skipped as
  today.

## Design

### Matching

Add a DocReg-specific check **before** the existing standard-drawing regex in
the per-file loop at `ProjectManager.cs` ~line 508.

Pattern:

```regex
^DocReg-(?<projectNo>\d{5,6})-(?<fileDate>\d{8})$
```

- Case-insensitive on the `DocReg` literal (accepts `DocReg`, `DOCREG`, `docreg`).
- Applied to the sanitised base filename (same sanitisation as drawings).
- On match: build a `DocumentMetadata` using the DocReg defaults below and
  `continue` to the next file (skip the drawing regex).
- On no match: fall through to the existing drawing regex unchanged.
- The `fileDate` capture group enforces the SER format (`yyyyMMdd`, 8 digits)
  but is **not** used as the issue date — see Field mapping. A filename with a
  non-8-digit tail fails the regex and falls through.

### Field mapping

| `DocumentMetadata` field | Value |
|---|---|
| `DocumentNumber` | Full base filename, e.g. `DocReg-124379-20260422` |
| `DocumentType` | `DOCREG` |
| `Description` | `Document Register` |
| `Discipline` | *(empty string)* |
| `Package` | *(empty string)* |
| `Revision` | `-` |
| `IssueDate` | Parsed from parent date folder, same as drawings |
| `FilePath` | Original path; filename preserved verbatim |
| `ProjectNumber` | From filename `projectNo` group |
| `Size` | Auto-detected via `DetermineDrawingSize` (PdfPig) |
| `PurposeOfIssue`, `MethodOfIssue`, `IssuedBy` | Same detection as drawings in the same batch |

### Revisions

Each DocReg filename embeds its compile date, so every issue is a **new
register row**, not a new revision of an existing row. The
`RevisionHistory` dictionary on a DocReg `DocumentMetadata` contains one entry:
the issue date from the parent folder mapped to a `RevisionInfo { Revision = "-",
… }`. No changes are needed to `GenerateRevisionCode` or
`LatestNonSupersededRevision`.

### Supporting change

Add one case to `ProjectManager.GetDrawingTypeDescription` (line 909) so the
type label reads `DOCUMENT REGISTER` wherever `GetDrawingTypeDescription` is
called:

```csharp
case "DOCREG": return "DOCUMENT REGISTER";
```

### Edge cases

- **Malformed date** (e.g. `DocReg-124379-2026042.pdf`) — DocReg regex fails,
  falls through to drawing regex, fails there, skipped as "Filename format not
  recognised". Existing behaviour.
- **Case variants** (`DOCREG-…`, `docreg-…`) — accepted by the
  case-insensitive match; stored type code is always `DOCREG`.
- **Project-number mismatch** — after DocReg parsing, run the same
  `fileProjectNo != ProjectNumber` check used for drawings; mismatches are
  added to `importResult.SkippedFiles` with a descriptive reason.
- **Duplicate file on re-scan** — same file path parsed again should update
  the existing `DocumentMetadata` row, not duplicate it. This is existing
  behaviour in the per-file loop and relies on `DocumentNumber` being stable;
  the full-filename-as-DocumentNumber choice guarantees this.

## Testing

### Unit tests (`DrawingRegister.App.Tests`)

Add a `DocRegImportTests` class covering:

1. `DocReg-124379-20260422.pdf` → parsed, correct field values.
2. Uppercase variant `DOCREG-124379-20260422.pdf` → parsed.
3. Lowercase variant `docreg-124379-20260422.pdf` → parsed.
4. Malformed date `DocReg-124379-2026042.pdf` → skipped as unrecognised.
5. Mismatched project `DocReg-999999-20260422.pdf` inside a 124379 project →
   skipped with project-mismatch reason.
6. Filename preserved in `FilePath` and `DocumentNumber` exactly as on disk.

### Manual verification

Scan `G:\My Drive\M+J\DIROOTS\PDF\test pdf\20260424-WARRANT ISSUE - STRUCTURAL\`
and confirm:

- `DocReg-124379-20260422.pdf` appears as a row in the register.
- Its fields match the mapping table above.
- All 15 existing drawings/certificates/letters in the folder still import
  correctly (no regression from the new DocReg branch).
- Issue date column shows **24 Apr 2026** (from the folder), not 22 Apr (from
  the filename).

## Files touched

- `DrawingRegister.App/Models/ProjectManager.cs` — new DocReg branch in the
  per-file loop; new case in `GetDrawingTypeDescription`.
- `DrawingRegister.App.Tests/` — new `DocRegImportTests.cs`.
