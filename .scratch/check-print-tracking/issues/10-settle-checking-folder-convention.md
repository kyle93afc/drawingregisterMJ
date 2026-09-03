# 10: Settle the checking folder convention

**What to build:** A human decision, recorded here, on how the checking folder is structured before any tool renames or moves a file. The v1 scanner does not care; v2 renaming and `_PREV` foldering cannot start until this is fixed.

**Blocked by:** nothing.

**Status:** ready-for-human

- [ ] Confirm the CP sequence runs per drawing across revisions (as job 124997 shows: `P01-CP01`, `T01-CP02`, `T01-CP03`) and is not reset per revision or per volume.
- [ ] Decide whether date-named subfolders (for example `20260805`) remain the way prints are grouped, or whether v2 replaces them with a flat folder plus `_PREV`.
- [ ] Reconcile this with the zoned folder hierarchy agreed at the Makro jobs kickoff, and record which one wins for checking folders.
- [ ] Decide the status token format for filenames (proposed: `...-CP03-AWC.pdf`, tokens `FC`, `AWC`, `APPD`, `UNKNOWN`, `CONFLICT`, with `-BD` appended when back-drafted).
- [ ] Decide what happens to the filename when a stamp is later removed (proposed: the token always reflects the latest scan, so it downgrades).
- [ ] Record the decisions in this ticket and in the design spec's "Deferred to v2" section.

## Comments

Raised 2026-09-02 after the v1 window went live on job 124997. The design spec of 2026-08-21 flags this as the external conversation gating v2.
