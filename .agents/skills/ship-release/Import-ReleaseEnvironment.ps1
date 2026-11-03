[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot '../../../.env.local')
)

$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $Path) {
    $tokenLines = @(Get-Content -LiteralPath $Path | Where-Object { $_ -match '^\s*GITHUB_TOKEN\s*=' })
    if ($tokenLines.Count -ne 1) {
        throw '.env.local must contain exactly one GITHUB_TOKEN entry.'
    }

    $releaseToken = ($tokenLines[0] -split '=', 2)[1].Trim()
    if ($releaseToken.Length -ge 2 -and
        (($releaseToken.StartsWith('"') -and $releaseToken.EndsWith('"')) -or
         ($releaseToken.StartsWith("'") -and $releaseToken.EndsWith("'")))) {
        $releaseToken = $releaseToken.Substring(1, $releaseToken.Length - 2)
    }
    if ([string]::IsNullOrWhiteSpace($releaseToken)) {
        throw 'GITHUB_TOKEN in .env.local is empty.'
    }
    $env:GITHUB_TOKEN = $releaseToken
    Remove-Variable releaseToken, tokenLines
}

if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    throw 'Provide GITHUB_TOKEN through the private secret field and save it in the ignored .env.local file.'
}
