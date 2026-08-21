# 04: Join checks to register issue state

**What to build:** The checking window combines each check print with the matching register revision so a user can tell whether it is current, superseded, distributed, and therefore clear of the live checking queue.

**Blocked by:** 03: Derive check verdict and attribution.

**Status:** ready-for-agent

- [ ] Check prints join to register documents by document code and revision only at display time.
- [ ] Current-revision decisions use the latest non-superseded revision by issue-date key, never lexical revision sorting.
- [ ] The view shows distribution, issue date, issued by, purpose, and method when a matching revision exists.
- [ ] An `APPD` check print leaves the live view only when its matching revision is distributed.
- [ ] `AWC`, `UNKNOWN`, `CONFLICT`, unmatched, superseded, and approved-but-not-distributed rows remain visible with an explicit reason.
- [ ] Scanning and viewing check state never writes to register document or revision data.
- [ ] Tests cover current, superseded, unmatched, distributed, and approved-but-unissued revisions.
