using System.Collections.Generic;
namespace WA1GONFM.App;
public sealed class DirectoryTreeNode
{
    private bool _loaded;
    public string Name { get; }
    public string Path { get; }
    public ObservableCollection<DirectoryTreeNode> Children { get; } = new();
    public DirectoryTreeNode(string path, string? name = null, bool placeholder = false)
    {
        Path = path; Name = name ?? (path == "/" ? "/" : System.IO.Path.GetFileName(path));
        if (!placeholder && HasSubdirectories(path)) Children.Add(new DirectoryTreeNode(path, "…", true));
        _loaded = placeholder;
    }
    public void LoadChildren()
    {
        if (_loaded) return;
        _loaded = true;
        Children.Clear();
        try
        {
            foreach (var path in Directory.EnumerateDirectories(Path).Where(x => !System.IO.Path.GetFileName(x).StartsWith('.')).OrderBy(x => x))
                Children.Add(new DirectoryTreeNode(path));
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }
    private static bool HasSubdirectories(string path)
    {
        try { return Directory.EnumerateDirectories(path).Any(); }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }
}
