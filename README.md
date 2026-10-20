# WA1GONFM

Native .NET 10 + Avalonia Linux file manager starter solution. Modern dark Fluent theme, tabs, shortcuts, root directory tree, Home/Root toggle, hidden/folder toggles, and configurable terminal launcher.

## Build and run (Linux Mint)

```bash
dotnet restore WA1GONFM.sln
dotnet build WA1GONFM.sln
dotnet test WA1GONFM.sln
dotnet run --project src/WA1GONFM.App
```

## One deployable executable

```bash
bash scripts/publish-linux-x64.sh
./dist/linux-x64/WA1GONFM.App
```

This creates **one self-contained application executable** from multiple source projects. Linux system libraries for Avalonia/X11/font rendering are still required; a single binary does not mean a statically linked binary or an installer. Publish on Linux x64 for Linux x64. Settings live in the user's config directory (`~/.config/WA1GONFM/settings.json` on typical Linux systems).

## Tests

- xUnit core tests: navigation history, directory filtering
- Avalonia.Headless UI smoke test: window initialization
- `tests/desktop-e2e/smoke.py`: optional AT-SPI smoke test requiring a desktop session and `python3-dogtail`; this is a starter check, not a complete E2E suite

## Important first-release limitations

This is an **initial runnable scaffold**, not a full replacement for Nemo/Krusader. Basic browsing and navigation are implemented. Double-click opens directories; file launching, file operations, tab closing, context menus, drag/drop shortcut reordering, search, and rich file details are not yet implemented. Directory tree child enumeration is lazy on selection, rather than expansion. The terminal arguments field only supports simple whitespace-separated arguments; prefer setting the executable alone. Terminal emulators vary in how they accept a working directory. The initial app has no elevated privilege support. Never run as root to work around permissions.

The .NET SDK is not installed in the artifact-generation environment, so the solution has **not been compiled or tested here**. If Rider or the CLI reports any package or API issues, resolve them before considering this a verified build.

## Fixes in revision 2

- UI test uses fully qualified `WA1GONFM.App.App` in the Avalonia test builder.
- Main window ignores selection and toggle events during XAML initialization, avoiding startup null-reference exceptions.
- Settings are loaded before XAML initialization.

**Security note:** The previously reported `NU1903` warning for transitive `Tmds.DBus.Protocol 0.21.2` is **not resolved** by this revision. Run `dotnet nuget why src/WA1GONFM.App/WA1GONFM.App.csproj Tmds.DBus.Protocol` and update the appropriate dependency to a patched version before production use.

This revision has not been build-verified because the .NET SDK is unavailable in this environment.


### v3 startup fix

`MainWindow` now calls the generated `InitializeComponent()` and explicitly resolves named XAML controls before accessing them. UI tests assert that essential controls exist. The `Tmds.DBus.Protocol` NU1903 advisory remains to be resolved after inspecting the transitive dependency chain.

## v4 fix
Removed manual named-control declarations and lookups from MainWindow.axaml.cs. Avalonia source-generated fields are initialized by InitializeComponent(). Build and tests have not been executed in this packaging environment.

## v5 changes
- System/Light/Dark setting (System default), applied without restarting.
- Higher-contrast tabs with close buttons.
- File operations: Ctrl+C copy, Ctrl+X move, Ctrl+V paste, F2 rename, Delete (permanent deletion with confirmation).
- Ctrl+T opens a tab. Drag files from the desktop into the file list to copy.
- File operation errors are displayed; existing destinations are never silently overwritten.
- **Caution:** Delete is permanent (no trash integration yet). Drag and drop currently supports importing external files into the active directory; internal drag-to-move is not yet implemented.
