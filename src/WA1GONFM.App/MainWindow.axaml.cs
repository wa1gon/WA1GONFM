using System.Threading;
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
    private CancellationTokenSource? _integrityScan;
    private FileSystemWatcher? _directoryWatcher;
    private string? _watchedDirectory;
    private readonly Avalonia.Threading.DispatcherTimer _directoryRefreshTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(350)
    };
    private bool _directoryRefreshPending;
    private bool _directoryStructureChanged;
    private readonly HashSet<string> _modifiedPaths = new(StringComparer.Ordinal);
    private DateTime _lastRightClickUtc = DateTime.MinValue;

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
    private string[] _clipboardPaths = Array.Empty<string>();
    private bool _clipboardCut;
    private readonly AppSettings _settings;
    private readonly ObservableCollection<NavigationTab> _tabs = new();
    private readonly DirectoryTreeNode _treeRoot = new("/");
    private Avalonia.Point? _dragStart;
    private PointerPressedEventArgs? _dragPressEvent;
    private ListBox? _dragSource;
    private FileViewItem? _pressedDragItem;
    private bool _dragging;
    private bool _suppressSingleClickOpen;
    private bool _sortUpdating;
    private readonly string _windowDragId = Guid.NewGuid().ToString("N");
    private TabItem? _hoverTab;
    private DateTime _hoverTabSince;
    private bool _refreshing;
    private bool _initialized;
    private string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private NavigationTab? Active => Tabs.SelectedIndex >= 0 && Tabs.SelectedIndex < _tabs.Count ? _tabs[Tabs.SelectedIndex] : null;
    public MainWindow() : this(null) { }

    public MainWindow(string? startupDirectory)
    {
        _settings = _store.Load();
        InitializeComponent();
        var appVersion = typeof(MainWindow).Assembly.GetName().Version;
        Title = appVersion is null
            ? "filepilot"
            : $"filepilot v{appVersion.Major}.{appVersion.Minor}.{appVersion.Build}";
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
        {
            fileList.AddHandler(PointerMovedEvent, Files_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            fileList.AddHandler(PointerPressedEvent, Files_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        // Handle wheel input consistently in the file views and sidebar, including
        // the icon WrapPanel, whose default scrolling behavior can vary by theme.
        foreach (var pane in new Avalonia.Controls.Control[] { FilesList, IconFilesList, CompactFilesList, DirectoryTree, ShortcutsList })
            pane.AddHandler(PointerWheelChangedEvent, Pane_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        SortBy.ItemsSource = new[] { "Name", "Size", "Type", "Extension", "Date modified" };
        SortBy.SelectedItem = _settings.SortBy;
        SortDescending.IsChecked = _settings.SortDescending;
        FoldersFirst.IsChecked = _settings.FoldersFirst;
        _directoryRefreshTimer.Tick += (_, _) =>
        {
            _directoryRefreshTimer.Stop();
            if (!_directoryRefreshPending || !_settings.AutoRefresh) return;
            // Never recycle rows underneath an open context menu or active drag.
            if (_dragging || _dragStart is not null ||
                DateTime.UtcNow - _lastRightClickUtc < TimeSpan.FromSeconds(3))
            {
                _directoryRefreshTimer.Start();
                return;
            }
            _directoryRefreshPending = false;
            if (_directoryStructureChanged)
            {
                _directoryStructureChanged = false;
                _modifiedPaths.Clear();
                RefreshFilesPreservingState();
            }
            else
            {
                var paths = _modifiedPaths.ToArray();
                _modifiedPaths.Clear();
                foreach (var path in paths) UpdateModifiedFile(path);
            }
        };
        Closed += (_, _) =>
        {
            _directoryRefreshTimer.Stop();
            _directoryWatcher?.Dispose();
            _integrityScan?.Cancel();
        };
        RefreshShortcuts();
        _tabs.Add(new NavigationTab(startupDirectory ?? Home) { ViewMode = _settings.DefaultViewMode });
        _initialized = true;
        RefreshTabs(0);
        AddHandler(KeyDownEvent, Window_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // Diagnostic hooks run even when a child control handles the drag event.
        AddHandler(DragDrop.DragEnterEvent, DragDiagnostics.OnEnter, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, DragDiagnostics.OnOver, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, DragDiagnostics.OnLeave, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, DragDiagnostics.OnDrop, RoutingStrategies.Tunnel, handledEventsToo: true);
        // Register the native top-level drop target explicitly as well as the individual views.
        DragDrop.SetAllowDrop(this, true);
        DragDiagnostics.Log("Initialized drag event diagnostics on main window");
    }
    // Keep both expanded panels equally sized by default. Collapsed headers take
    // only their natural height, allowing the other panel to use the remaining space.
    // The splitter remains draggable while both panels are expanded.
    private void SidebarPanel_Expanded(object? sender, RoutedEventArgs e) => UpdateSidebarPanelLayout();
    private void SidebarPanel_Collapsed(object? sender, RoutedEventArgs e) => UpdateSidebarPanelLayout();

    private void UpdateSidebarPanelLayout()
    {
        // Expansion events may fire before InitializeComponent completes.
        if (SidebarPanels is null || ShortcutsPanel is null || DirectoriesPanel is null || SidebarSplitter is null)
            return;

        var shortcutsOpen = ShortcutsPanel.IsExpanded;
        var directoriesOpen = DirectoriesPanel.IsExpanded;
        var shortcutsRow = SidebarPanels.RowDefinitions[0];
        var dividerRow = SidebarPanels.RowDefinitions[1];
        var directoriesRow = SidebarPanels.RowDefinitions[2];

        // Give each panel a true half-height when both are expanded. When a
        // panel collapses, use Auto for its header so no empty region remains.
        shortcutsRow.Height = shortcutsOpen ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        directoriesRow.Height = directoriesOpen ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        dividerRow.Height = shortcutsOpen && directoriesOpen ? new GridLength(6) : new GridLength(0);
        SidebarSplitter.IsVisible = shortcutsOpen && directoriesOpen;
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
        _integrityScan?.Cancel();
        _integrityScan = new CancellationTokenSource();
        if (Active is null) return;
        try
        {
            PathBox.Text = Active.CurrentPath;
            var entries = _browser.List(Active.CurrentPath, _settings.ShowHidden, _settings.ShowDirectories);
            var sorted = SortEntries(entries);
            _entries = sorted;
            ConfigureDirectoryWatcher();
            _viewItems.Clear();
            foreach (var entry in sorted) _viewItems.Add(new FileViewItem(entry));
            _ = ScanIntegrityAsync(_viewItems.ToArray(), _integrityScan.Token);
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
    private void ConfigureDirectoryWatcher()
    {
        var path = _settings.AutoRefresh ? Active?.CurrentPath : null;
        if (string.Equals(path, _watchedDirectory, StringComparison.Ordinal) &&
            (_directoryWatcher is not null || path is null)) return;
        _directoryRefreshTimer.Stop();
        _directoryRefreshPending = false;
        _directoryStructureChanged = false;
        _modifiedPaths.Clear();
        _directoryWatcher?.Dispose();
        _directoryWatcher = null;
        _watchedDirectory = null;
        if (path is null || !Directory.Exists(path)) return;
        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false
            };
            watcher.Changed += OnDirectoryChanged;
            watcher.Created += OnDirectoryChanged;
            watcher.Deleted += OnDirectoryChanged;
            watcher.Renamed += OnDirectoryRenamed;
            watcher.Error += OnDirectoryWatcherError;
            watcher.EnableRaisingEvents = true;
            _directoryWatcher = watcher;
            _watchedDirectory = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            System.Diagnostics.Debug.WriteLine($"Directory watcher unavailable: {ex.Message}");
        }
    }

    private void OnDirectoryChanged(object sender, FileSystemEventArgs e) =>
        QueueDirectoryRefresh(sender, e.FullPath, e.ChangeType != WatcherChangeTypes.Changed);

    private void OnDirectoryRenamed(object sender, RenamedEventArgs e) =>
        QueueDirectoryRefresh(sender, e.FullPath, true);

    private void OnDirectoryWatcherError(object sender, ErrorEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _directoryWatcher)) return;
            _directoryWatcher?.Dispose();
            _directoryWatcher = null;
            _watchedDirectory = null;
            _directoryRefreshPending = true;
            _directoryStructureChanged = true;
            _directoryRefreshTimer.Stop();
            _directoryRefreshTimer.Start();
        });
    }

    private void QueueDirectoryRefresh(object sender, string path, bool structureChanged)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _directoryWatcher) || !_settings.AutoRefresh) return;
            _directoryRefreshPending = true;
            if (structureChanged) _directoryStructureChanged = true;
            else _modifiedPaths.Add(path);
            _directoryRefreshTimer.Stop();
            _directoryRefreshTimer.Start();
        });
    }

    private void UpdateModifiedFile(string path)
    {
        // A change to existing content should not replace the file list or its selection.
        // Only this row's metadata and content-derived type need to be refreshed.
        var item = _viewItems.FirstOrDefault(i =>
            string.Equals(i.Entry.FullPath, path, StringComparison.Ordinal));
        if (item is null || !File.Exists(path)) return;
        item.RefreshMetadata();
        var token = _integrityScan?.Token ?? CancellationToken.None;
        _ = Task.Run(() => FileTypeIntegrity.Check(path), token).ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully && !token.IsCancellationRequested &&
                _viewItems.Contains(item)) item.ApplyIntegrity(task.Result);
        }, CancellationToken.None, TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void RefreshFilesPreservingState()
    {
        var selectedPaths = SelectedEntries.ToHashSet(StringComparer.Ordinal);
        var scrolls = new[] { FilesList, IconFilesList, CompactFilesList }
            .Select(list => list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault())
            .ToArray();
        var offsets = scrolls.Select(scroll => scroll?.Offset).ToArray();
        RefreshFiles();
        CurrentFilesList.SelectedItems?.Clear();
        foreach (var item in _viewItems.Where(item => selectedPaths.Contains(item.Entry.FullPath)))
            CurrentFilesList.SelectedItems?.Add(item);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            for (var i = 0; i < scrolls.Length; i++)
                if (scrolls[i] is not null && offsets[i] is { } offset) scrolls[i]!.Offset = offset;
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void SetAutoRefresh(bool enabled)
    {
        _settings.AutoRefresh = enabled;
        _store.Save(_settings);
        ConfigureDirectoryWatcher();
        if (enabled) RefreshFilesPreservingState();
    }

    private static async System.Threading.Tasks.Task ScanIntegrityAsync(FileViewItem[] items, CancellationToken token)
    {
        // Bound concurrency and skip folders and symlinks. No file is executed.
        using var limit = new SemaphoreSlim(2);
        var tasks = items.Where(i => !i.Entry.IsDirectory).Select(async item =>
        {
            try
            {
                await limit.WaitAsync(token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    var result = await System.Threading.Tasks.Task.Run(() => FileTypeIntegrity.Check(item.Entry.FullPath), token);
                    if (!token.IsCancellationRequested)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (!token.IsCancellationRequested) item.ApplyIntegrity(result); });
                }
                finally { limit.Release(); }
            }
            catch (OperationCanceledException) { }
        });
        await System.Threading.Tasks.Task.WhenAll(tasks);
    }

    private IReadOnlyList<DirectoryEntry> SortEntries(IReadOnlyList<DirectoryEntry> entries)
    {
        // File metadata can disappear during enumeration, so treat inaccessible items as zero/oldest.
        long Size(DirectoryEntry entry)
        {
            if (entry.IsDirectory) return 0;
            try { return new FileInfo(entry.FullPath).Length; } catch { return 0; }
        }
        DateTime Modified(DirectoryEntry entry)
        {
            try { return File.GetLastWriteTimeUtc(entry.FullPath); } catch { return DateTime.MinValue; }
        }
        string Type(DirectoryEntry entry) => entry.IsDirectory ? "" : FilenameExtension.GetExtension(entry.Name);
        string Extension(DirectoryEntry entry) => entry.IsDirectory ? "" : FilenameExtension.GetExtension(entry.Name).TrimStart('.');
        IOrderedEnumerable<DirectoryEntry> sorted = _settings.FoldersFirst
            ? entries.OrderBy(entry => entry.IsDirectory ? 0 : 1)
            : entries.OrderBy(_ => 0);
        var descending = _settings.SortDescending;
        sorted = _settings.SortBy switch
        {
            "Size" => descending ? sorted.ThenByDescending(Size) : sorted.ThenBy(Size),
            "Type" => descending ? sorted.ThenByDescending(Type, StringComparer.OrdinalIgnoreCase) : sorted.ThenBy(Type, StringComparer.OrdinalIgnoreCase),
            "Extension" => descending ? sorted.ThenByDescending(Extension, StringComparer.OrdinalIgnoreCase) : sorted.ThenBy(Extension, StringComparer.OrdinalIgnoreCase),
            "Date modified" => descending ? sorted.ThenByDescending(Modified) : sorted.ThenBy(Modified),
            _ => descending ? sorted.ThenByDescending(entry => entry.Name, StringComparer.OrdinalIgnoreCase) : sorted.ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        };
        return sorted.ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void Sort_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_initialized || _sortUpdating) return;
        _settings.SortBy = SortBy.SelectedItem as string ?? "Name";
        _settings.SortDescending = SortDescending.IsChecked == true;
        _settings.FoldersFirst = FoldersFirst.IsChecked == true;
        _store.Save(_settings);
        RefreshFiles();
    }

    private void SortHeader_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string column }) return;
        _sortUpdating = true;
        if (_settings.SortBy == column) _settings.SortDescending = !_settings.SortDescending;
        else { _settings.SortBy = column; _settings.SortDescending = false; }
        SortBy.SelectedItem = _settings.SortBy;
        SortDescending.IsChecked = _settings.SortDescending;
        _sortUpdating = false;
        _store.Save(_settings);
        RefreshFiles();
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
    private string[] SelectedEntries => CurrentFilesList.SelectedItems?
        .OfType<FileViewItem>()
        .Select(item => item.Entry.FullPath)
        .Distinct(StringComparer.Ordinal)
        .ToArray() ?? Array.Empty<string>();
    private string? SelectedEntry => CurrentFilesList.SelectedIndex is var i && i >= 0 && i < _entries.Count ? _entries[i].FullPath : null;
    private void ApplyViewMode()
    {
        var mode = Active?.ViewMode ?? _settings.DefaultViewMode;
        DetailsContainer.IsVisible = mode == "Details";
        IconFilesList.IsVisible = mode == "Icons";
        CompactFilesList.IsVisible = mode == "Compact";
        ViewModeLabel.Text = mode;
        if (mode == "Icons")
            foreach (var item in _viewItems) item.LoadThumbnail();
    }
    private void SetViewMode(string mode)
    {
        if (Active is null) return;
        var selected = SelectedEntries.ToHashSet(StringComparer.Ordinal);
        Active.ViewMode = mode;
        ApplyViewMode();
        var list = CurrentFilesList;
        list.SelectedItems?.Clear();
        foreach (var item in _viewItems.Where(item => selected.Contains(item.Entry.FullPath)))
            list.SelectedItems?.Add(item);
    }
    private void DetailsView_Click(object? sender, RoutedEventArgs e) => SetViewMode("Details");
    private void IconView_Click(object? sender, RoutedEventArgs e) => SetViewMode("Icons");
    private void CompactView_Click(object? sender, RoutedEventArgs e) => SetViewMode("Compact");
    private void ViewSelection_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list || !ReferenceEquals(list, CurrentFilesList) || Active is null) return;
        var count = SelectedEntries.Length;
        StatusText.Text = count == 0
            ? $"{_entries.Count} items  •  {Active.CurrentPath}"
            : $"{count} item(s) selected  •  {Active.CurrentPath}";
    }
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
            else if (e.Key == Key.A && (ReferenceEquals(e.Source, CurrentFilesList) ||
                         (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBox>() == CurrentFilesList))
            {
                CurrentFilesList.SelectAll();
                e.Handled = true;
            }
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
    private static readonly DataFormat<string> UriListFormat =
        DataFormat.CreateStringPlatformFormat("text/uri-list");
    private static readonly DataFormat<string> GnomeCopiedFilesFormat =
        DataFormat.CreateStringPlatformFormat("x-special/gnome-copied-files");

    private async Task CaptureSelectionAsync(bool cut)
    {
        var sources = SelectedEntries;
        if (sources.Length == 0) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) { StatusText.Text = "System clipboard unavailable."; return; }
        try
        {
            var uris = sources.Select(path => new Uri(Path.GetFullPath(path)).AbsoluteUri).ToArray();
            var item = new DataTransferItem();
            item.Set(UriListFormat, string.Join("\r\n", uris) + "\r\n");
            item.Set(GnomeCopiedFilesFormat, (cut ? "cut\n" : "copy\n") + string.Join("\n", uris));
            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
            _clipboardPaths = sources;
            _clipboardCut = cut;
            StatusText.Text = $"{(cut ? "Cut" : "Copied")} {sources.Length} item(s)";
        }
        catch (Exception ex) { StatusText.Text = $"Clipboard failed: {ex.Message}"; }
    }

    private async Task<(string[] Paths, bool Cut)> ReadClipboardFilesAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return (Array.Empty<string>(), false);
        var paths = new List<string>();
        var cut = false;
        // The received transfer must be disposed after reading its values.
        using (var transfer = await clipboard.TryGetDataAsync())
        {
            if (transfer is not null)
            {
                foreach (var item in transfer.Items)
                {
                    var raw = DecodeClipboardText(await item.TryGetRawAsync(GnomeCopiedFilesFormat));
                    if (raw is not null)
                    {
                        cut |= raw.StartsWith("cut\n", StringComparison.OrdinalIgnoreCase);
                        paths.AddRange(ParseFileUris(raw));
                    }
                }
                if (paths.Count == 0)
                {
                    foreach (var item in transfer.Items)
                    {
                        var raw = DecodeClipboardText(await item.TryGetRawAsync(UriListFormat));
                        if (raw is not null) paths.AddRange(ParseFileUris(raw));
                    }
                }
                if (paths.Count == 0)
                {
                    var files = await transfer.TryGetFilesAsync();
                    paths.AddRange((files ?? Array.Empty<IStorageItem>()).Where(f => f.Path.IsFile).Select(f => f.Path.LocalPath));
                }
            }
        }
        if (paths.Count == 0 && _clipboardPaths.Length > 0)
            return (_clipboardPaths.Where(path => File.Exists(path) || Directory.Exists(path)).ToArray(), _clipboardCut);
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
                _clipboardPaths = Array.Empty<string>();
                _clipboardCut = false;
            }
        }
    }
    private async Task DeleteSelectedAsync(bool permanent)
    {
        var paths = SelectedEntries;
        if (paths.Length == 0) return;
        var title = permanent ? "Delete permanently" : "Move to Trash";
        var action = permanent ? "Delete" : "Trash";
        var confirmationEnabled = permanent ? _settings.ConfirmPermanentDelete : _settings.ConfirmTrash;
        if (confirmationEnabled)
        {
            var message = permanent
                ? $"Permanently delete {paths.Length} selected item(s)? This cannot be undone."
                : $"Move {paths.Length} selected item(s) to Trash?";
            if (!await ConfirmAsync(title, message, action)) return;
        }
        var completed = 0;
        var failures = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                if (permanent) _operations.Delete(path);
                else await TrashService.MoveToTrashAsync(path);
                completed++;
                InvalidateTreeFor(path);
            }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
        }
        RefreshFiles();
        StatusText.Text = $"{(permanent ? "Deleted" : "Trashed")} {completed} of {paths.Length} item(s)";
        if (failures.Count > 0)
            await ShowMessageAsync($"{title}: {failures.Count} failed", string.Join("\n", failures));
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
        if (!e.GetCurrentPoint(list).Properties.IsLeftButtonPressed) { _dragStart = null; _dragPressEvent = null; return; }
        var p = e.GetPosition(list);
        if (Math.Abs(p.X - _dragStart.Value.X) < 7 && Math.Abs(p.Y - _dragStart.Value.Y) < 7) return;
        var pressEvent = _dragPressEvent;
        _dragStart = null;
        _dragPressEvent = null;
        if (pressEvent is null) return;
        // Selection can change during pointer movement; keep the item that was pressed.
        var selected = list.SelectedItems?.OfType<FileViewItem>().Select(i => i.Entry.FullPath).Distinct().ToArray()
            ?? Array.Empty<string>();
        if (_pressedDragItem is { } pressed && !selected.Contains(pressed.Entry.FullPath))
            selected = new[] { pressed.Entry.FullPath };
        if (selected.Length == 0 && SelectedEntry is { } path) selected = new[] { path };
        if (selected.Length == 0) { DragDiagnostics.Log("Drag source: no selected files"); return; }
        DragDiagnostics.Log($"Drag source: pointer threshold reached, selected={selected.Length}; paths={string.Join(" | ", selected)}");
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
            DragDiagnostics.Log($"Drag source: storage provider resolved {items.Count}/{selected.Length} items");
            if (items.Count == 0) return;
            // Avalonia 12: the same native file transfer used by the successful Nemo probe.
            var data = new DataTransfer();
            foreach (var item in items)
                data.Add(DataTransferItem.CreateFile(item));
            DragDiagnostics.Log($"Drag source: DoDragDropAsync starting; formats={string.Join(", ", data.Formats)}; allowed=Copy");
            var result = await DragDrop.DoDragDropAsync(pressEvent, data, DragDropEffects.Copy);
            DragDiagnostics.Log($"Drag source: DoDragDrop completed; result={result}");
        }
        catch (Exception ex) { DragDiagnostics.Log($"Drag source ERROR: {ex}"); StatusText.Text = $"Drag failed: {ex.Message}"; }
        finally { _dragging = false; _hoverTab = null; _pressedDragItem = null; _dragSource = null; }
    }

    private void Files_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStart = null;
        _pressedDragItem = null;
        _dragPressEvent = null;
    }

    private void Files_DragOver(object? sender, DragEventArgs e)
    {
        var target = GetFolderDropTarget(e) ?? Active?.CurrentPath;
        DragDiagnostics.Log($"Files_DragOver: target={target ?? "(none)"}; parsedPaths={GetDroppedPaths(e).Length}; incomingEffects={e.DragEffects}", throttle: true);
        e.DragEffects = target is not null ? AllowedDropEffect(e, target) : DragDropEffects.None;
        e.Handled = true;
    }

    private static string[] GetDroppedPaths(DragEventArgs e)
    {
        // Avalonia 12 exposes native file transfers through DataTransfer.
        try
        {
            return (e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>())
                .Where(item => item.Path.IsFile)
                .Select(item => item.Path.LocalPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex)
        {
            DragDiagnostics.Log($"GetDroppedPaths: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    // Determine the filesystem using the Linux mount table, including bind and NFS mounts.
    // A failed lookup safely defaults to copy, not a destructive move.
    private static string? GetMountPoint(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            return File.ReadLines("/proc/self/mountinfo")
                .Select(line => line.Split(' '))
                .Where(parts => parts.Length > 4)
                .Select(parts => parts[4].Replace("\\040", " ").Replace("\\011", "\t"))
                .Where(mount => full == mount || full.StartsWith(mount.TrimEnd('/') + "/", StringComparison.Ordinal))
                .OrderByDescending(mount => mount.Length).FirstOrDefault();
        }
        catch (Exception) { return null; }
    }

    private static DragDropEffects ChooseDropEffect(DragEventArgs e, string destination)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) return DragDropEffects.Copy;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return DragDropEffects.Move;
        var destinationMount = GetMountPoint(destination);
        var sources = GetDroppedPaths(e);
        return destinationMount is not null && sources.Length > 0 &&
               sources.All(path => GetMountPoint(path) == destinationMount)
            ? DragDropEffects.Move : DragDropEffects.Copy;
    }

    private static DragDropEffects AllowedDropEffect(DragEventArgs e, string destination)
    {
        var sources = GetDroppedPaths(e);
        if (sources.Length == 0 || !Directory.Exists(destination)) return DragDropEffects.None;
        var fullDestination = Path.GetFullPath(destination).TrimEnd('/') + "/";
        if (sources.Any(source =>
        {
            var fullSource = Path.GetFullPath(source).TrimEnd('/');
            return fullDestination == fullSource + "/" ||
                   (Directory.Exists(source) && fullDestination.StartsWith(fullSource + "/", StringComparison.Ordinal));
        })) return DragDropEffects.None;
        var desired = ChooseDropEffect(e, destination);
        return (e.DragEffects & desired) != DragDropEffects.None ? desired
            : (e.DragEffects & DragDropEffects.Copy) != DragDropEffects.None ? DragDropEffects.Copy
            : (e.DragEffects & DragDropEffects.Move) != DragDropEffects.None ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private static string? GetFolderDropTarget(DragEventArgs e)
    {
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is FileViewItem entry && Directory.Exists(entry.Entry.FullPath))
            return entry.Entry.FullPath;
        return null;
    }

    private async void Files_Drop(object? sender, DragEventArgs e)
    {
        var destination = GetFolderDropTarget(e) ?? Active?.CurrentPath;
        if (destination is null) return;
        e.Handled = true;
        await ImportDropAsync(e, destination);
    }

    private async Task ImportDropAsync(DragEventArgs e, string destination)
    {
        var paths = GetDroppedPaths(e);
        DragDiagnostics.Log($"ImportDrop: destination={destination}; parsedPaths={paths.Length}; effects={e.DragEffects}");
        if (paths.Length == 0 || AllowedDropEffect(e, destination) == DragDropEffects.None) { DragDiagnostics.Log("ImportDrop: rejected (no paths or no allowed effect)"); return; }
        var move = ChooseDropEffect(e, destination) == DragDropEffects.Move;
        var completed = 0;
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
                        _operations.Copy(source, destination);
                        _operations.Delete(source);
                    }
                }
                else _operations.Copy(source, destination, overwrite: exists);
                InvalidateTreeFor(source, target);
                completed++;
            }
            catch (Exception ex) { await ShowMessageAsync("Drop failed", $"{source}\n{ex.Message}"); }
        }
        RefreshFiles();
        if (completed > 0) StatusText.Text = $"{(move ? "Moved" : "Copied")} {completed} item(s) to {destination}";
    }

    private void Tabs_DragOver(object? sender, DragEventArgs e)
    {
        var tab = (e.Source as Avalonia.Visual)?.FindAncestorOfType<TabItem>();
        if (tab is null) { e.DragEffects = DragDropEffects.None; return; }
        var index = Tabs.Items.Cast<object>().ToList().IndexOf(tab);
        if (index < 0 || index >= _tabs.Count) return;
        e.DragEffects = AllowedDropEffect(e, _tabs[index].CurrentPath);
        if (!ReferenceEquals(tab, _hoverTab)) { _hoverTab = tab; _hoverTabSince = DateTime.UtcNow; }
        else if ((DateTime.UtcNow - _hoverTabSince).TotalMilliseconds > 450 && !tab.IsSelected)
            Tabs.SelectedItem = tab;
        e.Handled = true;
    }

    private async void Tabs_Drop(object? sender, DragEventArgs e)
    {
        var tab = (e.Source as Avalonia.Visual)?.FindAncestorOfType<TabItem>();
        if (tab is null) return;
        var index = Tabs.Items.Cast<object>().ToList().IndexOf(tab);
        if (index < 0 || index >= _tabs.Count) return;
        var destination = _tabs[index].CurrentPath;
        Tabs.SelectedIndex = index;
        e.Handled = true;
        await ImportDropAsync(e, destination);
    }

    private void Tree_DragOver(object? sender, DragEventArgs e)
    {
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<TreeViewItem>();
        e.DragEffects = item?.DataContext is DirectoryTreeNode { IsReal: true } node
            ? AllowedDropEffect(e, node.Path) : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Tree_Drop(object? sender, DragEventArgs e)
    {
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<TreeViewItem>();
        if (item?.DataContext is not DirectoryTreeNode { IsReal: true } node) return;
        e.Handled = true;
        await ImportDropAsync(e, node.Path);
    }

    private void Files_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var list = sender as ListBox ?? CurrentFilesList;
        var point = e.GetCurrentPoint(list);
        if (point.Properties.IsLeftButtonPressed)
        {
            // Modified clicks are for multi-selection, never single-click opening.
            _suppressSingleClickOpen = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) != KeyModifiers.None;
            _dragStart = e.GetPosition(list);
            _dragPressEvent = e;
            _dragSource = list;
            _pressedDragItem = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext as FileViewItem;
            if (_pressedDragItem is null) { _dragStart = null; _dragPressEvent = null; }
            return;
        }
        _dragPressEvent = null;
        if (!point.Properties.IsRightButtonPressed) return;
        _lastRightClickUtc = DateTime.UtcNow;
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item is not null && !list.SelectedItems!.Contains(item.DataContext))
            list.SelectedItem = item.DataContext;
        else if (item is null) list.SelectedIndex = -1;
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
        var registry = new LinuxApplicationRegistry();
        string mime;
        IReadOnlyList<DesktopApplication> apps;
        try
        {
            mime = await Task.Run(() => registry.DetectMime(path));
            apps = await Task.Run(() => registry.GetApplications(mime));
        }
        catch (Exception ex) { await ShowMessageAsync("Open With", ex.Message); return; }

        var store = new OpenWithAssociationStore();
        var customIds = store.Get(mime);
        var hiddenIds = store.GetHidden(mime);
        const string customPrefix = "exec:";
        bool IsCustomExecutable(DesktopApplication app) => app.DesktopId.StartsWith(customPrefix, StringComparison.Ordinal);
        DesktopApplication[] AllApplications() => apps.Concat(customIds.Concat(hiddenIds).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id => id.StartsWith(customPrefix, StringComparison.Ordinal))
            .Select(id => id[customPrefix.Length..])
            .Where(File.Exists)
            .Select(executable => new DesktopApplication(customPrefix + executable,
                (store.GetApplicationName(executable) ?? LinuxApplicationRegistry.GetExecutableDisplayName(executable)), false, false)))
            .GroupBy(app => app.DesktopId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(app => new DesktopApplication(app.DesktopId,
                store.GetDisplayName(IsCustomExecutable(app) ? app.DesktopId[customPrefix.Length..] : app.DesktopId)
                ?? app.Name, app.Recommended, app.IsDefault))
            .ToArray();
        DesktopApplication[] known = Array.Empty<DesktopApplication>();
        DesktopApplication[] others = Array.Empty<DesktopApplication>();
        void RebuildLists()
        {
            known = AllApplications().Where(a => !hiddenIds.Contains(a.DesktopId) &&
                (a.Recommended || customIds.Contains(a.DesktopId)))
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            others = AllApplications().Where(a => hiddenIds.Contains(a.DesktopId) ||
                (!a.Recommended && !customIds.Contains(a.DesktopId)))
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        RebuildLists();
        var showAll = new CheckBox { Content = "Show all installed applications" };
        var knownHeader = new TextBlock { FontWeight = Avalonia.Media.FontWeight.SemiBold };
        var othersHeader = new TextBlock { FontWeight = Avalonia.Media.FontWeight.SemiBold, Text = "All other installed applications" };
        var knownList = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            MinHeight = 70,
            MaxHeight = 210,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<DesktopApplication>((app, _) =>
            {
                var label = new TextBlock { Text = app?.DisplayName ?? string.Empty, Margin = new Thickness(5, 3) };
                if (app is not null)
                    ToolTip.SetTip(label, IsCustomExecutable(app) ? app.DesktopId[customPrefix.Length..] : app.DesktopId);
                return label;
            }, true)
        };
        var otherList = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            MinHeight = 70,
            MaxHeight = 210,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<DesktopApplication>((app, _) =>
            {
                var label = new TextBlock { Text = app?.DisplayName ?? string.Empty, Margin = new Thickness(5, 3) };
                if (app is not null)
                    ToolTip.SetTip(label, IsCustomExecutable(app) ? app.DesktopId[customPrefix.Length..] : app.DesktopId);
                return label;
            }, true)
        };
        // Each application list has its own ScrollViewer, nested inside the dialog's
        // outer ScrollViewer. Route wheel events to the list beneath the pointer
        // first, so a long list scrolls without moving the entire dialog.
        void ScrollApplicationList(object? sender, PointerWheelEventArgs e)
        {
            if (sender is not ListBox list || e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scroller is null) return;

            var wheel = e.Delta.Y;
            if (wheel == 0) return;
            var maxOffset = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
            var newOffset = Math.Clamp(scroller.Offset.Y - wheel * 48, 0, maxOffset);
            if (Math.Abs(newOffset - scroller.Offset.Y) < 0.01) return;

            scroller.Offset = new Vector(scroller.Offset.X, newOffset);
            e.Handled = true;
        }
        knownList.AddHandler(PointerWheelChangedEvent, ScrollApplicationList,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        otherList.AddHandler(PointerWheelChangedEvent, ScrollApplicationList,
            RoutingStrategies.Tunnel, handledEventsToo: true);

        var addApp = new Button { Content = "Add selected to list" };
        var addExecutable = new Button { Content = "Add executable to list" };
        var removeApp = new Button { Content = "Remove from list" };
        var editName = new Button { Content = "Edit display name" };
        DesktopApplication? SelectedApp() => knownList.SelectedItem as DesktopApplication
            ?? otherList.SelectedItem as DesktopApplication;
        void UpdateActions()
        {
            var app = SelectedApp();
            addApp.IsEnabled = app is not null &&
                (hiddenIds.Contains(app.DesktopId) || (!app.Recommended && !customIds.Contains(app.DesktopId)));
            removeApp.IsEnabled = app is not null && !hiddenIds.Contains(app.DesktopId);
            editName.IsEnabled = app is not null;
        }
        void RefreshApplicationChoices(string? selectId = null)
        {
            RebuildLists();
            knownHeader.Text = $"Known applications for this file type ({known.Length})";
            knownList.ItemsSource = known;
            otherList.ItemsSource = showAll.IsChecked == true ? others : Array.Empty<DesktopApplication>();
            othersHeader.IsVisible = otherList.IsVisible = showAll.IsChecked == true;
            knownList.SelectedItem = known.FirstOrDefault(a => a.DesktopId == selectId);
            otherList.SelectedItem = showAll.IsChecked == true ? others.FirstOrDefault(a => a.DesktopId == selectId) : null;
            if (SelectedApp() is null && known.Length > 0 && selectId is null) knownList.SelectedIndex = 0;
            UpdateActions();
        }
        knownList.SelectionChanged += (_, _) =>
        {
            if (knownList.SelectedItem is not null) otherList.SelectedItem = null;
            UpdateActions();
        };
        otherList.SelectionChanged += (_, _) =>
        {
            if (otherList.SelectedItem is not null) knownList.SelectedItem = null;
            UpdateActions();
        };
        showAll.IsCheckedChanged += (_, _) => RefreshApplicationChoices(SelectedApp()?.DesktopId);
        addApp.Click += async (_, _) =>
        {
            if (SelectedApp() is not { } app) return;
            try
            {
                store.Unhide(mime, app.DesktopId);
                store.Add(mime, app.DesktopId);
                customIds = store.Get(mime);
                hiddenIds = store.GetHidden(mime);
                RefreshApplicationChoices(app.DesktopId);
            }
            catch (Exception ex) { await ShowMessageAsync("Add application failed", ex.Message); }
        };
        removeApp.Click += async (_, _) =>
        {
            if (SelectedApp() is not { } app) return;
            try
            {
                store.Remove(mime, app.DesktopId);
                store.Hide(mime, app.DesktopId);
                customIds = store.Get(mime);
                hiddenIds = store.GetHidden(mime);
                // Hide the entry only in filepilot; never modify system MIME associations.
                RefreshApplicationChoices();
            }
            catch (Exception ex) { await ShowMessageAsync("Remove application failed", ex.Message); }
        };
        // Edit names in-place. A second modal Window could appear behind the
        // Open With window under some Linux/X11 window managers.
        var nameEditor = new StackPanel { Spacing = 8, IsVisible = false };
        var editingPath = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var editingName = new TextBox { PlaceholderText = "Friendly application name", MinWidth = 260 };
        var saveName = new Button { Content = "Save name" };
        var resetName = new Button { Content = "Use system name" };
        var cancelName = new Button { Content = "Cancel" };
        nameEditor.Children.Add(new TextBlock
        {
            Text = "Application display name",
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        nameEditor.Children.Add(editingPath);
        nameEditor.Children.Add(editingName);
        nameEditor.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { saveName, resetName, cancelName }
        });
        string? editingKey = null;
        string? editingAppId = null;
        void CloseNameEditor()
        {
            nameEditor.IsVisible = false;
            editingKey = null;
            editingAppId = null;
            editName.IsEnabled = SelectedApp() is not null;
        }
        editName.Click += (_, _) =>
        {
            if (SelectedApp() is not { } selected) return;
            editingAppId = selected.DesktopId;
            editingKey = IsCustomExecutable(selected)
                ? selected.DesktopId[customPrefix.Length..] : selected.DesktopId;
            editingPath.Text = editingKey;
            editingName.Text = store.GetDisplayName(editingKey) ?? selected.Name;
            nameEditor.IsVisible = true;
            editName.IsEnabled = false;
            // Focus after layout has made the docked editor visible.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                editingName.Focus();
                editingName.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Input);
        };
        async Task CommitNameAsync(bool reset)
        {
            if (editingKey is null || editingAppId is null) return;
            try
            {
                store.SetDisplayName(editingKey, reset ? null : editingName.Text);
                var appId = editingAppId;
                CloseNameEditor();
                RefreshApplicationChoices(appId);
            }
            catch (Exception ex)
            {
                // Keep the editor visible so the user can retry.
                await ShowMessageAsync("Rename application failed", ex.Message);
            }
        }
        saveName.Click += async (_, _) => await CommitNameAsync(false);
        resetName.Click += async (_, _) => await CommitNameAsync(true);
        cancelName.Click += (_, _) => CloseNameEditor();
        editingName.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; await CommitNameAsync(false); }
            else if (e.Key == Key.Escape) { e.Handled = true; CloseNameEditor(); }
        };
        RefreshApplicationChoices();
        var manualBox = new TextBox { PlaceholderText = "Executable path (e.g. /usr/bin/code)", MinWidth = 350 };
        addExecutable.IsEnabled = false;
        addExecutable.Click += async (_, _) =>
        {
            var executable = manualBox.Text?.Trim() ?? string.Empty;
            if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            {
                await ShowMessageAsync("Add application", "Enter the full path to an existing executable, such as /usr/bin/code.");
                return;
            }
            try
            {
                var id = customPrefix + Path.GetFullPath(executable);
                store.Add(mime, id);
                customIds = store.Get(mime);
                RefreshApplicationChoices(id);
                manualBox.Text = string.Empty;
            }
            catch (Exception ex) { await ShowMessageAsync("Add application failed", ex.Message); }
        };
        var setDefault = new CheckBox { Content = "Always use selected application for this file type" };
        void UpdateDefaultChoice()
        {
            setDefault.IsEnabled = string.IsNullOrWhiteSpace(manualBox.Text) && SelectedApp() is not null;
            setDefault.IsChecked = setDefault.IsEnabled &&
                SelectedApp() is { } candidate &&
                string.Equals(store.GetPreferred(mime), candidate.DesktopId, StringComparison.OrdinalIgnoreCase);
        }
        knownList.SelectionChanged += (_, _) => UpdateDefaultChoice();
        otherList.SelectionChanged += (_, _) => UpdateDefaultChoice();
        UpdateDefaultChoice();
        var dialog = new Window
        {
            Title = $"Open With: {Path.GetFileName(path)}", Width = 650, Height = 630,
            MinWidth = 500, MinHeight = 490,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var open = new Button { Content = "Open", IsDefault = true };
        var useDefault = new Button { Content = "Use Default" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        open.Click += (_, _) => dialog.Close(1);
        useDefault.Click += (_, _) => dialog.Close(2);
        cancel.Click += (_, _) => dialog.Close(0);
        knownList.DoubleTapped += (_, _) => { if (SelectedApp() is not null) dialog.Close(1); };
        otherList.DoubleTapped += (_, _) => { if (SelectedApp() is not null) dialog.Close(1); };
        manualBox.TextChanged += (_, _) =>
        {
            addExecutable.IsEnabled = !string.IsNullOrWhiteSpace(manualBox.Text);
            setDefault.IsEnabled = string.IsNullOrWhiteSpace(manualBox.Text) && SelectedApp() is not null;
        };
        var container = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
        var bottom = new StackPanel { Spacing = 9 };
        DockPanel.SetDock(bottom, Dock.Bottom);
        // Keep the name editor outside the scrollable application list.
        // The previous inline editor was inserted below the list and could be
        // invisible until the user scrolled down; this dock keeps it in view.
        bottom.Children.Add(nameEditor);
        bottom.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { manualBox, addExecutable }
        });
        bottom.Children.Add(setDefault);
        bottom.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { useDefault, cancel, open }
        });
        container.Children.Add(bottom);
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = $"File type: {mime}" });
        content.Children.Add(knownHeader);
        content.Children.Add(knownList);
        content.Children.Add(showAll);
        content.Children.Add(othersHeader);
        content.Children.Add(otherList);
        content.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { addApp, removeApp, editName }
        });
        content.Children.Add(new TextBlock
        {
            Text = "Remove from list hides an application in filepilot for this file type, without changing Linux MIME associations. Use Show all to restore hidden apps. Edit display name changes the name across file types.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        if (known.Length == 0)
            content.Children.Add(new TextBlock { Text = "No known applications. Select Show all, choose a program, and click Add to list.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        container.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = container;

        var choice = await dialog.ShowDialog<int>(this);
        if (choice == 0) return;
        try
        {
            if (choice == 2) { _launcher.OpenWith("xdg-open", path); return; }
            if (!string.IsNullOrWhiteSpace(manualBox.Text))
            {
                _launcher.OpenWith(manualBox.Text.Trim(), path);
                return;
            }
            if (SelectedApp() is not DesktopApplication selected)
                throw new InvalidOperationException("Select an application or enter an executable.");
            if (setDefault.IsChecked == true)
            {
                if (IsCustomExecutable(selected))
                    store.SetPreferred(mime, selected.DesktopId);
                else
                {
                    registry.SetDefault(selected, mime);
                    // A previous filepilot-only preference must not override the new Linux default.
                    store.SetPreferred(mime, null);
                }
            }
            if (IsCustomExecutable(selected))
                _launcher.OpenWith(selected.DesktopId[customPrefix.Length..], path);
            else
                registry.Launch(selected, path);
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
        var input = new TextBox { PlaceholderText = "Folder name", Width = 320 };
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
        if (_settings.ClickMode != "Single" || _dragging || _suppressSingleClickOpen) return;
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
        _shortcuts = defaults.Concat(_settings.Shortcuts)
            .Where(Directory.Exists)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
    private async void Help_Click(object? sender, RoutedEventArgs e)
    {
        await new HelpWindow().ShowDialog(this);
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        var displayVersion = version is null ? "Unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
        await ShowInformationAsync("About filepilot",
            $"filepilot v{displayVersion}\n\nLinux file manager built with .NET and Avalonia.\n\n" +
            "Features include tabbed browsing, multiple file views, native file drag-and-drop, " +
            "clipboard operations, and libmagic-based File Type Integrity checks.\n\n" +
            "Source repository: WA1GONFM\n\n" +
            "License: MIT License\nCopyright (c) 2026 filepilot contributors\n" +
            "See the LICENSE file for permission and warranty terms.", 520, 355);
    }

    private async Task ShowInformationAsync(string title, string message, double width, double height)
    {
        var dialog = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(20),
            RowSpacing = 12,
            Children =
            {
                new ScrollViewer
                {
                    Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                close
            }
        };
        Grid.SetRow(close, 1);
        await dialog.ShowDialog(this);
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, _store, SetAutoRefresh);
        await dialog.ShowDialog(this);
    }
}

