# filepilot

A .NET 10 and Avalonia 12.1.4 file manager for Linux. The application is named **filepilot** (lowercase). The existing GitHub repository, solution, project directories, and C# namespaces retain the **WA1GONFM** name.

## What makes filepilot different?

**filepilot** is a lightweight Linux file manager built with **.NET 10 and Avalonia 12**, with particular attention to working *with* the Linux desktop rather than in isolation.

- **Native file-manager interoperability.** Drag files between filepilot and **Nemo in either direction**, or drag files onto folders inside filepilot. This has been tested on Linux Mint Cinnamon/X11.
- **Clipboard compatibility with Nemo.** Copy and cut files between filepilot and the desktop file manager, including multi-file operations.
- **Three ways to browse.** Switch between **Details**, **Icons**, and **Compact** views, with selection carried across view changes.
- **Multi-file operations.** Ctrl+Click, Shift+Click, and Ctrl+A select multiple files or folders; copy, cut, paste, and delete operate on the selection. Delete uses Trash, while Shift+Delete requests permanent deletion with confirmation.
- **Tabbed navigation.** Work in multiple directories without opening a separate window for each one.
- **Desktop-aware directory opening.** Launch `filepilot ~/Documents` or open a local `file://` URL. Optionally register filepilot as the default handler for directories (`inode/directory`).
- **One executable for deployment.** Publish a self-contained **linux-x64** binary with the .NET runtime bundled; target systems still need compatible Linux native libraries.
- **Desktop integration without changing your defaults.** The installer adds the custom folder-and-compass icon and a `.desktop` launcher, including Cinnamon taskbar association. Changing the system's default file manager remains opt-in.

**Platform scope:** filepilot is currently a Linux-only application. There are no plans at this time to support Windows or macOS. Linux Mint Cinnamon on X11 is the primary tested environment; other Linux distributions, desktop environments, and Wayland require additional testing.

The Details view Type column automatically sizes to file type descriptions (up to 320 pixels) and stays aligned with its header.

## Requirements

- Linux x64 desktop with X11 (tested on Linux Mint Cinnamon with Nemo)
- .NET 10 SDK to build and run from source
- `gio` for trash integration

Run all commands below from the extracted `WA1GONFM` directory (the folder containing `WA1GONFM.sln`).

## Build and automated tests

```bash
dotnet restore
dotnet build
dotnet test
```

Expected: the solution builds successfully, and all **13 tests** pass (the baseline confirmed before this renamed source package). The renamed package itself has not been independently compiled in the packaging environment.

To run the two test projects individually:

```bash
dotnet test tests/WA1GONFM.Core.Tests/WA1GONFM.Core.Tests.csproj
dotnet test tests/WA1GONFM.UI.Tests/WA1GONFM.UI.Tests.csproj
```

## Run from source

```bash
dotnet run --project src/WA1GONFM.App
```

## Publish a single-file Linux executable

```bash
chmod +x scripts/publish-linux-x64.sh
./scripts/publish-linux-x64.sh
./dist/linux-x64/filepilot
```

The published executable is `dist/linux-x64/filepilot`. **Do not copy the `filepilot` launcher from `bin/Debug/net10.0` to the Desktop by itself**: a normal build requires its companion `filepilot.dll` and other files. Use the self-contained published binary instead.

To put the published executable on the Desktop:

```bash
cp dist/linux-x64/filepilot ~/Desktop/filepilot
chmod +x ~/Desktop/filepilot
~/Desktop/filepilot
```

The publish script targets `linux-x64` and includes the .NET runtime. Linux desktop system libraries are still required.

## Build, test, and install (Linux x64)

The install script builds in Release mode, runs the automated tests, publishes a self-contained single-file binary, and installs it as `filepilot`. It uses `sudo` **only** for the installation when the destination requires elevated permissions.

Install to the default `/usr/local/bin/filepilot`:

```bash
./scripts/build-install.sh
filepilot
```

Install to another directory, supplied as the **first argument**:

```bash
./scripts/build-install.sh "$HOME/.local/bin"
"$HOME/.local/bin/filepilot"
```

The argument is a **directory**, not the complete executable filename. If using a custom directory, ensure it is in your `PATH` if you want to launch `filepilot` without its full path. This script targets `linux-x64` and requires the .NET SDK to build. The title bar shows the product name and assembly version, for example **filepilot v0.7.36**.

## Manual functional test steps

**Use disposable files and folders only.** In particular, confirm whether drag-and-drop copies or moves before testing anything important.

