# 01: Extract the shared drawing identity parser

**What to build:** Every drawing filename currently accepted by the register must continue to import identically, while check-print scanning can obtain the same document code and revision from one shared parser instead of maintaining another filename grammar.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The shared parser returns the document code and revision for all supported revision forms, including sub-letter, numeric, prefixed, and bare-letter revisions.
- [x] Existing drawing imports use the shared parser without changing accepted filenames, document numbers, revisions, descriptions, or skip reasons.
- [x] Malformed and unrelated filenames remain rejected as before.
- [x] Tests exercise the production parser rather than mirroring its regular expression.

## Answer

Implemented in commit `2c6c6f1`. The full test suite passes with 64 tests.
