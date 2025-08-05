# Drawing Register Build and Release Script
# This script builds the application and creates a Squirrel installer

$ErrorActionPreference = "Stop"

# Configuration
$projectPath = "DrawingRegister.App\DrawingRegister.App.csproj"
$outputDir = "publish"
$releaseDir = "Releases"

Write-Host "Building Drawing Register..." -ForegroundColor Green

# Clean previous builds
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
if (Test-Path $releaseDir) {
    Remove-Item $releaseDir -Recurse -Force
}

# Build the application
Write-Host "Building application..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $outputDir

# Create NuGet package for Squirrel
Write-Host "Creating NuGet package..." -ForegroundColor Yellow
$version = (Get-Content $projectPath | Select-String '<Version>(.*)</Version>').Matches[0].Groups[1].Value
$nuspecContent = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>DrawingRegister</id>
    <version>$version</version>
    <title>Drawing Register</title>
    <authors>Your Company Name</authors>
    <description>Engineering drawing and document management system</description>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
  </metadata>
  <files>
    <file src="publish\**\*.*" target="lib\net45\" />
  </files>
</package>
"@

$nuspecContent | Out-File -FilePath "DrawingRegister.nuspec" -Encoding UTF8

# Pack the NuGet package
nuget pack DrawingRegister.nuspec -OutputDirectory .

# Create Squirrel release
Write-Host "Creating Squirrel installer..." -ForegroundColor Yellow
$nupkgFile = "DrawingRegister.$version.nupkg"

# Create Releases directory
New-Item -ItemType Directory -Force -Path $releaseDir

# Build Squirrel installer
squirrel --releasify $nupkgFile --releaseDir $releaseDir --no-msi

Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Installer created in: $releaseDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Test the Setup.exe in $releaseDir"
Write-Host "2. Create a GitHub release and upload all files from $releaseDir"
Write-Host "3. Update the GitHub URL in App.xaml.cs"

# Clean up
Remove-Item DrawingRegister.nuspec
Remove-Item $nupkgFile