1. Start `filepilot` and Nemo side by side. Confirm filepilot opens without an exception and displays the current directory.
2. Create two temporary folders and a test file in Nemo. Browse to the folders in filepilot and confirm file listings refresh as expected.
3. **filepilot to Nemo:** Drag the test file from filepilot into a Nemo folder. Verify the destination file exists and its contents are intact.
4. **Nemo to filepilot:** Drag another test file from Nemo into the filepilot file view. Verify the destination file exists and its contents are intact.
5. **Within filepilot:** Drag a disposable file onto a directory in filepilot. Verify the result and whether it copied or moved as expected.
6. Switch between **Details**, **Icons**, and **Compact** views. Verify files remain visible and selection/navigation still work.
7. Open a second tab and navigate between directories. Verify tab switching and directory navigation.
8. Test copy/paste and cut/paste with disposable files using **Ctrl+C**, **Ctrl+X**, and **Ctrl+V**. Verify copy preserves the source and cut/move has the expected result.
9. Test **F2** rename and the context menu. If testing Delete or Shift+Delete, use only disposable data and confirm prompts and trash/permanent-delete behavior.
10. Open Settings, change a non-destructive preference, close and restart filepilot, and verify the setting persists.
11. Launch the **published** `dist/linux-x64/filepilot` binary and repeat the startup and Nemo drag-and-drop checks to ensure the packaged executable behaves like the development build.

Drag-and-drop in both directions with Nemo and internal drag-and-drop were confirmed working on the user's Linux Mint desktop before the application rename. These manual checks are still needed for a new build or distribution.

## Troubleshooting

- `The application to execute does not exist: .../filepilot.dll`: you copied a framework-dependent launcher without its DLL. Publish with `scripts/publish-linux-x64.sh` and use `dist/linux-x64/filepilot`.
- `CS0433` for xUnit `FactAttribute` or `TheoryAttribute`: ensure the UI test project uses `xunit.v3` only, not both `xunit` v2 and v3. The included project uses xUnit v3.2.2.
- For drag-and-drop issues, run from a terminal and inspect `[FM-DND ...]` diagnostic messages.

## Project naming and notes

The repository and solution remain `WA1GONFM`; the user-facing application name and published executable are `filepilot`. Existing settings paths and C# namespaces are intentionally preserved for compatibility. See [FILEPILOT-RENAME.md](FILEPILOT-RENAME.md) for migration details.

Earlier release notes are retained in Git history; this README describes the current v0.7.36 package rather than older scaffold limitations.

## Application icon and desktop launcher

The blue folder-and-compass icon is embedded in the Avalonia window and included as
`src/WA1GONFM.App/Assets/filepilot-256.png` (other sizes also provided).
Running `./scripts/build-install.sh [INSTALL_DIRECTORY]` also installs the icon to
`~/.local/share/icons/hicolor/256x256/apps/filepilot.png` and creates a user
desktop launcher at `~/.local/share/applications/filepilot.desktop`.
The desktop launcher uses the chosen installation directory. You may need to
refresh the Cinnamon menu or log out and back in for the icon to appear.

`.gitignore` excludes all `*.pdb` files. Already tracked PDB files must be
removed from Git's index separately (`git rm --cached <path>`).


## Open directories from other applications

filepilot accepts a directory path or a local `file://` URL as its first argument.

```bash
filepilot ~/Documents
filepilot 'file:///home/your-user/My%20Documents'
```

The installer registers the `inode/directory` MIME type but does not change your existing default file manager. To opt in:

```bash
xdg-mime default filepilot.desktop inode/directory
xdg-mime query default inode/directory
xdg-open ~/Documents
```

To restore Nemo on Mint Cinnamon:

```bash
xdg-mime default nemo.desktop inode/directory
```

The `.desktop` launcher passes folder URLs with `%U`. If the argument is missing, invalid, or points to a non-directory, filepilot starts in your home directory.

## Multi-selection

In Details, Icons, or Compact view, use Ctrl+Click to toggle items, Shift+Click to select a range, and Ctrl+A to select all when the file list has focus. Ctrl+C copies all selected paths, Ctrl+X cuts them, Ctrl+V pastes, Delete moves selected items to Trash, and Shift+Delete permanently deletes after confirmation when enabled. Operations apply to both files and directories.

### Sorting by extension

In Details view, the **Ext** column displays file extensions without the leading dot. Click the Ext header to sort ascending or descending, or choose **Extension** from the Sort dropdown. Folder extensions are blank, sorting is case-insensitive, and the existing Folders first setting remains effective.


