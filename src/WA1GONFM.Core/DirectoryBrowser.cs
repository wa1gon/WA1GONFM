namespace WA1GONFM.Core;
public sealed class DirectoryBrowser
{
    public IReadOnlyList<DirectoryEntry> List(string path, bool showHidden, bool showDirectories)
    {
        var result = new List<DirectoryEntry>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            try
            {
                var name = Path.GetFileName(entry);
                var attributes = File.GetAttributes(entry);
                var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                var hidden = name.StartsWith('.') || attributes.HasFlag(FileAttributes.Hidden);
                if ((!showHidden && hidden) || (!showDirectories && isDirectory)) continue;
                result.Add(new DirectoryEntry(name, entry, isDirectory, hidden));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return result.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
