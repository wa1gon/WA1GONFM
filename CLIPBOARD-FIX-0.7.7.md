# WA1GONFM v0.7.7 clipboard change

Removed the trailing LF from the `x-special/gnome-copied-files` clipboard payload, matching the Nemo clipboard sample. `text/uri-list` continues to end in CRLF. This is a targeted interoperability experiment; Nemo behavior remains to be tested.