### Empty files and signature verification

Zero-byte files (including empty `.txt`, `.pdf`, and extensionless files) are shown as **Empty**, not as a mismatch. Linux `libmagic` normally reports `inode/x-empty` for these files, but no file signature exists to compare against the extension. Empty files are therefore **not verified** and receive no red or amber security highlighting. An empty `.pdf` is not a valid PDF, but that is a file-validity issue rather than evidence of malicious content. Once content is added and the file is refreshed, normal MIME verification applies.

## File Type Integrity (experimental)

filepilot uses Linux `libmagic.so.1` to check regular file contents against common filename extensions in the background. In Details view, high-risk executable mismatches receive a red-tinted row, ordinary mismatches amber, and the Security column shows status. Icons and Compact views highlight filenames. Hover for expected and detected MIME types. Extensionless and unrecognized formats are not treated as malicious. This is **not antivirus scanning**: a match does not establish safety, and a mismatch does not prove malware. Some valid file formats use containers or ambiguous signatures; false positives are possible. Symlinks are skipped. Detection requires libmagic and is currently targeted at Linux.


## File type detection and integrity

filepilot uses `libmagic` to identify the actual content MIME type of **all regular files**, including extensionless names such as `LICENSE`, `README`, and `Makefile`. The **Type** column shows a friendly detected type (for example, **Text Document** for `text/plain`), while **Ext** remains blank when the filename has no extension. If `libmagic` is unavailable, the display falls back to the filename extension or an unknown state.

File Type Integrity compares detected MIME with the expected type for known extensions. Red highlights indicate high-risk executable disguises, amber highlights indicate other mismatches. Extensionless and empty files are not treated as mismatches. This is advisory detection, not antivirus scanning.

## Help and About

Use the **Help** button in the toolbar for navigation, shortcuts, and file-integrity guidance. Use **About** to see the installed filepilot version, read from the application's assembly version metadata.

### MIME type display (v0.7.15)

The Type column uses a centralized MIME description resolver. Common types such as AAC Audio, OpenDocument Text, Word Document, and Python Script are shown with readable names. Unknown formats use readable category-based fallback descriptions instead of raw MIME identifiers. The original MIME value is retained for integrity comparisons and diagnostic tooltips; a MIME type alone is not a security warning.

### ADIF amateur radio logs (v0.7.15)

For `.adi` files, filepilot checks a bounded portion of the file for well-formed ADIF tags, declared value lengths, and header/record delimiters. Verified logs display **Amateur Radio ADIF Log** and do not receive a false mismatch warning when libmagic would otherwise report AAC audio. Files that do not validate continue through normal libmagic detection. ADIF identification is advisory and is not malware scanning.

### Details Type column (v0.7.19)

The Details Type column uses a consistent 270-pixel width in the header and item templates, with internal spacing and ellipsis for exceptionally long descriptions. This prevents the Type and Ext columns from overlapping.

### v0.7.19 UI and warning fixes

- Name column is 360 px wide, and Details view supports horizontal scrolling when the window is too narrow for the full table.
- Replaced obsolete `TextBox.Watermark` with `PlaceholderText`.
- Added a public parameterless `MainWindow` constructor for Avalonia XAML runtime-loader compatibility.

### v0.7.19 Sidebar panels

Shortcuts and Directories are now expandable/collapsible panels. Drag the horizontal splitter between them to adjust their relative heights when both are open. The existing vertical splitter still adjusts sidebar width.

### v0.7.19 Sidebar layout and licensing

- The Shortcuts and Directories expanders share the sidebar height equally when both are open.
- Collapsing either panel reduces it to its header and makes the other panel fill the available height, with no blank spacer.
- A draggable divider remains available when both panels are expanded.
- The About dialog now includes the MIT License notice. See `LICENSE` for full license terms.

### v0.7.24 Unix dotfile extensions

The Ext column, extension sorting, and content-integrity comparison now treat a leading dot in Unix dotfiles such as `.bash_history`, `.gitignore`, and `.env` as part of the name, rather than a file extension. Names with a separate subsequent dot, such as `.config.json`, still show `json` as their extension. Added automated tests for these cases.

### Help dialog (v0.7.24)

Help now provides a scrollable, sectioned guide covering navigation, keyboard shortcuts, file operations, sidebar controls, file type detection, and File Type Integrity warning levels.

### v0.7.24 Shortcuts navigation

