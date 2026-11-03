# 09: Investigate missing pilot conflicts

**What to build:** Reconcile the pilot's approved baseline of nine conflicting check prints with the scanner's zero `CONFLICT` results, then correct verdict derivation or the recorded ground truth as evidence requires.

**Blocked by:** 03: Derive check verdict and attribution.

**Status:** ready-for-human

- [ ] A human identifies the expected conflicting PDFs in the current pilot folder and records their applied verdict stamps.
- [ ] Scanner output for each expected conflict is compared with the PDF annotation structure.
- [ ] Any unrecognised real verdict stamp subject is covered by an approved real fixture and the minimum scanner mapping.
- [ ] Genuine incompatible verdict stamps produce `CONFLICT`; files that are no longer conflicts have their ground truth corrected with a reason.
- [ ] The complete pilot is re-scanned and the conflict count agrees with human-confirmed ground truth.

## Comments

The 2026-08-21 all-fact scan of 326 pilot PDFs returned 146 `FC`, 73 `AWC`, 49 `APPD`, 58 `UNKNOWN`, and no `CONFLICT`. The approved design baseline records nine conflicting PDFs in the original 321-file pilot set, so ticket 07 cannot pass until this discrepancy is resolved.
