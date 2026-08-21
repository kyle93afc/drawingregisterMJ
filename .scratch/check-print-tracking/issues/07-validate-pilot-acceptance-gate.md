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
