# 11: Make CheckPrintApplier safe to write files

**What to build:** `CheckPrintApplier.Apply` gains the safety rules the design spec requires before any rename or move, without yet performing a real rename. A dry-run mode reports what would change so the rules can be proven on real folders first.

**Blocked by:** 07: Validate the pilot acceptance gate; 09: Investigate missing pilot conflicts; 10: Settle the checking folder convention.

**Status:** blocked

- [ ] Each planned action re-reads the file and compares its hash to the `SourceHash` captured at plan time immediately before acting; a mismatch skips the action and reports it.
- [ ] A single-run lock in the checking folder prevents two desktop or headless runs from applying at once; a stale lock is reported, not silently broken.
- [ ] A file open in another program (Bluebeam, Acrobat) is reported as a normal "locked, will retry next scan" outcome, not an error.
- [ ] A target filename that already exists is never overwritten; the action is skipped and the pair is reported as a collision for a human.
- [ ] Re-running Apply against an already-applied folder produces zero actions.
- [ ] Dry-run mode lists every planned action with source and target paths and performs none of them; the checking window and headless runner can both request it.
- [ ] `ApplyResult` carries per-file outcomes (applied, skipped-locked, skipped-hash-changed, collision) and the CSV export includes them.
- [ ] Tests cover each outcome using PdfPig-built fixtures and a held-open file handle.
