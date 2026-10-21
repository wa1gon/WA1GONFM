namespace WA1GONFM.Core;
public sealed class NavigationTab
{
    public string ViewMode { get; set; } = "Details";

    private readonly List<string> _history = new();
    private int _position = -1;
    public string CurrentPath => _position >= 0 ? _history[_position] : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string Title => CurrentPath == "/" ? "/" : Path.GetFileName(CurrentPath.TrimEnd('/')) is { Length: > 0 } name ? name : CurrentPath;
    public bool CanBack => _position > 0;
    public bool CanForward => _position < _history.Count - 1;
    public NavigationTab(string path) => Navigate(path);
    public void Navigate(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);
        if (_position >= 0 && string.Equals(CurrentPath, full, StringComparison.Ordinal)) return;
        _history.RemoveRange(_position + 1, _history.Count - _position - 1);
        _history.Add(full);
        _position++;
    }
    public void Back() { if (CanBack) _position--; }
    public void Forward() { if (CanForward) _position++; }
    public void Up() { var parent = Directory.GetParent(CurrentPath); if (parent is not null) Navigate(parent.FullName); }
}
