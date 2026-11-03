# 06: Allocate the next check-print number

**What to build:** From the checking window, a technician can reserve the next CP number for a drawing revision without racing another technician or changing any PDF, filename, folder, or register record.

**Blocked by:** 02: Scan check prints into a separate checking inventory.

**Status:** resolved

- [x] Allocation is scoped by document code and revision and returns one greater than the highest reserved or scanned CP in that scope.
- [x] ~~A new revision starts its own CP sequence.~~ Superseded 2026-09-02: the CP sequence runs per drawing across revisions.
- [x] The reservation is persisted in the separate checking dataset before success is shown.
- [x] Concurrent requests against the shared project store cannot receive the same CP number.
- [x] A failed or unavailable store produces no apparent reservation and gives the user a clear retryable error.
- [x] Allocation does not rename or move files and does not add or modify register documents.
- [x] Tests cover initial allocation, repeated allocation, revision reset, persisted reload, and competing requests.

## Answer

Implemented in commit `68ed2ba`. The full test suite passes with 94 tests.

### 2026-09-02 scope change

Job 124997 shows the real convention: `P01-CP01`, `T01-CP02`, `T01-CP03` on one drawing. The revision can change between check prints when the engineer changes the issue purpose. Allocation is now scoped by document code only, so the next CP is one greater than the highest scanned or reserved CP for that drawing in any revision. The checking window also now reserves for the selected row and shows the suggested filename.
