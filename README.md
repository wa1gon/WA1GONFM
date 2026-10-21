# filepilot

A .NET 10 and Avalonia 12.1.4 file manager for Linux. The application is named **filepilot** (lowercase). The existing GitHub repository, solution, project directories, and C# namespaces retain the **WA1GONFM** name.

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

The argument is a **directory**, not the complete executable filename. If using a custom directory, ensure it is in your `PATH` if you want to launch `filepilot` without its full path. This script targets `linux-x64` and requires the .NET SDK to build. The title bar shows the product name and assembly version, for example **filepilot v0.7.13**.

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

Earlier release notes are retained in Git history; this README describes the current v0.7.13 package rather than older scaffold limitations.

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
