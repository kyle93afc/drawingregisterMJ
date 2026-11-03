# Drawing Register Scan Validation and Quarantine Design

Date: 2026-04-15
Topic: Add scan-time validation, quarantine, in-app fixes, and SQLite-backed project storage so invalid drawing data does not pollute the live register.

## Summary

The current scan flow is optimized for importing PDFs from dated issue folders into the register. That is useful, but it trusts folder naming, file naming, and drawing-number consistency too early.

This causes several real workflow problems:

- issue folders may use the wrong date format instead of the required `YYYYMMDD`
- filenames may drift away from the canonical drawing naming convention
- the same drawing number can be reused accidentally for genuinely different drawings
- bad inputs from different authoring tools can enter the register before anyone notices

This design adds a validation gate between disk scanning and live-register import.

The new model is:

- scan items are parsed first, not trusted immediately
- clean items import into the live register
- critical problems go to quarantine instead of the live register
- the app suggests fixes and can apply safe fixes in place
- once fixed and revalidated, historical items import using their intended issue date
- the live store moves from `project_data.json` to `project_data.db` to keep validation, quarantine, and audit operations fast

## Goals

- Prevent invalid folders and files from polluting the live register
- Enforce `YYYYMMDD` issue-folder naming as the canonical standard
- Detect malformed filenames, malformed drawing numbers, and conflicting duplicate drawing numbers
- Allow safe in-app fixes for obvious structural problems such as folder-date and filename formatting issues
- Preserve historical issue dates when quarantined items are fixed later
- Let drawing descriptions evolve over time while keeping the latest valid description as the register’s current description
- Keep scan and filter behavior fast as project data grows

## Non-Goals

- No attempt to auto-resolve genuine engineering conflicts where two different drawings appear to share one drawing number
- No attempt to infer missing drawings that do not exist on disk
- No machine-global database; storage remains per project
- No automatic backup system
- No git integration for the live database file

## User Problem Definition

The key issue is not just import convenience. The app needs to act as a quality gate for the register.

Examples the design must handle:

- a folder is named `15-04-2026` instead of `20260415`
- a file name uses the wrong separators or does not match the drawing number and description convention
- two users create different drawings in different authoring tools but both use the same drawing number
- a drawing description changes legitimately over the course of the project due to client requirements
- a bad historical folder or file is corrected later and must still land on the original issue date

## Source of Truth

The source of truth is split by responsibility:

- the file system is the source of raw evidence about folders and PDFs
- validation rules decide whether that evidence is trusted
- the live register contains only validated items
- quarantined items remain outside the live register until they pass validation

For description precedence:

- each revision keeps its own historical description
- the register’s current description for a drawing number comes from the latest valid revision

## User-Facing Behavior

The existing scan entry point remains the user’s main action.

After scanning:

- valid files are imported normally
- invalid files are withheld from the live register
- the user sees a post-scan validation results window

The validation results window should have three clear sections:

- `Imported`: files that were accepted into the live register
- `Quarantined`: files or folders blocked by critical validation issues
- `Warnings`: non-blocking issues that imported but need cleanup

Each issue should show:

- severity
- rule name
- file path or folder path
- detected value
- suggested canonical fix
- whether the fix can be auto-applied safely

Fast actions should include:

- `Open Folder`
- `Open File`
- `Copy Path`
- `Apply Suggested Fix` when safe
- `Revalidate`

## Validation Severity Model

Three severities are required.

### Critical

Critical issues do not enter the live register.

Examples:

- folder date is not in canonical `YYYYMMDD` form
- drawing number cannot be parsed confidently
- same drawing number appears to represent two different live documents on the same issue date
- same drawing number appears with materially different descriptions on the same issue date and the app cannot prove one is just a replacement

### Warning

Warnings import into the live register but remain visible in the validation results.

Examples:

- filename formatting drift that is still parseable
- inconsistent separators or casing
- weak or fallback metadata inference for purpose, method, or issuer

### Info

