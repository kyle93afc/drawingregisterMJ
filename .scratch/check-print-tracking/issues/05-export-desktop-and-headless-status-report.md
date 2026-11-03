# 05: Export desktop and headless status reports

**What to build:** Users can export the checking result to CSV from the desktop, and an unattended console command can run the same read-only scan and report without starting WPF, allowing Task Scheduler to run it nightly.

**Blocked by:** 04: Join checks to register issue state.

**Status:** resolved

- [x] The checking window exports its rendered result to CSV with identity, CP, verdict, back-draft, attribution, issue state, file path, and any scan warning.
- [x] A console runner accepts the project and checking locations and produces the same result through the shared scan, apply, join, and render services.
- [x] Desktop and console output agree for the same inputs.
- [x] The console runner operates unattended without creating a WPF application or mutating PDFs, check-print files, or register data.
- [x] Malformed and non-conforming files are reported in both outputs without aborting the run.
- [x] The scheduled-run invocation and success/failure exit behavior are documented.

## Answer

Implemented in commit `1952cac`. The checking window and headless runner now share the same read-only render and CSV services, and the full test suite passes with 89 tests.
