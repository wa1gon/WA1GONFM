using System.Diagnostics;
namespace WA1GONFM.App;
public static class TrashService
{
    public static async Task MoveToTrashAsync(string path)
    {
        // gio trash follows freedesktop.org trash semantics, including mounted volumes.
        var start = new ProcessStartInfo("gio") { UseShellExecute = false, RedirectStandardError = true };
        start.ArgumentList.Add("trash");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(path);
        using var process = Process.Start(start) ?? throw new IOException("Unable to start gio trash.");
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new IOException($"gio trash failed: {error.Trim()}");
    }
}
