using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Diagnostics;
using System.Threading.Tasks;
namespace WA1GONFM.App;
public partial class MainWindow : Window
{
    private readonly DirectoryBrowser _browser = new();
    private readonly ObservableCollection<FileViewItem> _viewItems = new();
    private ListBox CurrentFilesList => Active?.ViewMode switch
    {
        "Icons" => IconFilesList,
        "Compact" => CompactFilesList,
        _ => FilesList
    };
    private readonly SettingsStore _store = new();
    private readonly TerminalLauncher _terminal = new();
    private readonly LinuxFileLauncher _launcher = new();
    private readonly FileOperations _operations = new();
    private string? _clipboardPath;
    private bool _clipboardCut;
    private readonly AppSettings _settings;
    private readonly ObservableCollection<NavigationTab> _tabs = new();
    private readonly DirectoryTreeNode _treeRoot = new("/");
    private Avalonia.Point? _dragStart;
    private ListBox? _dragSource;
    private FileViewItem? _pressedDragItem;
    private bool _dragging;
    private TabItem? _hoverTab;
    private DateTime _hoverTabSince;
    private bool _refreshing;
    private bool _initialized;
    private string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private NavigationTab? Active => Tabs.SelectedIndex >= 0 && Tabs.SelectedIndex < _tabs.Count ? _tabs[Tabs.SelectedIndex] : null;
    public MainWindow()
    {
        _settings = _store.Load();
        InitializeComponent();
        ThemeManager.Apply(_settings.Theme);
        HiddenToggle.IsChecked = _settings.ShowHidden;
        DirectoriesToggle.IsChecked = _settings.ShowDirectories;
        DirectoryTree.ItemsSource = new[] { _treeRoot };
        DirectoryTree.AddHandler(TreeViewItem.ExpandedEvent, Tree_Expanded, RoutingStrategies.Bubble);
        FilesList.ItemsSource = _viewItems;
        IconFilesList.ItemsSource = _viewItems;
        CompactFilesList.ItemsSource = _viewItems;
        // Receive pointer movement even when a ListBoxItem handles the routed event.
        foreach (var fileList in new[] { FilesList, IconFilesList, CompactFilesList })
            fileList.AddHandler(PointerMovedEvent, Files_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        // Handle wheel input consistently in the file views and sidebar, including
        // the icon WrapPanel, whose default scrolling behavior can vary by theme.
        foreach (var pane in new Avalonia.Controls.Control[] { FilesList, IconFilesList, CompactFilesList, DirectoryTree, ShortcutsList })
            pane.AddHandler(PointerWheelChangedEvent, Pane_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        RefreshShortcuts();
        _tabs.Add(new NavigationTab(Home) { ViewMode = _settings.DefaultViewMode });
        _initialized = true;
        RefreshTabs(0);
        AddHandler(KeyDownEvent, Window_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
    private void Pane_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        var scroll = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ScrollViewer>()
            ?? (sender as Avalonia.Visual)?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scroll is null) return;
        var horizontal = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var delta = horizontal ? (e.Delta.X != 0 ? e.Delta.X : e.Delta.Y) : e.Delta.Y;
        var nextX = horizontal ? Math.Clamp(scroll.Offset.X - delta * 48, 0, Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width)) : scroll.Offset.X;
        var nextY = horizontal ? scroll.Offset.Y : Math.Clamp(scroll.Offset.Y - delta * 48, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
        scroll.Offset = new Vector(nextX, nextY);
        e.Handled = true;
    }

    private void RefreshTabs(int selected)
    {
        _refreshing = true;
        Tabs.ItemsSource = _tabs.Select((t, index) =>
        {
            var close = new Button { Content = "×", Padding = new Thickness(5, 0), Tag = index };
            ToolTip.SetTip(close, "Close tab");
            close.Click += CloseTab_Click;
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock { Text = t.Title, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            header.Children.Add(close);
            return new TabItem { Header = header, Content = null };
        }).ToArray();
        Tabs.SelectedIndex = selected;
        _refreshing = false;
        RefreshFiles();
    }
    private void RefreshFiles()
    {
        if (Active is null) return;
        try
        {
            PathBox.Text = Active.CurrentPath;
            var entries = _browser.List(Active.CurrentPath, _settings.ShowHidden, _settings.ShowDirectories);
            _entries = entries;
            _viewItems.Clear();
            foreach (var entry in entries) _viewItems.Add(new FileViewItem(entry));
            ApplyViewMode();
            StatusText.Text = $"{entries.Count} items  •  {Active.CurrentPath}";
            _refreshing = true;
            HomeRootToggle.IsChecked = Active.CurrentPath == "/";
            _refreshing = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = ex.Message;
            _entries = Array.Empty<DirectoryEntry>();
            _viewItems.Clear();
        }
    }
    private IReadOnlyList<DirectoryEntry> _entries = Array.Empty<DirectoryEntry>();
    private void Navigate(string path)
    {
        try { Active?.Navigate(path); RefreshTabs(Tabs.SelectedIndex); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { StatusText.Text = ex.Message; }
    }
    private void Back_Click(object? sender, RoutedEventArgs e) { Active?.Back(); RefreshTabs(Tabs.SelectedIndex); }
    private void Forward_Click(object? sender, RoutedEventArgs e) { Active?.Forward(); RefreshTabs(Tabs.SelectedIndex); }
    private void Up_Click(object? sender, RoutedEventArgs e) { Active?.Up(); RefreshTabs(Tabs.SelectedIndex); }
    private void HomeRoot_Changed(object? sender, RoutedEventArgs e) { if (_initialized && !_refreshing) Navigate(HomeRootToggle.IsChecked == true ? "/" : Home); }
    private void Filters_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        _settings.ShowHidden = HiddenToggle.IsChecked == true;
        _settings.ShowDirectories = DirectoriesToggle.IsChecked == true;
        _store.Save(_settings);
        RefreshFiles();
    }
    private void Tabs_SelectionChanged(object? sender, SelectionChangedEventArgs e) { if (_initialized && !_refreshing) RefreshFiles(); }
    private void NewTab_Click(object? sender, RoutedEventArgs e) { _tabs.Add(new NavigationTab(Active?.CurrentPath ?? Home) { ViewMode = Active?.ViewMode ?? _settings.DefaultViewMode }); RefreshTabs(_tabs.Count - 1); }
    private void CloseTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || index < 0 || index >= _tabs.Count) return;
        if (_tabs.Count == 1) { _tabs[0] = new NavigationTab(Home) { ViewMode = _settings.DefaultViewMode }; RefreshTabs(0); return; }
        var selected = Tabs.SelectedIndex;
        _tabs.RemoveAt(index);
        RefreshTabs(Math.Clamp(selected > index ? selected - 1 : selected, 0, _tabs.Count - 1));
    }
    private string? SelectedEntry => CurrentFilesList.SelectedIndex is var i && i >= 0 && i < _entries.Count ? _entries[i].FullPath : null;
    private void ApplyViewMode()
    {
        var mode = Active?.ViewMode ?? _settings.DefaultViewMode;
        FilesList.IsVisible = mode == "Details";
        IconFilesList.IsVisible = mode == "Icons";
        CompactFilesList.IsVisible = mode == "Compact";
        ViewModeLabel.Text = mode;
        if (mode == "Icons")
            foreach (var item in _viewItems) item.LoadThumbnail();
    }
    private void SetViewMode(string mode)
    {
        if (Active is null) return;
        var selected = CurrentFilesList.SelectedIndex;
        Active.ViewMode = mode;
        ApplyViewMode();
        if (selected >= 0 && selected < _viewItems.Count) CurrentFilesList.SelectedIndex = selected;
    }
    private void DetailsView_Click(object? sender, RoutedEventArgs e) => SetViewMode("Details");
    private void IconView_Click(object? sender, RoutedEventArgs e) => SetViewMode("Icons");
    private void CompactView_Click(object? sender, RoutedEventArgs e) => SetViewMode("Compact");
    private void ViewSelection_Changed(object? sender, SelectionChangedEventArgs e) { }
    // Tunnel keyboard shortcuts so they work after changing tabs or clicking the background.
    // Do not hijack text editing inside TextBox controls.
    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is TextBox) return;
        // Delete in the sidebar removes only a custom shortcut, never its target.
        if (e.Key == Key.Delete && IsShortcutFocus(e.Source))
        {
            RemoveSelectedShortcut();
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.C) { await CaptureSelectionAsync(false); e.Handled = true; }
            else if (e.Key == Key.X) { await CaptureSelectionAsync(true); e.Handled = true; }
            else if (e.Key == Key.V) { await PasteAsync(); e.Handled = true; }
            else if (e.Key == Key.D1) { SetViewMode("Details"); e.Handled = true; }
            else if (e.Key == Key.D2) { SetViewMode("Icons"); e.Handled = true; }
            else if (e.Key == Key.D3) { SetViewMode("Compact"); e.Handled = true; }
            else if (e.Key == Key.T) { NewTab_Click(null, new RoutedEventArgs()); e.Handled = true; }
        }
        else if (e.Key == Key.Delete)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) await DeleteSelectedAsync(true);
            else await DeleteSelectedAsync(false);
            e.Handled = true;
        }
        else if (e.Key == Key.F2) { await RenameSelectedAsync(); e.Handled = true; }
    }
    private void Files_KeyDown(object? sender, KeyEventArgs e) { /* Handled by the window tunnel. */ }
    private async Task CaptureSelectionAsync(bool cut)
    {
        var source = SelectedEntry;
        if (source is null) return; // Do not discard a valid clipboard on empty selection.
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) { StatusText.Text = "System clipboard unavailable."; return; }
        try
        {
            var uri = new Uri(Path.GetFullPath(source));
            var data = new DataObject();
            // Clipboard is not drag-and-drop: Avalonia's DataFormats.Files can be
            // exported as an empty/unsupported GFile to GTK/Nemo. Publish only
            // well-formed URI text targets here. Dragging still uses Files.
            data.Set("text/uri-list", uri.AbsoluteUri + "\r\n");
            data.Set("x-special/gnome-copied-files", (cut ? "cut\n" : "copy\n") + uri.AbsoluteUri);
            await clipboard.SetDataObjectAsync(data);
            _clipboardPath = source;
            _clipboardCut = cut;
            StatusText.Text = $"{(cut ? "Cut" : "Copied")}: {source}";
        }
        catch (Exception ex) { StatusText.Text = $"Clipboard failed: {ex.Message}"; }
    }

    private async Task<(string[] Paths, bool Cut)> ReadClipboardFilesAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return (Array.Empty<string>(), false);
        var formats = await clipboard.GetFormatsAsync();
        var cut = false;
        var paths = new List<string>();
        if (formats.Contains("x-special/gnome-copied-files"))
        {
            var raw = DecodeClipboardText(await clipboard.GetDataAsync("x-special/gnome-copied-files"));
            if (raw is not null)
            {
                cut = raw.StartsWith("cut\n", StringComparison.OrdinalIgnoreCase);
                paths.AddRange(ParseFileUris(raw));
            }
        }
        if (paths.Count == 0 && formats.Contains("text/uri-list"))
        {
            var raw = DecodeClipboardText(await clipboard.GetDataAsync("text/uri-list"));
            if (raw is not null) paths.AddRange(ParseFileUris(raw));
        }
        if (paths.Count == 0 && formats.Contains(DataFormats.Files))
        {
            var files = await clipboard.GetDataAsync(DataFormats.Files) as IEnumerable<IStorageItem>;
            if (files is not null)
                paths.AddRange(files.Where(f => f.Path.IsFile).Select(f => f.Path.LocalPath));
        }
        if (paths.Count == 0 && _clipboardPath is { } ownPath && (File.Exists(ownPath) || Directory.Exists(ownPath)))
            return (new[] { ownPath }, _clipboardCut);
        return (paths.Distinct(StringComparer.Ordinal).ToArray(), cut);
    }

    // GTK clipboard targets can arrive as UTF-8 bytes rather than a managed string.
    private static string? DecodeClipboardText(object? data) => data switch
    {
        string text => text,
        byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0'),
        System.IO.MemoryStream stream => System.Text.Encoding.UTF8.GetString(stream.ToArray()).TrimEnd('\0'),
        _ => null
    };

    private static IEnumerable<string> ParseFileUris(string raw)
    {
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith('#') || line is "copy" or "cut") continue;
            if (Uri.TryCreate(line, UriKind.Absolute, out var uri) && uri.IsFile)
                yield return uri.LocalPath;
        }
    }

    private async Task PasteAsync()
    {
        if (Active is null) return;
        string[] sources;
        bool cut;
        try
        {
            (sources, cut) = await ReadClipboardFilesAsync();
        }
        catch (Exception ex) { StatusText.Text = $"Clipboard read failed: {ex.Message}"; return; }
        if (sources.Length == 0)
        {
            StatusText.Text = "Clipboard contains no files.";
            return;
        }
        var destination = Active.CurrentPath;
        var completed = 0;
        foreach (var source in sources)
        {
            var target = Path.Combine(destination, Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)));
            try
            {
                if (Path.GetFullPath(source) == Path.GetFullPath(target)) continue;
                var exists = File.Exists(target) || Directory.Exists(target);
                if (cut && _settings.ConfirmMove &&
                    !await ConfirmAsync("Move", $"Move '{Path.GetFileName(source)}' to '{destination}'?", "Move")) continue;
                if (exists && _settings.ConfirmOverwrite &&
                    !await ConfirmAsync("Overwrite", $"Replace existing '{target}'?", "Replace")) continue;
                if (cut) _operations.Move(source, destination, overwrite: exists);
                else _operations.Copy(source, destination, overwrite: exists);
                completed++;
                InvalidateTreeFor(source, target);
            }
            catch (Exception ex) { await ShowMessageAsync("Paste failed", ex.Message); }
        }
        if (completed > 0)
        {
            RefreshFiles();
            StatusText.Text = $"{(cut ? "Moved" : "Copied")} {completed} item(s) to {destination}";
            if (cut && completed == sources.Length)
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null) await clipboard.ClearAsync();
                _clipboardPath = null;
                _clipboardCut = false;
            }
        }
    }
    private async Task DeleteSelectedAsync(bool permanent)
    {
        var path = SelectedEntry;
        if (path is null) return;
        if (permanent)
        {
            if (_settings.ConfirmPermanentDelete &&
                !await ConfirmAsync("Delete permanently", $"Permanently delete '{Path.GetFileName(path)}'? This cannot be undone.", "Delete")) return;
            try { _operations.Delete(path); RefreshFiles(); InvalidateTreeFor(path); }
            catch (Exception ex) { await ShowMessageAsync("Delete failed", ex.Message); }
        }
        else
        {
            if (_settings.ConfirmTrash &&
                !await ConfirmAsync("Move to Trash", $"Move '{Path.GetFileName(path)}' to Trash?", "Trash")) return;
            try { await TrashService.MoveToTrashAsync(path); RefreshFiles(); InvalidateTreeFor(path); }
            catch (Exception ex) { await ShowMessageAsync("Trash failed", ex.Message); }
        }
    }
    private async Task RenameSelectedAsync()
    {
        var path = SelectedEntry;
        if (path is null) return;
        var nameBox = new TextBox { Text = Path.GetFileName(path), Width = 330 };
        var dialog = new Window { Title = "Rename", Width = 400, Height = 165, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var ok = new Button { Content = "Rename" };
        var cancel = new Button { Content = "Cancel" };
        ok.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = { nameBox, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } } } };
        if (!await dialog.ShowDialog<bool>(this)) return;
        try { _operations.Rename(path, nameBox.Text ?? ""); RefreshFiles(); InvalidateTreeFor(path, Path.Combine(Path.GetDirectoryName(path)!, nameBox.Text ?? "")); }
        catch (Exception ex) { await ShowMessageAsync("Rename failed", ex.Message); }
    }
    private async Task<bool> ConfirmAsync(string title, string message, string action = "OK")
    {
        var dialog = new Window { Title = title, Width = 450, Height = 175, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Content = action };
        var no = new Button { Content = "Cancel" };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { yes, no } } } };
        return await dialog.ShowDialog<bool>(this);
    }
    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window { Title = title, Width = 470, Height = 160, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var ok = new Button { Content = "OK" };
        ok.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, ok } };
        await dialog.ShowDialog(this);
    }
    // Avalonia's standard Files data format carries file:// URIs across Linux applications.
    // No shell command or application-specific data format is required.
    private async void Files_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging || _dragStart is null || sender is not ListBox list || !ReferenceEquals(list, _dragSource)) return;
        if (!e.GetCurrentPoint(list).Properties.IsLeftButtonPressed) { _dragStart = null; return; }
        var p = e.GetPosition(list);
        if (Math.Abs(p.X - _dragStart.Value.X) < 7 && Math.Abs(p.Y - _dragStart.Value.Y) < 7) return;
        _dragStart = null;
        // Selection can change during pointer movement; keep the item that was pressed.
        var selected = list.SelectedItems?.OfType<FileViewItem>().Select(i => i.Entry.FullPath).Distinct().ToArray()
            ?? Array.Empty<string>();
        if (_pressedDragItem is { } pressed && !selected.Contains(pressed.Entry.FullPath))
            selected = new[] { pressed.Entry.FullPath };
        if (selected.Length == 0 && SelectedEntry is { } path) selected = new[] { path };
        if (selected.Length == 0) return;
        _dragging = true;
        try
        {
            var items = new List<IStorageItem>();
            foreach (var sourcePath in selected)
            {
                var uri = new Uri(Path.GetFullPath(sourcePath));
                IStorageItem? item = Directory.Exists(sourcePath)
                    ? await StorageProvider.TryGetFolderFromPathAsync(uri)
                    : await StorageProvider.TryGetFileFromPathAsync(uri);
                if (item is not null) items.Add(item);
            }
            if (items.Count == 0) return;
            var data = new DataObject();
            data.Set(DataFormats.Files, items.ToArray());
            // GTK/Nemo interoperates with URI lists; provide them in addition to
            // Avalonia's native file format for cross-application drags.
            var uriList = string.Join("\r\n", selected.Select(path => new Uri(Path.GetFullPath(path)).AbsoluteUri)) + "\r\n";
            data.Set("text/uri-list", uriList);
            data.Set("x-special/gnome-copied-files", "copy\n" + string.Join("\n", selected.Select(path => new Uri(Path.GetFullPath(path)).AbsoluteUri)));
            data.Set("application/x-wa1gonfm-files", "1");
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
        catch (Exception ex) { StatusText.Text = $"Drag failed: {ex.Message}"; }
        finally { _dragging = false; _hoverTab = null; _pressedDragItem = null; }
    }

    private void Files_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStart = null;
        _pressedDragItem = null;
    }

    private void Files_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = GetDroppedPaths(e).Length > 0 ? ChooseDropEffect(e) : DragDropEffects.None;
    }

    private static string[] GetDroppedPaths(DragEventArgs e)
    {
        var native = e.Data.GetFiles()?.Select(file => file.Path.LocalPath)
            .Where(path => !string.IsNullOrWhiteSpace(path)) ?? Enumerable.Empty<string>();
        var paths = new List<string>(native);
        foreach (var format in new[] { "text/uri-list", "x-special/gnome-copied-files" })
        {
            if (e.Data.Get(format) is not string raw) continue;
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith('#') || line is "copy" or "cut") continue;
                if (Uri.TryCreate(line, UriKind.Absolute, out var uri) && uri.IsFile)
                    paths.Add(uri.LocalPath);
            }
        }
        return paths.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static DragDropEffects ChooseDropEffect(DragEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) return DragDropEffects.Copy;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return DragDropEffects.Move;
        // WA1GONFM drags default to move; external drags default to copy.
        return e.Data.Get("application/x-wa1gonfm-files") is not null
            ? DragDropEffects.Move : DragDropEffects.Copy;
    }

    private async void Files_Drop(object? sender, DragEventArgs e)
    {
        if (Active is null) return;
        await ImportDropAsync(e, Active.CurrentPath);
    }

    private async Task ImportDropAsync(DragEventArgs e, string destination)
    {
        var paths = GetDroppedPaths(e);
        if (paths.Length == 0) return;
        var move = ChooseDropEffect(e) == DragDropEffects.Move;
        foreach (var source in paths)
        {
            var target = Path.Combine(destination, Path.GetFileName(source.TrimEnd('/')));
            if (Path.GetFullPath(source) == Path.GetFullPath(target)) continue;
            var exists = File.Exists(target) || Directory.Exists(target);
            if (move && _settings.ConfirmMove &&
                !await ConfirmAsync("Move", $"Move '{Path.GetFileName(source)}' to '{destination}'?", "Move")) continue;
            if (exists && _settings.ConfirmOverwrite &&
                !await ConfirmAsync("Overwrite", $"Replace '{target}'?", "Replace")) continue;
            try
            {
                if (move)
                {
                    try { _operations.Move(source, destination, overwrite: exists); }
                    catch (IOException ex) when (!exists && (ex.HResult & 0xffff) == 18)
                    {
                        // Directory.Move/File.Move may fail with EXDEV across filesystems.
                        // Copy first; delete the source only after the copy completes.
                        _operations.Copy(source, destination);
                        _operations.Delete(source);
                    }
                }
                else _operations.Copy(source, destination, overwrite: exists);
                InvalidateTreeFor(source, target);
            }
            catch (Exception ex) { await ShowMessageAsync("Drop failed", $"{source}\n{ex.Message}"); }
        }
        RefreshFiles();
    }

    // A dragged file can be dropped on a tab header. Hovering also activates the tab
    // after a short delay, allowing a subsequent drop into its file view.
    private void Tabs_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = GetDroppedPaths(e).Length > 0 ? ChooseDropEffect(e) : DragDropEffects.None;
        var tab = (e.Source as Avalonia.Visual)?.FindAncestorOfType<TabItem>();
        if (tab is null) return;
        if (!ReferenceEquals(tab, _hoverTab)) { _hoverTab = tab; _hoverTabSince = DateTime.UtcNow; }
        else if ((DateTime.UtcNow - _hoverTabSince).TotalMilliseconds > 450 && !tab.IsSelected)
            Tabs.SelectedItem = tab;
    }

    private async void Tabs_Drop(object? sender, DragEventArgs e)
    {
        var tab = (e.Source as Avalonia.Visual)?.FindAncestorOfType<TabItem>();
        if (tab is null) return;
        var index = Tabs.Items.Cast<object>().ToList().IndexOf(tab);
        if (index < 0 || index >= _tabs.Count) return;
        Tabs.SelectedIndex = index;
        await ImportDropAsync(e, _tabs[index].CurrentPath);
    }

    private void Files_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var list = sender as ListBox ?? CurrentFilesList;
        var point = e.GetCurrentPoint(list);
        if (point.Properties.IsLeftButtonPressed)
        {
            _dragStart = e.GetPosition(list);
            _dragSource = list;
            _pressedDragItem = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext as FileViewItem;
            if (_pressedDragItem is null) _dragStart = null;
            return;
        }
        if (!point.Properties.IsRightButtonPressed) return;
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item is not null) list.SelectedItem = item.DataContext;
        else list.SelectedIndex = -1;
    }
    private async void OpenSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedEntry is { } path) await OpenPathAsync(path);
    }
    private async Task OpenPathAsync(string path)
    {
        if (Directory.Exists(path)) { Navigate(path); return; }
        try { _launcher.Open(path); }
        catch (NotSupportedException) { await ShowOpenWithDialogAsync(path); }
        catch (Exception ex) { await ShowMessageAsync("Open failed", ex.Message); }
    }
    private async void OpenWith_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedEntry is { } path && File.Exists(path)) await ShowOpenWithDialogAsync(path);
    }
    private async Task ShowOpenWithDialogAsync(string path)
    {
        var appBox = new TextBox { Watermark = "Application executable (e.g. xed, code, /usr/bin/evince)", Width = 430 };
        var dialog = new Window { Title = $"Open With: {Path.GetFileName(path)}", Width = 520, Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var open = new Button { Content = "Open With" };
        var defaultApp = new Button { Content = "Use Default" };
        var cancel = new Button { Content = "Cancel" };
        open.Click += (_, _) => dialog.Close(1);
        defaultApp.Click += (_, _) => dialog.Close(2);
        cancel.Click += (_, _) => dialog.Close(0);
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 12,
            Children = { new TextBlock { Text = "Choose a program to open this file. The file is passed as one argument." },
                appBox, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
                    Children = { open, defaultApp, cancel } } } };
        var choice = await dialog.ShowDialog<int>(this);
        if (choice == 0) return;
        try
        {
            if (choice == 2) _launcher.OpenWith("xdg-open", path);
            else _launcher.OpenWith(appBox.Text ?? "", path);
        }
        catch (Exception ex) { await ShowMessageAsync("Open With failed", ex.Message); }
    }
    private async void Cut_Click(object? sender, RoutedEventArgs e) => await CaptureSelectionAsync(true);
    private async void Copy_Click(object? sender, RoutedEventArgs e) => await CaptureSelectionAsync(false);
    private async void Paste_Click(object? sender, RoutedEventArgs e) => await PasteAsync();
    private async void Rename_Click(object? sender, RoutedEventArgs e) => await RenameSelectedAsync();
    private async void Trash_Click(object? sender, RoutedEventArgs e) => await DeleteSelectedAsync(false);
    private async void PermanentDelete_Click(object? sender, RoutedEventArgs e) => await DeleteSelectedAsync(true);
    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        RefreshFiles();
        if (Active is not null) InvalidateTreeFor(Active.CurrentPath);
    }
    private async void Properties_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedEntry is not { } path) return;
        try
        {
            var info = new FileInfo(path);
            var directory = Directory.Exists(path);
            var details = $"Path: {path}\nType: {(directory ? "Directory" : "File")}\n" +
                (directory ? "" : $"Size: {info.Length:N0} bytes\n") +
                $"Modified: {(directory ? Directory.GetLastWriteTime(path) : info.LastWriteTime)}";
            await ShowMessageAsync("Properties", details);
        }
        catch (Exception ex) { await ShowMessageAsync("Properties", ex.Message); }
    }
    private async void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (Active is null) return;
        var input = new TextBox { Watermark = "Folder name", Width = 320 };
        var dialog = new Window { Title = "New folder", Width = 400, Height = 160, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var create = new Button { Content = "Create" };
        var cancel = new Button { Content = "Cancel" };
        create.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        input.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) { args.Handled = true; dialog.Close(true); }
            else if (args.Key == Key.Escape) { args.Handled = true; dialog.Close(false); }
        };
        dialog.Opened += (_, _) => input.Focus();
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10,
            Children = { input, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { create, cancel } } } };
        if (!await dialog.ShowDialog<bool>(this)) return;
        var name = input.Text;
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Contains('/') || name.Contains('\\')) return;
        try { Directory.CreateDirectory(Path.Combine(Active.CurrentPath, name)); RefreshFiles(); InvalidateTreeFor(Active.CurrentPath); }
        catch (Exception ex) { await ShowMessageAsync("New folder failed", ex.Message); }
    }
    private async void Files_Tapped(object? sender, TappedEventArgs e)
    {
        if (_settings.ClickMode != "Single" || _dragging) return;
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is FileViewItem entry) await OpenPathAsync(entry.Entry.FullPath);
    }
    private async void Files_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_settings.ClickMode != "Double") return;
        if ((e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext is FileViewItem entry)
            await OpenPathAsync(entry.Entry.FullPath);
    }
    private void Tree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initialized && DirectoryTree.SelectedItem is DirectoryTreeNode { IsReal: true } node) Navigate(node.Path);
    }
    private async void Tree_Expanded(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not TreeViewItem { DataContext: DirectoryTreeNode { IsReal: true } node }) return;
        // Expansion, not selection, triggers enumeration.
        await node.LoadChildrenAsync();
    }

    private void InvalidateTreeFor(params string[] paths)
    {
        foreach (var path in paths)
        {
            var parent = System.IO.Path.GetDirectoryName(path.TrimEnd('/'));
            if (string.IsNullOrEmpty(parent)) parent = "/";
            var node = _treeRoot.FindLoaded(path);
            node?.Invalidate();
            var parentNode = _treeRoot.FindLoaded(parent);
            parentNode?.Invalidate();
            // Refresh only expanded nodes. Collapsed nodes reload on their next expansion.
            if (parentNode is not null && IsNodeExpanded(parentNode))
                _ = parentNode.LoadChildrenAsync();
            if (node is not null && IsNodeExpanded(node))
                _ = node.LoadChildrenAsync();
        }
    }

    private bool IsNodeExpanded(DirectoryTreeNode node)
    {
        return DirectoryTree.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .Any(item => ReferenceEquals(item.DataContext, node) && item.IsExpanded);
    }

    private void RefreshShortcuts()
    {
        var defaults = new[] { Home, Path.Combine(Home, "Downloads"), Path.Combine(Home, "Documents") };
        _shortcuts = defaults.Concat(_settings.Shortcuts).Where(Directory.Exists).Distinct().ToArray();
        ShortcutsList.ItemsSource = _shortcuts;
    }
    private string[] _shortcuts = Array.Empty<string>();
    private void Shortcut_Tapped(object? sender, TappedEventArgs e)
    {
        if (_settings.ClickMode != "Single") return;
        if ((e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext is string path)
            Navigate(path);
    }
    private void Shortcut_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_settings.ClickMode == "Double" && ShortcutsList.SelectedIndex is var i && i >= 0 && i < _shortcuts.Length) Navigate(_shortcuts[i]);
    }
    private bool IsShortcutFocus(object? source) =>
        source is Avalonia.Visual visual &&
        (ReferenceEquals(visual, ShortcutsList) ||
         ReferenceEquals(visual.FindAncestorOfType<ListBox>(), ShortcutsList));

    private void Shortcut_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ShortcutsList).Properties.IsRightButtonPressed) return;
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>();
        ShortcutsList.SelectedItem = item?.DataContext;
    }

    private void RemoveShortcut_Click(object? sender, RoutedEventArgs e) => RemoveSelectedShortcut();

    private void RemoveSelectedShortcut()
    {
        if (ShortcutsList.SelectedItem is not string path) return;
        if (!ShortcutService.Remove(_settings, path, Home))
        {
            StatusText.Text = "Built-in shortcuts cannot be removed.";
            return;
        }
        _store.Save(_settings);
        RefreshShortcuts();
        StatusText.Text = $"Removed shortcut: {path}";
    }

    private void AddShortcut_Click(object? sender, RoutedEventArgs e)
    {
        if (Active is null || _settings.Shortcuts.Contains(Active.CurrentPath)) return;
        _settings.Shortcuts.Add(Active.CurrentPath);
        _store.Save(_settings);
        RefreshShortcuts();
    }
    private void PathBox_KeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter && PathBox.Text is { } path) Navigate(path); }
    private void Terminal_Click(object? sender, RoutedEventArgs e)
    {
        try { _terminal.Launch(Active?.CurrentPath ?? Home, _settings); }
        catch (Exception ex) { StatusText.Text = $"Terminal: {ex.Message}"; }
    }
    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, _store);
        await dialog.ShowDialog(this);
    }
}
