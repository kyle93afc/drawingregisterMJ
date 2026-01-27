# Drawing Register Project

## Release Process

To release a new version:

1. **Update version numbers:**
   - `DrawingRegister.App/DrawingRegister.App.csproj` - Update `<Version>`, `<AssemblyVersion>`, `<FileVersion>`
   - `DrawingRegister.App/MainWindow.xaml` - Update title to include version

2. **Build, package, and upload:**
   ```powershell
   cd C:/Cursor_Files/drawingregisterMJ
   dotnet publish DrawingRegister.App -c Release --self-contained -r win-x64 -o ./publish
   vpk pack --packId "DrawingRegister" --packVersion "X.X.X" --packDir ./publish --mainExe "DrawingRegister.App.exe"
   vpk upload github --repoUrl "https://github.com/kyle93afc/drawingregisterMJ" --tag "vX.X.X" --token "YOUR_GITHUB_TOKEN"
   ```

3. **Publish the release:**
   ```powershell
   gh release edit vX.X.X --repo kyle93afc/drawingregisterMJ --draft=false --title "vX.X.X" --notes "Release notes here"
   ```

Users with the installed app will automatically get notified on next launch.
