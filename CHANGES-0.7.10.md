# WA1GONFM v0.7.10

- Added persistent sort by name, size, type, or modification date; ascending/descending; folders first.
- Added sortable column headings to Details view.
- Simplified drag payload to native Avalonia Files and standard text/uri-list, removing clipboard-only and private targets that may confuse XDND interoperability.
- Drop action now checks allowed XDND effects.

**Testing limitation:** This source package was not compiled or exercised under Cinnamon/X11. Nemo drag-and-drop interoperability remains unverified. If the X cursor persists, instrument native XDND advertised targets and actions before declaring it fixed.
