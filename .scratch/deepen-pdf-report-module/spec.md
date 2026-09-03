# Deepen the PDF Report Module

**Status:** resolved

## Problem Statement

A maintainer changing Register, transmittal, or Document Register PDF output must currently understand a large section of the main WPF window. Report selection, Register Document projection, header and table composition, distribution text, QuestPDF generation, output-path fallback, and user-interface controls are intertwined.

The existing PDF report module is shallow: it prepares only a small row projection while most report behavior remains in the window. This weakens locality, makes the WPF window the effective test surface, and allows the three report modes to drift. Users may not see a defect today, but routine report changes carry unnecessary regression risk across outputs relied on for issues and distributions.

## Solution

Deepen the existing PDF report module so one small interface owns the complete in-process report-generation behavior for Register, transmittal, and Document Register modes.

The WPF window will collect the user's current choices, create an immutable report request using Register domain values, ask the report module to generate the PDF, and present the result. The report module will own document selection and projection, report identity, layout, QuestPDF composition, output-path fallback, and file generation. It will not know about WPF controls, dialogs, messages, or opening the generated file.

This is an architectural refactor: existing report appearance, content, selection rules, naming, and error behavior remain unchanged unless this specification explicitly says otherwise.

## User Stories

1. As a Register user, I want the PDF Report command to remain in the same place, so that the refactor does not change my workflow.
2. As a Register user, I want an All Dates export to remain a full Register report, so that I retain the current project overview.
3. As a Register user, I want a selected issue date to continue producing a transmittal, so that dated Issue records remain reportable.
4. As a Register user, I want Document Register mode to continue producing the SER Document Register format, so that warrant-issue output remains compliant with the current workflow.
5. As a Register user, I want each report mode to retain its current title and Register number, so that generated PDFs remain recognisable.
6. As a Register user, I want generated filenames to retain their current mode-specific naming, so that existing filing conventions continue to work.
7. As a Register user, I want the selected report date to continue driving report identity and filename dates, so that the output reflects my chosen date.
8. As a Register user, I want a transmittal number to continue appearing only on transmittals, so that Register and Document Register output are not mislabeled.
9. As a Register user, I want transmittals to contain only Register Documents from the selected Issue date, so that the PDF represents that Issue.
10. As a Register user, I want a selected issue subfolder to continue narrowing transmittal contents, so that separate Issues on the same date remain distinguishable.
11. As a Register user, I want transmittals to retain distribution, purpose, method, and issuer information, so that the Issue is understandable without reopening the application.
12. As a Register user, I want full Register rows to remain ordered by Document Code, so that documents are easy to locate.
13. As a Register user, I want the latest non-superseded Revision and its Issue date to continue driving current Register columns, so that Superseded Revisions do not appear current.
14. As a Register user, I want Register Documents with missing or placeholder Revision codes to render safely, so that imperfect source data does not abort the report.
15. As a Register user, I want Document Register mode to continue using the currently displayed Register Documents, so that active filters remain reflected in that export.
16. As a Register user, I want all report modes to retain the current project identity, discipline, Register number, and client number in the header, so that PDFs remain attributable.
17. As a Register user, I want reports to retain the current company logo behavior, so that generated output remains branded when an embedded logo is available.
18. As a Register user, I want reports to remain A4 landscape with the current table columns and page numbering, so that the layout does not unexpectedly change.
19. As a Register user, I want a locked requested PDF to continue producing a suffixed writable filename, so that report generation succeeds without overwriting or requiring me to close the existing PDF.
20. As a Register user, I want the success message to identify the actual generated path, so that I can find a suffixed output.
21. As a Register user, I want the generated PDF to continue opening after a successful export, so that I can inspect it immediately.
22. As a Register user, I want generation failures to remain visible as user-facing errors, so that failures are not silent.
23. As a maintainer, I want report behavior behind one interface, so that I can change report output without navigating the WPF window implementation.
24. As a maintainer, I want report input expressed as domain values rather than WPF controls, so that report behavior can run without constructing a window.
25. As a maintainer, I want Register, transmittal, and Document Register modes exercised through the same test seam, so that one mode cannot bypass the tested generation path.
26. As a maintainer, I want report selection, layout, and file generation to have locality within one module, so that related changes and defects concentrate in one place.
27. As a maintainer, I want obsolete shallow report helpers and window composition methods removed after replacement, so that there is only one implementation to understand.
28. As a maintainer, I want existing dependencies reused, so that the installer gains no additional package weight.
29. As a coding agent, I want the report module's interface to expose the complete behavior under test, so that I do not need to inspect private composition details to verify a change.
30. As a coding agent, I want tests to assert generated report outcomes rather than helper method structure, so that internal refactors do not require test rewrites.

## Implementation Decisions

