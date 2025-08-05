# Drawing Register Installer & Auto-Update Setup Guide

## Overview
This guide explains how to build, deploy, and maintain the Drawing Register application with automatic updates via GitHub Releases.

## Prerequisites
1. Install NuGet CLI: `winget install NuGet.NuGet`
2. Install Squirrel tools: `dotnet tool install -g Squirrel.Windows`
3. GitHub repository with releases enabled

## Initial Setup

### 1. Update GitHub URL in App.xaml.cs
Replace line 104 in `App.xaml.cs`:
```csharp
string updateUrl = "https://github.com/YourUsername/YourRepo/releases";
```
With your actual GitHub repository URL.

### 2. Update Version Number
In `DrawingRegister.App.csproj`, update the version:
```xml
<Version>1.0.0</Version>
```

## Building the Installer

### Method 1: PowerShell Script (Recommended)
```powershell
.\build-installer.ps1
```

### Method 2: Manual Steps
```powershell
# 1. Build the application
dotnet publish DrawingRegister.App\DrawingRegister.App.csproj -c Release -r win-x64 --self-contained false -o publish

# 2. Create NuGet package
nuget pack DrawingRegister.nuspec

# 3. Create Squirrel installer
squirrel --releasify DrawingRegister.1.0.0.nupkg --releaseDir Releases
```

## Creating a GitHub Release

1. Go to your GitHub repository
2. Click "Releases" → "Create a new release"
3. Tag version: `v1.0.0` (match your project version)
4. Release title: `Drawing Register v1.0.0`
5. Upload ALL files from the `Releases` folder:
   - `Setup.exe` (main installer - users download this)
   - `DrawingRegister-1.0.0-full.nupkg` (update package)
   - `RELEASES` (metadata file)
   - Any `.exe` and `.nupkg` delta files

## How Updates Work

1. When users run the app, it checks GitHub for new releases
2. If a new version exists, users get a notification
3. Updates download in the background
4. App restarts automatically after update

## Version Numbering

Always increment version numbers:
- Major changes: `1.0.0` → `2.0.0`
- New features: `1.0.0` → `1.1.0`
- Bug fixes: `1.0.0` → `1.0.1`

## Deployment Workflow

1. Make code changes
2. Update version in `.csproj`
3. Commit and push changes
4. Run `.\build-installer.ps1`
5. Test `Setup.exe` locally
6. Create GitHub release
7. Upload all files from `Releases` folder

## First-Time Installation

Users should:
1. Download `Setup.exe` from latest GitHub release
2. Run installer (may need to bypass Windows SmartScreen)
3. App installs to `%LOCALAPPDATA%\DrawingRegister`
4. Desktop shortcut created automatically
5. Updates check automatically on startup

## Troubleshooting

### "Unable to download updates"
- Check GitHub URL is correct
- Ensure repository is public or users have access
- Verify all release files were uploaded

### Updates not detected
- Ensure version number was incremented
- Check `RELEASES` file was uploaded
- Verify GitHub release is not marked as "pre-release"

### Installation fails
- Run as administrator
- Check antivirus isn't blocking
- Ensure .NET 8 is installed on target machine

## Alternative: ClickOnce (Simpler but less flexible)

If Squirrel seems complex, consider ClickOnce:
1. Right-click project → Publish
2. Choose "ClickOnce"
3. Set update URL
4. Publish to web server or file share

## Notes

- First setup takes ~30 minutes
- Subsequent releases take ~5 minutes
- Users love automatic updates - worth the setup effort!
- Consider code signing certificate for trust ($200-500/year)