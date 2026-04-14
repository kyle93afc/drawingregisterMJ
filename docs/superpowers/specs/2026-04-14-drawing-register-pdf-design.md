# Drawing Register PDF Replacement Design

Date: 2026-04-14
Topic: Replace the existing full drawing register PDF export so it produces a clearer issued-drawings register with full revision history.

## Summary

The current `PDF Report` export already supports two modes:

- A date-filtered transmittal PDF
- An `All Dates` full-register PDF

The problem is limited to the full-register PDF. Its current layout shows only the latest revision, latest date, and up to three date-based history columns. That does not answer the client email asking where previous revisions and issue dates are, and it does not surface obvious revision-history anomalies clearly.

This design replaces only the `All Dates` PDF register layout. The date-filtered transmittal layout remains unchanged.

## Goals

- Produce a full drawing register PDF for all drawings found in the issue-folder history
- Show complete revision history for each drawing in a readable format
- Make the latest revision and latest issue date easy to scan
- Flag obvious revision-history anomalies without inventing workflow states
- Reuse the existing export path instead of creating a second register export feature

## Non-Goals

- No change to the date-filtered transmittal PDF output
- No attempt to infer or list drawings "yet to be issued"
- No change to how drawings are scanned or imported from folders
- No attempt to correct bad register data automatically

## Source of Truth

For this project, if a drawing exists in an issue folder, it has been issued.

Implications:

- The report includes only drawings discovered from the existing issue-folder scan history
- The report does not try to produce a separate list of unissued drawings
- Status values are exception flags only, not issuance states

## User-Facing Behavior

The existing `PDF Report` button remains the entry point.

Behavior by mode:

- If a specific issue date is selected, generate the existing transmittal PDF with no layout or logic change
- If `All Dates` is selected, generate the replacement full drawing register PDF described in this document

## Full Register Layout

Columns for the replacement `All Dates` register:

`Document No | Description | Package | Type | Size | Latest Rev | Latest Issue Date | Revision Trail | Status`

### Column definitions

- `Document No`: Existing document number
- `Description`: Existing drawing description
- `Package`: Existing package value
- `Type`: Existing document type
- `Size`: Existing drawing size
- `Latest Rev`: Revision code from the newest revision-history entry for the drawing
- `Latest Issue Date`: Date from the newest revision-history entry for the drawing
- `Revision Trail`: Full revision history rendered newest to oldest as a single text field
- `Status`: Exception-only flag derived from the revision history

### Revision Trail formatting

Each revision-history entry is rendered as:

`REV <revision> - dd/MM/yyyy`

Entries are ordered newest to oldest and joined with ` | `.

Examples:

- `REV 3 - 14/04/2026 | REV 2 - 01/04/2026 | REV 1 - 12/03/2026`
- `REV C02 - 14/04/2026 | REV C01 - 03/04/2026`
- `REV - - 14/04/2026`

If the implementation wants to suppress the `REV` prefix for blank or placeholder revisions, that is acceptable as long as the output remains consistent and readable.

## Status Rules

The normal state is `Issued`.

The report only promotes a drawing to an exception status when there is an obvious issue in the stored revision history.

Supported status values:

- `Issued`
- `Check Revision Sequence`
- `Check Same Revision Reissued`
- `Check Missing Revision`

### Rule definitions

#### `Issued`

Use when none of the exception rules below apply.

#### `Check Revision Sequence`

Use when the latest revision appears lower than an earlier revision for the same drawing.

Example:

- Latest revision is `1`, but an older history entry contains `3`
- Latest revision is `A`, but an older history entry contains `C`

This should be based on a defensible ordering strategy:

- Numeric revisions compare numerically
- Single-letter revisions compare alphabetically
- Letter-prefixed numeric revisions such as `C01`, `C02` compare within the same prefix by numeric suffix

If revisions are mixed in a way that cannot be compared safely, do not force this status based on uncertain inference alone.

#### `Check Same Revision Reissued`

Use when the same revision code appears on more than one issue date for the same drawing.

This is not necessarily wrong, but it is useful to surface because it can explain why a drawing has a recent issue date without a revision increase.

#### `Check Missing Revision`

Use when a revision entry exists in history but the revision code is blank or `-`.

Since the folder indicates the drawing was issued, this is a register-data quality warning rather than an issuance warning.

### Status precedence

If multiple exception conditions apply, the row should show the highest-signal one using this precedence:

1. `Check Revision Sequence`
2. `Check Missing Revision`
3. `Check Same Revision Reissued`
4. `Issued`

This keeps each row to a single status value and prioritizes the most serious anomaly.

## Data Preparation

The existing PDF code currently derives most display values directly inside `ComposeContent(...)`.

To keep the replacement register maintainable, the implementation should introduce a small report-row projection for full-register output, for example:

- document number
- description
- package
- type
- size
- latest revision
- latest issue date
- formatted revision trail
- computed status

This projection should be built from `DocumentMetadata.RevisionHistory`, sorted by `DocumentNumber`, and then passed into the PDF composition code.

The existing transmittal flow should continue to operate on the current logic unless a small shared helper naturally reduces duplication without changing behavior.

## Technical Scope

Expected change areas:

- `DrawingRegister.App/MainWindow.xaml.cs`
- New helper or private methods for:
  - preparing full-register report rows
  - formatting the revision trail
  - computing status from revision history

No new package dependencies are required.

## Error Handling

- If a drawing has no revision history, the row should still render safely with blank latest values and a sensible fallback status
- Revision formatting must tolerate blank, placeholder, numeric, alphabetical, and letter-prefixed numeric revisions
- The replacement logic must not affect the current date-filtered transmittal path

## Testing

Add focused unit tests around the new report-row/status/trail-building logic.

Coverage should include:

- full revision trail formatting in newest-to-oldest order
- latest revision and latest date selection
- `Issued` status for normal histories
- `Check Revision Sequence` for decreasing latest revision
- `Check Same Revision Reissued` for duplicate revision codes on different dates
- `Check Missing Revision` for blank or `-` revisions
- transmittal export behavior remaining unchanged by the new register-only logic

Because the WPF UI does not have an integration harness, final verification also includes manual generation of:

- an `All Dates` PDF register using the new layout
- a date-filtered transmittal PDF to confirm no regression

## Open Assumptions Resolved

- The replacement applies only to the full drawing register output
- The date-filtered transmittal remains unchanged
- Folder presence means the drawing has been issued
- The report does not attempt to derive a separate "yet to be issued" list

## Implementation Readiness

This scope is narrow enough for a single implementation plan and does not require repo-wide refactoring.
