using Avalonia.Input;
using System.Diagnostics;

namespace WA1GONFM.App;

// Temporary XDND diagnostics. Remove once Nemo interoperability is verified.
internal static class DragDiagnostics
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long _lastOverMilliseconds = -1000;

    internal static void Log(string message, bool throttle = false)
    {
        if (throttle && Clock.ElapsedMilliseconds - _lastOverMilliseconds < 700) return;
        if (throttle) _lastOverMilliseconds = Clock.ElapsedMilliseconds;
        Console.Error.WriteLine($"[FM-DND {DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    private static void Report(string phase, DragEventArgs e, bool throttle = false)
    {
        try
        {
            var formats = string.Join(", ", e.DataTransfer.Formats);
            var files = e.DataTransfer.TryGetFiles()?.Select(item => item.Path.ToString()).ToArray();
            var sourceType = e.Source?.GetType().Name ?? "null";
            Log($"{phase}: source={sourceType}; effects={e.DragEffects}; modifiers={e.KeyModifiers}; formats=[{formats}]; files=[{string.Join(" | ", files ?? Array.Empty<string>())}]", throttle);
        }
        catch (Exception ex) { Log($"{phase}: inspection failed: {ex}"); }
    }

    internal static void OnEnter(object? sender, DragEventArgs e) => Report("DragEnter", e);
    internal static void OnOver(object? sender, DragEventArgs e) => Report("DragOver", e, throttle: true);
    internal static void OnLeave(object? sender, DragEventArgs e) => Report("DragLeave", e);
    internal static void OnDrop(object? sender, DragEventArgs e) => Report("Drop", e);
}
