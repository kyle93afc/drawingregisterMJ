# 08: Parse pilot check-print filenames

**What to build:** Check-print scanning recognises the filename variants used by the pilot job, including CP tokens after the drawing description, without changing the register's drawing filename grammar.

**Blocked by:** 01: Extract the shared drawing identity parser.

**Status:** ready-for-agent

- [ ] A CP token can be read after the description while preserving support for `CP01`, `CP-01`, `CP_01`, and `CP 01` delimiters.
- [ ] Document code and revision still come from the shared drawing identity parser.
- [ ] PDFs without a revision or CP token remain visible with a precise warning rather than being assigned invented values.
- [ ] Re-scanning the pilot no longer gives a filename warning to valid CP-bearing filenames.
- [ ] Tests cover representative pilot filename placements and ensure register imports are unchanged.
- [ ] Scanning remains read-only.

## Comments

The 2026-08-21 pilot scan parsed identity, revision, and CP for only 1 of 326 PDFs and issued 325 filename warnings. The scanner currently requires the CP token at the start of the parsed description, while observed pilot filenames commonly place it at the end.
