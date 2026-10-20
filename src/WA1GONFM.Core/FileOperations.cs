namespace WA1GONFM.Core;
public sealed class FileOperations
{
    public void Copy(string source, string destinationDirectory)
    {
        var destination = Destination(source, destinationDirectory);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException($"Destination already exists: {destination}");
        if (Directory.Exists(source)) CopyDirectory(source, destination);
        else File.Copy(source, destination);
    }
    public void Move(string source, string destinationDirectory)
    {
        var destination = Destination(source, destinationDirectory);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException($"Destination already exists: {destination}");
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else File.Move(source, destination);
    }
    public void Rename(string source, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName is "." or ".." || newName.Contains('/') || newName.Contains('\\')) throw new ArgumentException("Invalid name.");
        var parent = Path.GetDirectoryName(Path.GetFullPath(source)) ?? throw new IOException("Missing parent directory.");
        Move(source, parent, newName);
    }
    public void Delete(string source)
    {
        if (Directory.Exists(source)) Directory.Delete(source, true);
        else File.Delete(source);
    }
    private static string Destination(string source, string directory) => Destination(source, directory, Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)));
    private static string Destination(string source, string directory, string name)
    {
        if (!File.Exists(source) && !Directory.Exists(source)) throw new FileNotFoundException("Source does not exist.", source);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        var dest = Path.Combine(Path.GetFullPath(directory), name);
        if (Directory.Exists(source) && Path.GetFullPath(dest).StartsWith(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new IOException("Cannot move/copy a directory into itself.");
        return dest;
    }
    private void Move(string source, string directory, string name)
    {
        var dest = Destination(source, directory, name);
        if (File.Exists(dest) || Directory.Exists(dest)) throw new IOException($"Destination already exists: {dest}");
        if (Directory.Exists(source)) Directory.Move(source, dest);
        else File.Move(source, dest);
    }
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var sub in Directory.EnumerateDirectories(source)) CopyDirectory(sub, Path.Combine(destination, Path.GetFileName(sub)));
    }
}