Informational issues do not block import and are low-priority cleanup items.

Examples:

- cosmetic naming normalization suggestions
- optional metadata gaps that do not compromise register identity

## Canonical Validation Rules

### Folder date rule

The canonical issue-folder format is `YYYYMMDD`.

Behavior:

- non-canonical formats such as `15-04-2026`, `15_04_2026`, or `2026-04-15` should be detected
- the app should suggest the canonical rename, for example `20260415`
- the app may offer an in-app folder rename
- the folder remains quarantined until renamed and revalidated

The app should not silently normalize non-canonical folder dates into the live register without the actual folder being corrected.

### Filename rule

Filenames should match the project’s canonical drawing naming convention closely enough that the app can trust the parsed drawing number, revision, and description.

Behavior:

- structural filename problems are flagged
- if the canonical filename can be derived with high confidence, the app suggests it and may rename in place
- low-confidence cases remain quarantined for user review

### Drawing number uniqueness rule

Drawing numbers identify a live drawing stream. The app must distinguish between valid revision history and genuine conflicts.

Behavior across different issue dates:

- the same drawing number is allowed
- description changes over time are allowed
- the latest valid revision supplies the register’s current description
- older revisions retain their historical descriptions

Behavior on the same issue date:

- if the same drawing number appears with effectively the same description and the files appear to be the same document, treat it as a replacement or duplicate-cleanup case
- if the same drawing number appears with materially different descriptions or appears to represent different documents, treat it as a critical ambiguity and quarantine it

The app must not auto-resolve true same-date ambiguities.

## Description Precedence Rules

Description changes are a normal part of early project evolution and must not be treated as automatic conflicts.

Rules:

- each revision record stores the description that belonged to that revision at the time
- the document row shown in the main register displays the latest valid description
- a description change across different issue dates is valid evolution, not a validation error by itself
- only same-date ambiguity between apparently different live documents should escalate to critical conflict

## Historical Correction Behavior

Quarantine must support back-dated recovery.

Rules:

- quarantined items are excluded from the live register until fixed
- once fixed and rescanned, they import using the intended issue date, not the date the fix was applied
- scan date and issue date must be tracked separately
- the app should record that the item was imported after a validation fix for traceability

Example:

- if a folder was intended to represent `20260415` but was originally named `15-04-2026`, correcting and revalidating it later should still create the revision for `2026-04-15`

## In-App Fix Model

The fix workflow is `Suggest -> Review -> Apply -> Revalidate`.

Safe auto-apply cases:

- renaming a folder from a recognized non-canonical date format to `YYYYMMDD`
- renaming a file to a canonical filename when confidence is high and no collision would be introduced

Assisted but not automatic cases:

- duplicate drawing number conflicts
- low-confidence parsing corrections
- cases where the app would need to invent a new drawing number or decide which of two genuinely different drawings is correct

For duplicate conflicts, the UI should show the competing items side by side and help the user resolve them, but the app should not guess.

## Data Model

The implementation should split raw scan evidence from validated register records.

### Raw scan item

Represents a folder or file discovered on disk before import.

Suggested fields:

- raw folder path
- raw file path
- detected folder date text
- parsed issue-date candidate
- parsed drawing number
- parsed revision
- parsed description
- scan timestamp

### Validation result

Represents one rule outcome attached to a raw item.

Suggested fields:

- severity
- rule code
- message
- suggested fix
- can auto-apply
- requires quarantine

### Quarantine record

Represents a blocked item that must not appear in the live register yet.

Suggested fields:

- original folder and file path
- intended issue-date candidate
- suggested canonical folder name
- suggested canonical file name
- current resolution state

### Live register document

Represents validated document identity and revision history.

Rules:

- current document description is derived from the latest valid revision
- each revision stores its own historical description and file path

### Fix audit log

Represents user-applied or app-applied corrections.

Suggested fields:

- action type
- original value
- new value
- applied timestamp
- item reference
- note that the change came from in-app validation fixing

## Storage Design

