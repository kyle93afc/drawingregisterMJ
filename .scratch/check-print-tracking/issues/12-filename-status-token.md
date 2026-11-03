# 12: Write the check status into the filename

**What to build:** After a scan, each check print is renamed so its status is visible in Explorer and Bluebeam without opening the app, using the token format agreed in ticket 10. This is the first real file mutation and the smallest useful one.

**Blocked by:** 11: Make CheckPrintApplier safe to write files.

**Status:** blocked

- [ ] The scanner's filename grammar accepts and strips an existing status token so the same file parses identically before and after renaming.
- [ ] A rename is planned only when the token on disk differs from the latest verdict, so unchanged files are never touched.
- [ ] A removed or changed stamp produces a downgrade rename on the next scan.
- [ ] Renames go through the ticket 11 safety rules (hash check, lock, collision quarantine, locked-file skip).
- [ ] The checking window shows a preview of planned renames and requires one explicit click to apply; the folder watcher never applies on its own.
- [ ] The headless runner gains an `--apply` flag; without it the run stays read-only exactly as today.
- [ ] The register import ignores status tokens, so a check print copied into the register folder by mistake still cannot become a register document with a token in its description.
- [ ] Tests cover first rename, no-op rerun, downgrade, and a filename that already carries a stale token.
