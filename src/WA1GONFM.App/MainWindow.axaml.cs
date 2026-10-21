using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia.Controls;
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
        RefreshShortcuts();
        _tabs.Add(new NavigationTab(Home) { ViewMode = _settings.DefaultViewMode });
        _initialized = true;
        RefreshTabs(0);
        AddHandler(KeyDownEvent, Window_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.C) { CaptureSelection(false); e.Handled = true; }
            else if (e.Key == Key.X) { CaptureSelection(true); e.Handled = true; }
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
    private void CaptureSelection(bool cut)
    {
        _clipboardPath = SelectedEntry;
        _clipboardCut = cut;
        if (_clipboardPath is not null)
            StatusText.Text = $"{(cut ? "Cut" : "Copied")}: {_clipboardPath}";
    }
    private async Task PasteAsync()
    {
        if (_clipboardPath is null || Active is null) return;
        var source = _clipboardPath;
        var target = Path.Combine(Active.CurrentPath, Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)));
        try
        {
            if (Path.GetFullPath(source) == Path.GetFullPath(target))
            {
                StatusText.Text = "Source and destination are the same.";
                return;
            }
            var exists = File.Exists(target) || Directory.Exists(target);
            if (_clipboardCut && _settings.ConfirmMove &&
                !await ConfirmAsync("Move", $"Move '{Path.GetFileName(source)}' to '{Active.CurrentPath}'?", "Move")) return;
            if (exists && _settings.ConfirmOverwrite &&
                !await ConfirmAsync("Overwrite", $"Replace existing '{target}'?", "Replace")) return;
            if (_clipboardCut)
            {
                _operations.Move(source, Active.CurrentPath, overwrite: exists);
                _clipboardPath = null;
            }
            else _operations.Copy(source, Active.CurrentPath, overwrite: exists);
            RefreshFiles();
            InvalidateTreeFor(source, target);
            StatusText.Text = $"{(_clipboardCut ? "Copied" : "Pasted")}: {target}";
        }
        catch (Exception ex) { await ShowMessageAsync("Paste failed", ex.Message); }
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
    private void Files_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.GetFiles()?.Any() == true ? DragDropEffects.Copy : DragDropEffects.None;
    }
    private async void Files_Drop(object? sender, DragEventArgs e)
    {
        if (Active is null) return;
        var files = e.Data.GetFiles()?.ToArray();
        if (files is null || files.Length == 0) return;
        foreach (var item in files)
        {
            var source = item.Path.LocalPath;
            try { _operations.Copy(source, Active.CurrentPath); }
            catch (Exception ex) { await ShowMessageAsync("Drop failed", ex.Message); }
        }
        RefreshFiles();
        InvalidateTreeFor(Active.CurrentPath);
    }
    private void Files_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(sender as ListBox ?? CurrentFilesList).Properties.IsRightButtonPressed) return;
        var list = sender as ListBox ?? CurrentFilesList;
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
    private void Cut_Click(object? sender, RoutedEventArgs e) => CaptureSelection(true);
    private void Copy_Click(object? sender, RoutedEventArgs e) => CaptureSelection(false);
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
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10,
            Children = { input, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { create, cancel } } } };
        if (!await dialog.ShowDialog<bool>(this)) return;
        var name = input.Text;
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Contains('/') || name.Contains('\\')) return;
        try { Directory.CreateDirectory(Path.Combine(Active.CurrentPath, name)); RefreshFiles(); InvalidateTreeFor(Active.CurrentPath); }
        catch (Exception ex) { await ShowMessageAsync("New folder failed", ex.Message); }
    }
    private async void Files_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SelectedEntry is { } path) await OpenPathAsync(path);
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
    private void Shortcut_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ShortcutsList.SelectedIndex is var i && i >= 0 && i < _shortcuts.Length) Navigate(_shortcuts[i]);
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