`project_data.json` is no longer the best primary live store for this scope.

The recommended storage model is:

- keep `project_info.json` for lightweight project metadata
- move operational data to `project_data.db`

`project_data.db` should live in the project root alongside `project_info.json`.

Example:

```text
<Project Folder>\
  project_info.json
  project_data.db
```

SQLite is preferred because it supports:

- fast partial writes
- indexed queries over drawing number, issue date, folder path, and conflict state
- separate tables for live documents, revisions, quarantine items, validation issues, and audit entries
- better performance than rewriting one large JSON document as the project grows

No automatic backup feature is part of this design.

## Migration from JSON

Migration should be automatic on first load.

Rules:

- if `project_data.db` exists, use it
- if `project_data.db` does not exist but `project_data.json` does, migrate JSON data to SQLite automatically
- `project_info.json` remains in place
- after a successful migration, `project_data.json` is treated as legacy input, not the active live store
- the migration should not silently delete the old JSON file

## Performance Guardrails

The feature only works well if scan and validation remain responsive.

Required guardrails:

- scan incrementally rather than rebuilding all project data unnecessarily
- only rescan folders whose timestamps changed, or folders the user explicitly rescans
- perform scan and validation work off the UI thread
- avoid opening PDFs unless metadata cannot be derived from cached results or a file changed
- batch UI refreshes rather than rebinding the full grid repeatedly during scan
- index hot fields in SQLite such as `DocumentNumber`, `IssueDate`, `FolderPath`, and validation state

The design goal is to keep the app feeling fast even after adding quarantine and validation history.

## Technical Scope

Likely change areas:

- `DrawingRegister.App/Models/`
- `DrawingRegister.App/Services/`
- `DrawingRegister.App/Helpers/`
- `DrawingRegister.App/MainWindow.xaml(.cs)`
- new validation/quarantine dialogs or windows

Expected technical additions:

- SQLite-backed project repository layer
- migration path from JSON storage
- scan validation service
- quarantine and fix-application workflow
- UI for validation results and conflict handling

No new solution-wide architecture change beyond the storage and validation workflow is required.

## Error Handling

- validation must tolerate malformed folders and filenames without crashing the scan
- in-app rename actions must verify destination collisions before applying
- failed fix actions should leave the item quarantined and report the reason clearly
- migration failures should leave the project unopened or in read-only failure state rather than partially corrupting the live store
- ambiguous duplicate conflicts must never be auto-imported on uncertain inference

## Testing

Automated coverage should focus on the rule engine and migration logic.

Coverage should include:

- folder-date validation and canonical date suggestions
- filename normalization suggestions
- quarantine decisions for critical issues
- warning-only behavior for non-critical formatting drift
- same drawing number across different issue dates with evolving descriptions
- same drawing number on the same issue date with materially different descriptions
- historical correction importing on intended issue date rather than fix date
- JSON-to-SQLite migration for an existing project
- latest valid description precedence in the live register

Manual verification should confirm:

- scan remains responsive on realistic project folders
- quarantine items do not appear in the main register
- in-app folder/file fixes work and revalidate correctly
- corrected historical items land on the intended issue date
- latest description shown in the register updates correctly after later revisions

## Open Assumptions Resolved

- the app should quarantine critical issues rather than block the entire scan
- the app should support in-app fixes for safe structural problems
- historical items should import against the intended issue date after correction
- latest valid description should take precedence in the register
- same-date duplicate ambiguities should not be auto-resolved
- SQLite in the project root is preferred over a growing JSON-only live store
- automatic JSON-to-SQLite migration should happen on first load
- backup and git integration are intentionally out of scope for this feature

## Implementation Readiness

This is a coherent feature set, but it is larger than a single narrow bugfix.

It is still a good candidate for one implementation plan because the work can be staged:

- storage and migration
- validation engine
- quarantine persistence
- fix-application workflow
- UI integration

The boundaries are clear enough to plan and implement incrementally without changing the product direction midway.
