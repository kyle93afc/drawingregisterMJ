# 08: Parse pilot check-print filenames

**What to build:** Check-print scanning recognises the filename variants used by the pilot job, including CP tokens after the drawing description, without changing the register's drawing filename grammar.

**Blocked by:** 01: Extract the shared drawing identity parser.

**Status:** resolved

- [x] A CP token can be read after the description while preserving support for `CP01`, `CP-01`, `CP_01`, and `CP 01` delimiters.
- [x] Document code and revision still come from the shared drawing identity parser.
- [x] PDFs without a revision or CP token remain visible with a precise warning rather than being assigned invented values.
- [x] Re-scanning the pilot no longer gives a filename warning to valid CP-bearing filenames.
- [x] Tests cover representative pilot filename placements and ensure register imports are unchanged.
- [x] Scanning remains read-only.

## Comments

The 2026-08-21 pilot scan parsed identity, revision, and CP for only 1 of 326 PDFs and issued 325 filename warnings. The scanner currently requires the CP token at the start of the parsed description, while observed pilot filenames commonly place it at the end.

### 2026-09-02 job 124997 scan

The desktop scan of job 124997's checking folder parsed 0 of 6 PDFs. All six place the CP token after the description, e.g. `124997-M+J-S1-XX-DR-S-00-01-T01-GENERAL NOTES-CP01.pdf`.

## Answer

The check-print token regex now accepts `CP` at the start of the description or, delimited by `-`, `_`, or a space, at its end. Document code and revision still come from the shared drawing filename parser, which is unchanged. Tests cover the job 124997 filename shapes, the four delimiter forms, the legacy leading-token form, and three malformed names that must warn without invented values. The full test suite passes with 102 tests.
