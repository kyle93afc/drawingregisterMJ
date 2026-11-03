# 07: Validate the pilot acceptance gate

**What to build:** A human reviewer validates the complete read-only checking workflow against the pilot job’s real check prints and records whether it is safe to use before any v2 file mutation work is considered.

**Blocked by:** 05: Export desktop and headless status reports; 06: Allocate the next check-print number.

**Status:** ready-for-human

- [ ] Every pilot PDF has a human-confirmed expected verdict and the scan result is compared against that ground truth.
- [ ] The pilot records zero false `APPD` results.
- [ ] The pilot records zero false `FC` results, including flattened-markup checks.
- [ ] Generic stamps, all legacy stamp variants, conflicting verdicts, author aliases, and independent back-draft stamps are represented in the validation set.
- [ ] Desktop and headless reports agree and check-print data has not appeared in the register, PDF report, or DocReg export.
- [ ] Any mismatch keeps this ticket open and is captured as a follow-up defect; v2 remains blocked until the gate passes.

## Comments

### 2026-08-21 pilot attempt

The headless workflow completed against job 17749's engineering register and all 326 PDFs currently in its checking folder. The all-fact scan reported:

- 146 `FC`, 73 `AWC`, 49 `APPD`, 58 `UNKNOWN`, and 0 `CONFLICT`.
- Only 1 PDF produced a document code, revision, and CP number; the other 325 received a filename-convention warning.
- The headless CSV contained 325 live rows; the omitted row was the one parsed `APPD` check print whose matching revision is distributed.

The gate has **not passed**. Human-confirmed verdicts and a desktop CSV were not available, so false `APPD`/`FC` rates and desktop parity could not be established. The filename failures prevent useful register joins, and the absence of `CONFLICT` results disagrees with the approved pilot baseline of nine conflicting PDFs. Follow-up defects 08 and 09 capture these mismatches; v2 remains blocked.
