# Release Process

This document describes how to create and publish releases for the Document and Drawing Register application.

## Prerequisites

1. **Velopack CLI** - Install the Velopack CLI tool:
   ```powershell
   dotnet tool install -g vpk
   ```

2. **GitHub Personal Access Token** - For publishing releases to GitHub:
   - Go to GitHub Settings > Developer settings > Personal access tokens
   - Generate a token with `repo` scope
   - Set the environment variable: `$env:GITHUB_TOKEN = "your_token_here"`

## Release Steps

### 1. Update Version Number

Edit `DrawingRegister.App/DrawingRegister.App.csproj` and update the version numbers:

```xml
<Version>1.0.1</Version>
<AssemblyVersion>1.0.1.0</AssemblyVersion>
<FileVersion>1.0.1.0</FileVersion>
```

Also update the title in `MainWindow.xaml`:
```xml
Title="DOCUMENT AND DRAWING REGISTER v1.0.1"
```

### 2. Build the Application

Build a self-contained release for Windows x64:

```powershell
dotnet publish DrawingRegister.App -c Release --self-contained -r win-x64 -o ./publish
```

### 3. Create Velopack Package

Package the application using Velopack:

```powershell
vpk pack --packId "DrawingRegister" --packVersion "1.0.1" --packDir ./publish --mainExe "DrawingRegister.App.exe"
```

This creates:
- `Releases/DrawingRegister-1.0.1-full.nupkg` - Full installation package
- `Releases/DrawingRegister-1.0.1-delta.nupkg` - Delta package (for updates)
- `Releases/DrawingRegister-Setup.exe` - Installer for new users
- `Releases/RELEASES` - Release manifest

### 4. Publish to GitHub

Upload the release to GitHub:

```powershell
vpk upload github --repoUrl "https://github.com/kyle93afc/drawingregisterMJ" --tag "v1.0.1"
```

The `--tag` should match the version number with a `v` prefix.

## Quick Release Script

Create a release with a single script:

```powershell
# Set variables
$VERSION = "1.0.1"
$env:GITHUB_TOKEN = "your_token_here"

# Build
dotnet publish DrawingRegister.App -c Release --self-contained -r win-x64 -o ./publish

# Package
vpk pack --packId "DrawingRegister" --packVersion $VERSION --packDir ./publish --mainExe "DrawingRegister.App.exe"

# Upload to GitHub
vpk upload github --repoUrl "https://github.com/kyle93afc/drawingregisterMJ" --tag "v$VERSION"
```

## Testing Updates

1. Install version 1.0.0 from a GitHub release
2. Publish version 1.0.1
3. Launch the application - it should detect the update
4. Click "Yes" to download and install
5. Verify the application restarts with the new version

## Troubleshooting

### Application doesn't check for updates
- Ensure the app was installed via Velopack (not run from Visual Studio)
- Check logs in `%LOCALAPPDATA%\DrawingRegister\logs`

### GitHub upload fails
- Verify `GITHUB_TOKEN` is set with `repo` scope
- Check that the tag doesn't already exist
- Ensure you have push access to the repository

### Delta packages not working
- Delta packages require the previous version's `.nupkg` in the `Releases` folder
- For first release, only full package is created

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 1.0.0 | TBD | Initial release |