Shortcuts are displayed alphabetically by their folder name (case-insensitive, with path as a tie-breaker). The **+ Add Shortcut** button is located beside **+ New Tab** above the file views.

### v0.7.24 Linux Open With application picker

The Open With dialog now discovers installed applications from XDG `.desktop` files,
shows only MIME-associated applications by default, with an explicit **Show all installed applications**
checkbox for choosing another application when needed. It displays the default application, supports opening by
selection/double-click, manual executable entry, and an optional **Always use**
checkbox that updates the Linux MIME default through `xdg-mime`.

Uses `gio mime` for desktop associations and `gtk-launch` for launching `.desktop`
applications, respecting their registered command arguments. The Open With dialog
requires `gio`, `gtk-launch`, and `xdg-mime` to be installed, as is typical on
Linux Mint Cinnamon.

### v0.7.25 Open With checkbox compatibility fix

Fixes Avalonia 12 build errors caused by using WPF-style `CheckBox.Checked` and `CheckBox.Unchecked` events. The Show all installed applications option now responds to changes in `ToggleButton.IsCheckedProperty`. Compatible applications still appear by default.

### v0.7.26 Add applications to Open With

In **Open With**, check **Show all installed applications**, select a program, and click **Add to list**.
The application will appear in the compatible choices for that file type on subsequent visits.
Choose **Remove from list** to remove a custom entry. This only changes filepilot's list, not the
Linux system MIME defaults. Selecting **Always use** when opening an application changes the default
through `xdg-mime`, as before. Custom choices are stored in
`~/.config/filepilot/open-with-apps.json` (or `$XDG_CONFIG_HOME/filepilot/open-with-apps.json`).

### v0.7.30 Automatic refresh and Open With sections

- Automatically refresh the active directory when files change outside filepilot, debounced 350 ms. The Settings checkbox enables or disables monitoring.
- Preserve current selection and scroll offset during watcher-triggered refreshes.
- Open With shows alphabetized known applications for the MIME type; optional Show all displays a separate, alphabetized list of other installed applications.
- Add and remove filepilot's custom MIME application associations. Linux-registered associations remain visible until changed through Linux settings.

### v0.7.30 Incremental directory updates

Directory watcher content changes update individual file rows without rebuilding the full list; structural changes still refresh the directory. Right-click and drag interactions defer watcher refreshes to avoid disrupting context menus. Linux shell scripts are executable (mode 755).

### v0.7.30 Open With executable registration

The Open With dialog can add programs by absolute executable path (for example `/usr/bin/code`) even when their desktop entry is missing or hidden. These entries are stored per MIME type and can be removed without changing system file associations.

### v0.7.30 Friendly labels for manually added executables

Open With now displays friendly names for common executables such as `/usr/bin/code` (Visual Studio Code), while preserving their full executable paths for launching. Hover an application entry to inspect its executable path or desktop entry ID.

### v0.7.32 Configurable Open With application names

The Open With dialog has **Edit display name** for manually added executables.
Names are shared across MIME types and saved in `~/.config/filepilot/open-with-apps.json`
under `applicationNames`. The friendly name is resolved by user configuration first,
then Linux `.desktop` metadata (`Name` and `Exec`), then the executable filename.
Existing per-MIME associations are preserved and migrated from the earlier JSON format.

### v0.7.32 Open With management

Open With now allows hiding system-discovered applications from the Known list for a particular MIME type using Remove from list. Hidden applications remain available under Show all installed applications and can be restored with Add selected to list. Edit display name is available for both desktop-registered applications and manually supplied executable paths. These choices only affect filepilot and do not modify Linux MIME associations.

### v0.7.33 Open With mouse-wheel scrolling

- Scroll the Known Applications and All Applications lists using the mouse wheel.
- The dialog's outer scrolling remains available when the selected list reaches its top or bottom.
- Preserves MIME filtering, list selection, custom display names, and application management.

### v0.7.36 Edit Display Name dialog positioning

The Edit Display Name modal uses Open With as its owner, keeping it in front of the Open With dialog.

### v0.7.36 Open With display-name editor

The Edit Display Name controls are pinned below the Open With application lists, instead of appearing outside the visible scrolled region. The dialog now reserves additional vertical space and focuses the edit box after layout.

### Open With defaults

For registered desktop applications, **Always use** updates Linux's MIME default.
For manually added executables, it saves a filepilot-only MIME default in
`~/.config/filepilot/open-with-apps.json` under `preferredApplications`.
