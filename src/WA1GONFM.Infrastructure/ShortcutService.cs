namespace WA1GONFM.Infrastructure;

/// <summary>Manages sidebar shortcuts without touching their target directories.</summary>
public static class ShortcutService
{
    public static bool Remove(AppSettings settings, string path, string home)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(home)) return false;
        var builtIns = new[] { home, Path.Combine(home, "Downloads"), Path.Combine(home, "Documents") };
        if (builtIns.Contains(path, StringComparer.Ordinal)) return false;
        return settings.Shortcuts.Remove(path);
    }
}
