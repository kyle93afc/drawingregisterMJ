# 06: Allocate the next check-print number

**What to build:** From the checking window, a technician can reserve the next CP number for a drawing revision without racing another technician or changing any PDF, filename, folder, or register record.

**Blocked by:** 02: Scan check prints into a separate checking inventory.

**Status:** ready-for-agent

- [ ] Allocation is scoped by document code and revision and returns one greater than the highest reserved or scanned CP in that scope.
- [ ] A new revision starts its own CP sequence.
- [ ] The reservation is persisted in the separate checking dataset before success is shown.
- [ ] Concurrent requests against the shared project store cannot receive the same CP number.
- [ ] A failed or unavailable store produces no apparent reservation and gives the user a clear retryable error.
- [ ] Allocation does not rename or move files and does not add or modify register documents.
- [ ] Tests cover initial allocation, repeated allocation, revision reset, persisted reload, and competing requests.
