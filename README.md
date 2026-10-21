# filepilot

See [FILEPILOT-RENAME.md](FILEPILOT-RENAME.md) for current build instructions and changes.

## Historical development notes

# WA1GONFM v6

Changes from v5:
- File-list right-click context menu (open, open with system default, cut/copy/paste, rename, trash, permanent delete, properties, new folder, refresh, terminal).
- Window-level keyboard shortcuts work after switching tabs (Ctrl+C, Ctrl+X, Ctrl+V, Ctrl+T, F2, Delete, Shift+Delete). TextBox editing retains normal keyboard behavior.
- Confirmation preferences under Settings, persisted: move off, overwrite on, trash off, permanent delete on.
- Overwrite support for files and recursive directory copy; copy/move between tabs uses retained internal clipboard selection.
- Trash uses `gio trash` on Linux, so `gio` must be installed. Permanent delete remains available separately.
- Fixed UI test `FindControl` namespace import and added core overwrite tests.

**Limitations:** This is an unverified source release: .NET SDK is unavailable in the packaging environment. Clipboard currently works **inside WA1GONFM**, not with other applications. Drag/drop currently supports copying external files into the current directory; advanced intra-app drag/drop is not implemented. Flatpak packaging is **not yet provided** because offline NuGet and sandbox terminal integration need validation. Do not publish to Flathub without validating filesystem permissions and portal integration. Overwriting an existing directory merges copied contents; it is not an atomic replacement. Directory move overwrite replaces the destination directory. Test on disposable data first.

---

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

## File views (v7)

Use the **Details**, **Icons**, and **Compact** toolbar buttons, or Ctrl+1, Ctrl+2, Ctrl+3.
Each tab retains its own view choice while open. The Settings window provides the default
view for new tabs. Icon view automatically previews supported image files (PNG, JPEG,
BMP, GIF, WebP) with background decoding. There is no separate thumbnail mode.

The three views share file operations and the context menu.
