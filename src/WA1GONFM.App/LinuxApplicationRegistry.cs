using System.Diagnostics;
using System.Text;

namespace WA1GONFM.App;

/// <summary>Queries Linux XDG MIME associations and launches desktop applications without shell interpolation.</summary>
public sealed class LinuxApplicationRegistry
{
    /// <summary>Find an executable's display name from the freedesktop desktop-entry metadata.</summary>
    public static string GetExecutableDisplayName(string executable)
    {
        var executableName = Path.GetFileName(executable);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(userData)) userData = Path.Combine(home, ".local/share");
        var dirs = new[] { userData }
            .Concat((Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ??
                "/usr/local/share:/usr/share:/var/lib/flatpak/exports/share:" +
                Path.Combine(home, ".local/share/flatpak/exports/share"))
                .Split(':', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal);
        foreach (var directory in dirs)
        {
            var applicationDirectory = Path.Combine(directory, "applications");
            if (!Directory.Exists(applicationDirectory)) continue;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(applicationDirectory, "*.desktop", SearchOption.AllDirectories).ToArray(); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            foreach (var file in files)
            {
                try
                {
                    var inEntry = false;
                    string? name = null;
                    string? exec = null;
                    var hidden = false;
                    foreach (var line in File.ReadLines(file))
                    {
                        if (line == "[Desktop Entry]") { inEntry = true; continue; }
                        if (line.StartsWith('[')) { if (inEntry) break; continue; }
                        if (!inEntry) continue;
                        if (line.StartsWith("Name=", StringComparison.Ordinal)) name = line[5..];
                        if (line.StartsWith("Exec=", StringComparison.Ordinal)) exec = line[5..];
                        if (line is "Hidden=true" or "Type=Link") hidden = true;
                    }
                    if (hidden || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exec)) continue;
                    // Match the executable token, not substrings inside arguments.
                    var command = exec.TrimStart();
                    if (command.StartsWith('"'))
                    {
                        var end = command.IndexOf('"', 1);
                        command = end > 1 ? command[1..end] : "";
                    }
                    else command = command.Split(' ', '\t')[0];
                    if (string.Equals(command, executable, StringComparison.Ordinal) ||
                        string.Equals(Path.GetFileName(command), executableName, StringComparison.Ordinal))
                        return name;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return string.IsNullOrWhiteSpace(executableName) ? executable : executableName;
    }

    public IReadOnlyList<DesktopApplication> GetApplications(string mime)
    {
        var names = ReadDesktopEntries();
        var (defaultId, recommendedIds) = QueryMimeAssociations(mime);
        var result = new List<DesktopApplication>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string id, bool recommended)
        {
            id = id.Trim();
            if (string.IsNullOrEmpty(id) || !seen.Add(id) || !names.TryGetValue(id, out var name)) return;
            result.Add(new DesktopApplication(id, name, recommended, string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase)));
        }

        if (defaultId is not null) Add(defaultId, true);
        foreach (var id in recommendedIds) Add(id, true);
        // Like Nemo's "Other Application" list, include all installed visible desktop apps.
        foreach (var id in names.Keys.OrderBy(id => names[id], StringComparer.CurrentCultureIgnoreCase)) Add(id, false);
        return result;
    }

    public void Launch(DesktopApplication app, string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);
        // gtk-launch interprets the desktop entry's Exec field correctly (including %f/%u).
        Start("gtk-launch", app.DesktopId, Path.GetFullPath(path));
    }

    public void SetDefault(DesktopApplication app, string mime)
    {
        var result = Run("xdg-mime", "default", app.DesktopId, mime);
        if (result.ExitCode != 0) throw new IOException($"Could not set default application: {result.Output}");
    }

    public string DetectMime(string path)
    {
        // GIO uses the same shared-mime-info database and user MIME overrides as Nemo.
        var result = Run("xdg-mime", "query", "filetype", Path.GetFullPath(path));
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            return result.Output.Trim().Split('\n')[0].Trim();
        return new LinuxFileLauncher().DetectMimeType(path);
    }

    private static (string? Default, List<string> Recommended) QueryMimeAssociations(string mime)
    {
        var output = Run("gio", "mime", mime);
        var list = new List<string>();
        string? defaultId = null;
        var inSuggestedSection = false;
        foreach (var raw in output.Output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("Default application for", StringComparison.OrdinalIgnoreCase))
            {
                var idx = line.LastIndexOf(':');
                if (idx >= 0) defaultId = line[(idx + 1)..].Trim();
            }
            else if (line.StartsWith("Recommended applications:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("Registered applications:", StringComparison.OrdinalIgnoreCase))
                inSuggestedSection = true;
            else if (line.EndsWith("applications:", StringComparison.OrdinalIgnoreCase))
                inSuggestedSection = false;
            else if (inSuggestedSection && line.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase))
                list.Add(line);
        }
        return (defaultId, list.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static Dictionary<string, string> ReadDesktopEntries()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(userData)) userData = Path.Combine(home, ".local/share");
        var dirs = new[] { userData }
            .Concat((Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share:/var/lib/flatpak/exports/share:" + Path.Combine(home, ".local/share/flatpak/exports/share"))
                .Split(':', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal);
        foreach (var dir in dirs)
        {
            var applications = Path.Combine(dir, "applications");
            if (!Directory.Exists(applications)) continue;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(applications, "*.desktop", SearchOption.AllDirectories).ToArray(); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            foreach (var file in files)
            {
                var id = Path.GetRelativePath(applications, file).Replace('/', '-');
                if (result.ContainsKey(id)) continue;
                try
                {
                    var desktopEntry = false;
                    string? name = null;
                    var hidden = false;
                    var visible = true;
                    var isApplication = true;
                    foreach (var line in File.ReadLines(file))
                    {
                        if (line == "[Desktop Entry]") { desktopEntry = true; continue; }
                        if (line.StartsWith('[')) { if (desktopEntry) break; continue; }
                        if (!desktopEntry) continue;
                        if (line.StartsWith("Name=", StringComparison.Ordinal)) name = line[5..];
                        if (line is "Hidden=true") hidden = true;
                        if (line is "NoDisplay=true") visible = false;
                        if (line.StartsWith("Type=", StringComparison.Ordinal) && line != "Type=Application") isApplication = false;
                    }
                    if (isApplication && !hidden && visible && !string.IsNullOrWhiteSpace(name)) result[id] = name;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return result;
    }

    private static (int ExitCode, string Output) Run(string command, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(command) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            using var proc = Process.Start(psi);
            if (proc is null) return (-1, "Unable to start application helper");
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(5000)) { proc.Kill(); return (-1, "Application helper timed out"); }
            return (proc.ExitCode, proc.ExitCode == 0 ? stdout : stderr);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception) { return (-1, ex.Message); }
    }

    private static void Start(string executable, params string[] args)
    {
        var psi = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var _ = Process.Start(psi) ?? throw new IOException($"Unable to start {executable}");
    }
}
