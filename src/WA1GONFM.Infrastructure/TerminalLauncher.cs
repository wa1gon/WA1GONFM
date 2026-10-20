using System.Diagnostics;
namespace WA1GONFM.Infrastructure;
public sealed class TerminalLauncher
{
    public void Launch(string directory, AppSettings settings)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        // ArgumentList prevents shell interpretation of paths; executable is a trusted user setting.
        var start = new ProcessStartInfo(settings.TerminalExecutable) { WorkingDirectory = directory, UseShellExecute = false };
        foreach (var argument in SplitArguments(settings.TerminalArguments)) start.ArgumentList.Add(argument);
        Process.Start(start);
    }
    private static System.Collections.Generic.IEnumerable<string> SplitArguments(string arguments)
    {
        // Settings currently supports simple whitespace-separated options, not shell quoting.
        return arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
