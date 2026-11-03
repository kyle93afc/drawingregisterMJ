# Release Drawing Register

Follow [AGENTS.md Section 6](AGENTS.md#6-release-process-critical--read-carefully) for the complete release sequence, version checks, authentication, packaging and publication gates.

Save the GitHub token through the private secret field into the Git-ignored repo-root `.env.local` as a single `GITHUB_TOKEN=...` entry. Reuse this file for later releases. Never print it, paste a token into a shell command, or commit it.

Load the credential from PowerShell at the repo root:

```powershell
. .agents/skills/ship-release/Import-ReleaseEnvironment.ps1
```

The loader reads the token without executing the file or displaying its contents. It can also use an existing `GITHUB_TOKEN` environment variable. Validate authentication before uploading and replace expired credentials privately.

After publishing, verify installed users can discover the new version through the public Velopack feed:

```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/ship-release/Verify-UpdateFeed.ps1 `
  -PreviousVersion "PREVIOUS" `
  -ExpectedVersion "TARGET"
```

A release is complete only when the public release is Latest, its tag and `working-version` match the intended commit, all six assets are attached, and the update-feed check passes.
