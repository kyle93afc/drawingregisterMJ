---
name: wpf-drawing-register
description: Development workflow for the Drawing Register WPF application (.NET 8) with Visual Studio 2022 Hot Reload. Use when making changes to DrawingRegister.App, working with XAML UI, C# code-behind, or models. Enables live development without full rebuilds.
---

# WPF Drawing Register Development

## Hot Reload Workflow

Visual Studio 2022 supports Hot Reload for .NET 8 WPF apps. The user runs the app in Visual Studio - you make edits, they see changes live.

### What Hot Reloads Instantly

**XAML (always works):**
- Layout changes (Grid, StackPanel, margins, sizes)
- Styling (colors, fonts, brushes)
- Control properties and bindings
- DataTemplates and resource dictionaries
- New controls in existing containers

**C# (usually works):**
- Method body changes
- Lambda/expression changes
- Adding new methods to existing classes
- Property getter/setter logic
- Event handler implementations

### What Requires Restart

- New classes or files
- Constructor changes
- Interface/inheritance modifications
- Static field/method changes
- Changes to App.xaml.cs startup
- NuGet package additions

## Making Hot Reload-Friendly Changes

1. **Prefer XAML over C#** - UI changes in XAML always hot reload
2. **Edit method bodies, not signatures** - Parameter changes need restart
3. **Use existing classes** - Add methods to existing files rather than new classes
4. **Test incrementally** - Small changes hot reload reliably

## Project Quick Reference

```
DrawingRegister.App/
├── MainWindow.xaml/.cs      # Main UI - Hot Reload friendly
├── Models/                  # Business logic
│   ├── DocumentMetadata.cs  # Core document with revisions
│   ├── ProjectManager.cs    # Project operations
│   └── ProjectStorage.cs    # JSON persistence
├── Converters/              # Value converters for bindings
├── *Dialog.xaml/.cs         # Modal dialogs - Hot Reload friendly
└── Resources/               # Images, logos
```

**Key bindings pattern:** DataContext is set in code-behind, properties use `{Binding PropertyName}`.

**Brand colors:** Red #eb1845, gray theme with Material Design styling.

## Commands

Do NOT run these - user handles Visual Studio:
- Build: `dotnet build`
- Run: F5 in Visual Studio (enables Hot Reload)

After edits, user presses Ctrl+Shift+Enter in VS to apply Hot Reload, or changes apply automatically for XAML.
