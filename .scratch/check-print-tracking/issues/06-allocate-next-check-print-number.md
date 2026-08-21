# 06: Allocate the next check-print number

**What to build:** From the checking window, a technician can reserve the next CP number for a drawing revision without racing another technician or changing any PDF, filename, folder, or register record.

**Blocked by:** 02: Scan check prints into a separate checking inventory.

**Status:** resolved

- [x] Allocation is scoped by document code and revision and returns one greater than the highest reserved or scanned CP in that scope.
- [x] A new revision starts its own CP sequence.
- [x] The reservation is persisted in the separate checking dataset before success is shown.
- [x] Concurrent requests against the shared project store cannot receive the same CP number.
- [x] A failed or unavailable store produces no apparent reservation and gives the user a clear retryable error.
- [x] Allocation does not rename or move files and does not add or modify register documents.
- [x] Tests cover initial allocation, repeated allocation, revision reset, persisted reload, and competing requests.

## Answer

Implemented in commit `68ed2ba`. The full test suite passes with 94 tests.
