using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using System.Threading.Tasks;
namespace WA1GONFM.App;
public partial class MainWindow : Window
{
    private readonly DirectoryBrowser _browser = new();
    private readonly SettingsStore _store = new();
    private readonly TerminalLauncher _terminal = new();
    private readonly FileOperations _operations = new();
    private string? _clipboardPath;
    private bool _clipboardCut;
    private readonly AppSettings _settings;
    private readonly ObservableCollection<NavigationTab> _tabs = new();
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
        DirectoryTree.ItemsSource = new[] { new DirectoryTreeNode("/") };
        RefreshShortcuts();
        _tabs.Add(new NavigationTab(Home));
        _initialized = true;
        RefreshTabs(0);
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
            FilesList.ItemsSource = entries.Select(x => (x.IsDirectory ? "📁  " : "📄  ") + x.Name).ToArray();
            _entries = entries;
            StatusText.Text = $"{entries.Count} items  •  {Active.CurrentPath}";
            _refreshing = true;
            HomeRootToggle.IsChecked = Active.CurrentPath == "/";
            _refreshing = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = ex.Message;
            _entries = Array.Empty<DirectoryEntry>();
            FilesList.ItemsSource = Array.Empty<string>();
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
    private void NewTab_Click(object? sender, RoutedEventArgs e) { _tabs.Add(new NavigationTab(Active?.CurrentPath ?? Home)); RefreshTabs(_tabs.Count - 1); }
    private void CloseTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || index < 0 || index >= _tabs.Count) return;
        if (_tabs.Count == 1) { _tabs[0] = new NavigationTab(Home); RefreshTabs(0); return; }
        var selected = Tabs.SelectedIndex;
        _tabs.RemoveAt(index);
        RefreshTabs(Math.Clamp(selected > index ? selected - 1 : selected, 0, _tabs.Count - 1));
    }
    private string? SelectedEntry => FilesList.SelectedIndex is var i && i >= 0 && i < _entries.Count ? _entries[i].FullPath : null;
    private async void Files_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.C || e.Key == Key.X)
            {
                _clipboardPath = SelectedEntry;
                _clipboardCut = e.Key == Key.X;
                if (_clipboardPath is not null) StatusText.Text = $"{(_clipboardCut ? "Move" : "Copy")}: {_clipboardPath}";
                e.Handled = true;
            }
            else if (e.Key == Key.V) { await PasteAsync(); e.Handled = true; }
            else if (e.Key == Key.T) { NewTab_Click(null, new RoutedEventArgs()); e.Handled = true; }
        }
        else if (e.Key == Key.Delete) { await DeleteSelectedAsync(); e.Handled = true; }
        else if (e.Key == Key.F2) { await RenameSelectedAsync(); e.Handled = true; }
    }
    private async Task PasteAsync()
    {
        if (_clipboardPath is null || Active is null) return;
        try
        {
            if (_clipboardCut) { _operations.Move(_clipboardPath, Active.CurrentPath); _clipboardPath = null; }
            else _operations.Copy(_clipboardPath, Active.CurrentPath);
            RefreshFiles();
        }
        catch (Exception ex) { await ShowMessageAsync("File operation failed", ex.Message); }
    }
    private async Task DeleteSelectedAsync()
    {
        var path = SelectedEntry;
        if (path is null) return;
        if (!await ConfirmAsync("Delete permanently?", $"Delete '{Path.GetFileName(path)}' and its contents? This cannot be undone.")) return;
        try { _operations.Delete(path); RefreshFiles(); }
        catch (Exception ex) { await ShowMessageAsync("Delete failed", ex.Message); }
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
        try { _operations.Rename(path, nameBox.Text ?? ""); RefreshFiles(); }
        catch (Exception ex) { await ShowMessageAsync("Rename failed", ex.Message); }
    }
    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new Window { Title = title, Width = 450, Height = 175, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Content = "Delete" };
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
    }
    private void Files_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var index = FilesList.SelectedIndex;
        if (index >= 0 && index < _entries.Count && _entries[index].IsDirectory) Navigate(_entries[index].FullPath);
    }
    private void Tree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initialized && DirectoryTree.SelectedItem is DirectoryTreeNode node) { node.LoadChildren(); Navigate(node.Path); }
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
