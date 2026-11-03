# 13: Move superseded check prints into _PREV

**What to build:** When a drawing has a newer CP, older CPs are moved into a `_PREV` subfolder so the checking folder shows only the current print per drawing, per the structure agreed in ticket 10.

**Blocked by:** 12: Write the check status into the filename.

**Status:** blocked

- [ ] Only the highest CP per document code stays in the working folder; every lower CP is planned for a move into `_PREV` beside it.
- [ ] Moves use the ticket 11 safety rules; a locked older print stays put and is reported.
- [ ] The scanner keeps reading `_PREV` so history stays in the grid, marked as superseded rather than live.
- [ ] Reserving a CP does not move anything; only a scanned file with a higher CP triggers a move.
- [ ] Moving is previewed and applied with the same explicit click as renames.
- [ ] Tests cover a three-CP drawing, a locked older print, and a rerun with nothing to move.
