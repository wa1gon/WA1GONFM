# WA1GONFM v0.7.13, Avalonia 12.1.4 drag/drop migration

Based on v0.7.12 XDND diagnostic build.

- Updated Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Headless and Avalonia.Headless.XUnit to 12.1.4.
- Drag source retains the PointerPressedEventArgs and uses DataTransfer with DataTransferItem.CreateFile and DragDrop.DoDragDropAsync.
- Drop target and diagnostics use DragEventArgs.DataTransfer.TryGetFiles.
- Source drag is **Copy only**, matching the successfully tested probe. Moving files via drag remains disabled pending a separate test to avoid unintended data loss.
- Existing clipboard code remains unchanged, but may require Avalonia 12 API updates depending on compatibility.

Run `dotnet restore && dotnet build && dotnet test` on your Linux machine. This package could not be compiled here because the .NET SDK is unavailable. Send any build errors for correction.
