namespace WA1GONFM.App;

/// <summary>Returns a file extension without mistaking a Unix dotfile name for an extension.</summary>
public static class FilenameExtension
{
    public static string GetExtension(string filename)
    {
        var name = Path.GetFileName(filename);
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var lastDot = name.LastIndexOf('.');
        // A leading dot is part of the filename, not an extension.
        // Names such as .config.json still have a genuine .json extension.
        return lastDot <= 0 || lastDot == name.Length - 1 ? string.Empty : name[lastDot..];
    }
}
