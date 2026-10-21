# Drag and drop update (experimental)

- All three views initiate standard Avalonia Files drags with multiple selection.
- Drag to another WA1GONFM window, Nemo, or another URI-capable application.
- Drop files onto the file pane or a tab header; hover over a tab to activate it.
- Ctrl forces copy, Shift forces move. Internal WA1GONFM drags default to move; external drops default to copy.
- Cross-filesystem move falls back to copy-then-delete only for EXDEV errors.
- Existing overwrite and move confirmation settings apply.

**Important:** Build and runtime behavior have not been verified against Avalonia on a Linux desktop in this environment. Test drag initiation, tab hover, multiple selection, external drag interoperability, and move/copy modifier behavior in Rider. The destination of drops is the active directory, not a folder row beneath the pointer.

Version 0.7.3: Expose text/uri-list and GNOME file URI payloads; accept both on drops. External drag behavior requires Linux desktop testing.
