# 02: Scan check prints into a separate checking inventory

**What to build:** A user with a loaded project can open a checking window, select a checking folder, and run a read-only scan that lists every PDF separately from the document register. The first tracer supports unannotated check prints as `FC` and keeps files that cannot be parsed visible as flagged rows.

**Blocked by:** 01: Extract the shared drawing identity parser.

**Status:** resolved

- [x] A checking window can scan a selected folder without adding rows or fields to the document register.
- [x] Conforming filenames produce check-print records containing document code, revision, CP number, file path, and source hash.
- [x] A PDF with no stamp annotation is shown as `FC`, with wording that means “no annotation found,” not “not checked.”
- [x] Non-conforming filenames and malformed PDFs appear as flagged rows instead of being silently skipped or aborting the scan.
- [x] Planning is filesystem-read-only, v1 apply is a no-op pass-through, and the view renders only the resulting apply facts.
- [x] Scan results persist in a separate check-print section of project storage and reload without changing existing project data.
- [x] Re-scanning unchanged files updates the inventory without creating duplicates.

## Answer

Implemented in commit `335a5df`. The full test suite passes with 70 tests.
