# 14: Import legacy check-print history

**What to build:** Older jobs with mixed naming schemes (job 17749 has five schemes across 58 date folders) can be scanned into the inventory so their history is visible, without renaming anything on those jobs.

**Blocked by:** 12: Write the check status into the filename.

**Status:** blocked

- [ ] Each legacy naming scheme observed on job 17749 is listed with an example filename and either parsed or explicitly rejected with a precise warning.
- [ ] Legacy CP counters that reset mid-job are recorded as-is; the tool does not renumber.
- [ ] Legacy jobs can be marked read-only so ticket 12 and 13 mutations never run on them.
- [ ] The 17749 all-fact scan parses the majority of files and the remainder are listed in a warning report for a human.
