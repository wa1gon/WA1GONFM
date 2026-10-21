# filepilot v0.7.13 (Avalonia 12.1.4)

This source package preserves the existing **WA1GONFM** repository, solution, project folders, and C# namespaces. The visible application name, settings window title, published binary, and application assembly are **filepilot** (lowercase).

## Build and run

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/WA1GONFM.App
```

The Linux publish script produces `dist/linux-x64/filepilot`.

## Included migration fixes

- Avalonia 12.1.4 X11 drag-and-drop and clipboard changes in the complete MainWindow code-behind
- Avalonia 12 XAML event/property updates
- xUnit v3.2.2 for UI tests, avoiding mixed v2/v3 attributes
- Tmds.DBus.Protocol 0.94.1, as used in the successful local build

Existing settings location and C# namespaces remain unchanged to avoid breaking user settings or project references. The GitHub repository is not renamed.

**Validation:** The user reported successful build, 13 passing tests, and working drag-and-drop in both directions with Nemo and internally on their machine. This repackaged rename has not been built in the packaging environment.
