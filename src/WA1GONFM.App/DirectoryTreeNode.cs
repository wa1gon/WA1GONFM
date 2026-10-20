namespace WA1GONFM.App;

/// <summary>A directory node whose contents are never enumerated until expansion.</summary>
public sealed class DirectoryTreeNode
{
    private bool _loaded;
    private bool _stale;
    private readonly bool _placeholder;

    public string Name { get; }
    public string Path { get; }
    public bool IsReal => !_placeholder;
    public bool IsLoaded => _loaded;
    public ObservableCollection<DirectoryTreeNode> Children { get; } = new();

    public DirectoryTreeNode(string path, string? name = null)
        : this(path, name, false)
    {
    }

    private DirectoryTreeNode(string path, string? name, bool placeholder)
    {
        Path = path;
        Name = name ?? (path == "/" ? "/" : System.IO.Path.GetFileName(path));
        _placeholder = placeholder;
        _loaded = placeholder;
        // An invisible sentinel provides Avalonia's expander without probing the filesystem.
        if (!placeholder) Children.Add(new DirectoryTreeNode(path, string.Empty, true));
    }

    public void Invalidate()
    {
        if (_loaded) _stale = true;
    }

    public async Task LoadChildrenAsync()
    {
        if (_placeholder || (_loaded && !_stale)) return;

        // Enumerate off the UI thread: network mounts must not freeze the window.
        var paths = await Task.Run(() =>
        {
            try
            {
                return Directory.EnumerateDirectories(Path)
                    .Where(path => !System.IO.Path.GetFileName(path).StartsWith('.'))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        });

        // Reuse existing nodes so expanded descendants survive refresh.
        var existing = Children.Where(child => child.IsReal)
            .ToDictionary(child => child.Path, StringComparer.Ordinal);
        Children.Clear();
        foreach (var path in paths)
            Children.Add(existing.TryGetValue(path, out var node) ? node : new DirectoryTreeNode(path));
        _loaded = true;
        _stale = false;
    }

    public DirectoryTreeNode? FindLoaded(string path)
    {
        if (string.Equals(Path, path, StringComparison.Ordinal)) return this;
        foreach (var child in Children.Where(child => child.IsReal))
        {
            var found = child.FindLoaded(path);
            if (found is not null) return found;
        }
        return null;
    }
}
