# v0.7.12: XDND target registration test

- Explicitly enables Avalonia drop handling on the top-level Window and the root layout Grid, in addition to the existing file lists, tree, and tabs.
- Keeps the v0.7.11 drag diagnostics and the v0.7.10 sorting features.
- Does not modify outgoing drag data or claim that Nemo interoperability is resolved.

## Test on Cinnamon/X11

1. Run `dotnet run --project src/WA1GONFM.App`.
2. In a second terminal, run `xprop XdndAware` and click the FM file area. Compare the output with the earlier `not found` result.
3. Drag a file from Nemo to FM and capture the `[FM-DND]` output.
4. Drag from FM to Nemo separately. This is a distinct unresolved path.

This package has not been compiled or tested on X11 in the packaging environment.
