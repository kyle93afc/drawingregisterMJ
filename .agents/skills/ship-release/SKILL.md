---
name: ship-release
description: Release Drawing Register through Velopack to GitHub. Use when publishing, shipping, bumping a version, creating a GitHub release, or verifying automatic updates.
---

# Release

Read and follow `AGENTS.md` Section 6 completely; it is canonical. Add these release gates.

## Before packaging

1. Record the previous public version and the target version.
2. Confirm `working-version`, the latest remote tag/release, and the exact commit being shipped.
3. Inspect `git status --short`. If unrelated changes exist, get the user's scope decision and package from a clean worktree at the release commit.
4. Confirm `DrawingRegister.App/Services/UpdateService.cs` uses `SimpleWebSource` with `releases/latest/download` for update detection. A public release must not depend on GitHub's unauthenticated API quota.
5. Run the full test suite from the exact clean commit being shipped.
6. Keep the previous full package in `Releases/` before `vpk pack`, downloading it from the previous release if necessary. This is required to generate the delta package.

The gate is complete only when the release commit is pushed, all required version strings match, tests pass, and both the target full and delta packages exist locally.

## After publishing

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/ship-release/Verify-UpdateFeed.ps1 `
  -PreviousVersion "PREVIOUS" `
  -ExpectedVersion "TARGET"
```

Then verify:

- the release is public, stable, and Latest;
- its tag and `working-version` both point to the intended release commit;
- all six Velopack assets from the `AGENTS.md` checklist are attached;
- the verification script reports that Velopack offers `TARGET` to an installed `PREVIOUS` through the public static feed.

A visible GitHub release is not completion. Finish only when every check above passes.
