# Simple Build Script for Drawing Register
# Creates a more manageable ~30-50MB single exe

Write-Host "Building Drawing Register (Trimmed Single File)..." -ForegroundColor Green

# Build trimmed single-file executable
dotnet publish DrawingRegister.App\DrawingRegister.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o dist

Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Executable location: dist\DrawingRegister.exe" -ForegroundColor Cyan
Write-Host "File size: $([math]::Round((Get-Item dist\DrawingRegister.exe).Length / 1MB, 2)) MB" -ForegroundColor Yellow

# Create a simple installer using IExpress (built into Windows)
Write-Host "`nCreating simple installer..." -ForegroundColor Yellow

$sedContent = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=Do you want to install Drawing Register?
DisplayLicense=
FinishMessage=Drawing Register has been installed successfully!
TargetName=DrawingRegisterInstaller.exe
FriendlyName=Drawing Register Installer
AppLaunched=DrawingRegister.exe
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0="DrawingRegister.exe"
[SourceFiles]
SourceFiles0=dist\
[SourceFiles0]
%FILE0%=
"@

$sedContent | Out-File -FilePath "installer.sed" -Encoding ASCII

# Run IExpress to create installer
iexpress /N installer.sed

Write-Host "`nInstaller created: DrawingRegisterInstaller.exe" -ForegroundColor Green

# Cleanup
Remove-Item installer.sed