# 03: Derive check verdict and attribution

**What to build:** Engineers can see the applied Bluebeam verdict, independent back-draft state, and self-asserted markup attribution for each check print without opening the PDF. Ambiguous evidence stays ambiguous, and approved-with-comments is visually distinct from approved.

**Blocked by:** 02: Scan check prints into a separate checking inventory.

**Status:** ready-for-agent

- [ ] Applied stamp annotations are read from the PDF annotation structure rather than by searching raw PDF text.
- [ ] No stamp yields `FC`; a non-verdict stamp yields `UNKNOWN`; approved-with-comments yields `AWC`; approved yields `APPD`; conflicting verdicts yield `CONFLICT`.
- [ ] Back-drafted is derived independently and never replaces the verdict status.
- [ ] `UNKNOWN` and `CONFLICT` remain non-mutating and clearly visible for human review.
- [ ] Stamp author aliases are normalised at read time, while author and date are described as self-asserted workflow metadata.
- [ ] `AWC` and `APPD` use unmistakably different text and styling in the checking window.
- [ ] Tests cover every controlled and legacy stamp variant using approved real check-print fixtures, with synthetic PDFs limited to structural edge cases.
