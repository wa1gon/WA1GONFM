# WA1GONFM v7 launcher and toolbar patch

- Double-click opens a directory in the active tab or opens a file via `LinuxFileLauncher`.
- libmagic (`libmagic.so.1`) identifies file content. Executable ELF binaries and shebang scripts launch directly **only** if Unix execute bits and effective `access(X_OK)` allow it.
- Other recognized files use `xdg-open` for the desktop's MIME association. An unrecognized octet-stream asks for Open With.
- Open With... currently provides a simple executable-name/path entry and a Use Default option. It is **not yet** a graphical list of installed desktop applications.
- Process execution uses `ProcessStartInfo.ArgumentList`, never `sh -c` or `bash -c`.
- Added New Folder to the toolbar. The existing handler creates it in the active tab and refreshes the tree/list.
- This patch includes the previous IsCheckedChanged settings warning fix and the 0.21.3 D-Bus package reference.

## Known limitations

- This archive has not been compiled in this environment. Build and test in Rider.
- If the desktop has no association for a MIME type, `xdg-open` may exit unsuccessfully; asynchronous exit-code reporting is a future improvement.
- The Open With dialog currently accepts a single executable name/path, not an application command with arbitrary flags.
- ELF/shebang recognition does not guarantee that the OS can run the file; errors from exec are reported.
- A shebang interpreter that exits with an error after starting is not currently captured.
