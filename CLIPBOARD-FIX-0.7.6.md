# WA1GONFM v0.7.6 clipboard fix

Removed Avalonia `DataFormats.Files` from the **clipboard** payload. GTK/Nemo can interpret the Avalonia storage-item clipboard target as an empty/unsupported file, producing an error about `""`. The clipboard now advertises URI text formats only: `text/uri-list` and `x-special/gnome-copied-files`. Drag-and-drop still advertises `DataFormats.Files`.

This is a targeted hypothesis based on the screenshot, not a confirmed runtime fix. Build and test in Rider, copying from FM into Nemo and vice versa.