- The existing PDF report module becomes the single deep module for all PDF report modes. Report identity, Register Document selection and projection, header composition, mode-specific content, page composition, writable-path resolution, and QuestPDF file generation move behind its interface.
- The module has one in-process implementation. Do not add an interface type, factory, dependency-injection registration, or adapter; there is no second implementation and therefore no real seam requiring them.
- The highest test seam is the report-generation interface. It accepts an immutable report request and a requested output path, generates the PDF, and returns the actual output path used.
- The report request contains domain values only: report mode and date; project identity fields; Register Documents; optional selected Issue date and issue-folder path; and the selected distribution, purpose, method, and issuer values needed by a transmittal.
- The report request must not contain WPF controls, windows, dialogs, bindings, visual elements, or callbacks into the user interface.
- The WPF window remains responsible for translating current controls into the report request, showing the save dialog, displaying success or failure messages, and opening the generated PDF.
- The report module throws generation and file errors to its caller. It does not display messages or launch files.
- Existing Register, transmittal, and Document Register selection behavior remains unchanged. In particular, transmittals use the selected Issue date and optional issue subfolder; Document Register mode uses the Register Documents currently displayed by the window; full Register mode uses all Register Documents.
- Full Register current-state columns continue to use the newest non-superseded Revision by Issue-date key. Revision text is not lexically sorted to determine current state.
- Existing mode-specific titles, Register numbers, transmittal numbers, filename prefixes, table columns, branding, page size, margins, and page numbering remain unchanged.
- Existing writable-path behavior remains inside the deep module: an available path is used directly, while a locked target produces the first available numbered suffix without overwriting another file.
- Existing embedded-logo fallback behavior remains an implementation detail of the report module. A missing logo must not prevent report generation.
- QuestPDF remains the only PDF-writing dependency and PdfPig remains available for reading generated PDFs in tests. No package is added.
- Existing small report helpers may remain private implementation details if they improve locality inside the deep module. Helpers that only pass values through, duplicate behavior, or remain exposed solely for old tests are deleted.
- Window-owned report composition methods and unused report cell helpers are removed after the deep module handles all report modes.
- No Register, Register Document, Revision, Issue, Distribution, or Document Register persistence schema changes are made.
- Check Prints remain excluded from every PDF report and Document Register output.

## Testing Decisions

- A good test crosses the report-generation interface, supplies domain input, reads the generated PDF or observes the resulting path, and asserts externally visible report behavior. It does not call private composition methods, inspect QuestPDF structures, or use reflection to enforce implementation shape.
- The principal suite generates PDFs in a temporary directory through the same interface used by the WPF window. PdfPig reads generated text where textual output is the behavior under test.
- Cover all three report modes through this one seam: full Register, transmittal, and Document Register.
- Verify mode-specific title, Register number, filename, and transmittal-number behavior.
- Verify full Register ordering and latest non-superseded Revision/Issue-date selection, including a later Superseded Revision, a reissued Revision, and a placeholder Revision.
- Verify transmittal filtering by Issue date and optional issue subfolder, plus distribution, purpose, method, and issuer text.
- Verify Document Register mode uses the supplied filtered Register Document set and retains its SER identity.
- Verify empty Register Document input and missing optional metadata generate a valid PDF rather than failing.
- Verify the returned path equals the requested path when writable and uses the current numbered suffix behavior when the target is locked.
- Verify a missing embedded logo does not make generation fail; do not assert internal logo-loading calls.
- Keep one small WPF manual check because this repository has no WPF integration harness: generate each report mode, confirm the save dialog and messages, and confirm the generated PDF opens.
- Existing report-row, report-identity, and writable-path tests are prior art. Replace lower-seam tests when equivalent behavior is covered through report generation; do not retain duplicate tests solely to preserve the old shallow interfaces.
- Existing PDF fixture and PdfPig patterns in the Register import and Check Print suites are prior art for temporary files and PDF inspection.
- The full automated test suite must pass. No visual snapshot framework or new test dependency is introduced.

## Out of Scope

- Changing report content, columns, typography, colors, spacing, or branding.
- Reintroducing Revision trail or report status columns removed from the current full Register output.
- Changing how Register Documents, Revisions, Issues, or Distributions are imported, edited, stored, or filtered.
- Changing Document Register filename grammar or import behavior.
- Adding Check Print data to Register, transmittal, or Document Register output.
- Adding new report modes, templates, user-configurable layouts, or a preview designer.
- Moving all WPF code to MVVM or refactoring unrelated main-window behavior.
- Adding background generation, cancellation, progress reporting, caching, or batch export.
- Adding a second PDF library, dependency-injection framework, interface hierarchy, or speculative adapter.
- Changing release packaging or installer behavior.

## Further Notes

- This spec deepens an existing module rather than adding another report path. The deletion test should pass after implementation: deleting the report module would force report selection, composition, and generation complexity back into callers.
- The selected seam matches the architecture-review recommendation accepted in this conversation: the window chooses inputs and presents the result; the deep PDF report module owns the implementation.
- There are no ADRs in this area to revisit.
- The earlier full Register PDF design remains the source for current user-facing behavior where it agrees with the implemented report. This refactor does not revive superseded design elements that current tests explicitly exclude.
