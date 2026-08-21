# 05: Export desktop and headless status reports

**What to build:** Users can export the checking result to CSV from the desktop, and an unattended console command can run the same read-only scan and report without starting WPF, allowing Task Scheduler to run it nightly.

**Blocked by:** 04: Join checks to register issue state.

**Status:** ready-for-agent

- [ ] The checking window exports its rendered result to CSV with identity, CP, verdict, back-draft, attribution, issue state, file path, and any scan warning.
- [ ] A console runner accepts the project and checking locations and produces the same result through the shared scan, apply, join, and render services.
- [ ] Desktop and console output agree for the same inputs.
- [ ] The console runner operates unattended without creating a WPF application or mutating PDFs, check-print files, or register data.
- [ ] Malformed and non-conforming files are reported in both outputs without aborting the run.
- [ ] The scheduled-run invocation and success/failure exit behavior are documented.
