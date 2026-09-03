[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$PreviousVersion,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
[xml]$project = Get-Content (Join-Path $repoRoot 'DrawingRegister.App/DrawingRegister.App.csproj')
$velopackVersion = ($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Velopack').Version

if (-not $velopackVersion) {
    throw 'Could not determine the Velopack package version.'
}

$tempDir = Join-Path ([IO.Path]::GetTempPath()) "drawingregister-update-check-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    Push-Location $tempDir

    & dotnet new console --framework net8.0 --no-restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to create the update-check project.' }

    & dotnet add package Velopack --version $velopackVersion --no-restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to reference Velopack.' }

    @'
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

var locator = new TestVelopackLocator(
    "DrawingRegister",
    args[0],
    Path.Combine(Environment.CurrentDirectory, "packages"),
    null!);
var source = new SimpleWebSource(
    "https://github.com/kyle93afc/drawingregisterMJ/releases/latest/download");
var update = await new UpdateManager(source, null, locator).CheckForUpdatesAsync();
var actual = update?.TargetFullRelease?.Version?.ToString();

if (actual != args[1])
{
    Console.Error.WriteLine($"FAIL: installed {args[0]} expected {args[1]}, feed offered {actual ?? "no update"}.");
    return 1;
}

Console.WriteLine($"PASS: installed {args[0]} detects public {actual} through Velopack.");
return 0;
'@ | Set-Content -Path Program.cs -Encoding UTF8

    & dotnet run -- $PreviousVersion $ExpectedVersion
    if ($LASTEXITCODE -ne 0) { throw 'Public Velopack update-feed verification failed.' }
}
finally {
    Pop-Location
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}
