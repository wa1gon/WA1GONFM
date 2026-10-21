using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WA1GONFM.App;

/// <summary>Content-aware Linux file launcher. Never sends paths through a shell.</summary>
public sealed class LinuxFileLauncher
{
    private const int MagicMimeType = 0x000010;
    private const int XOk = 1;

    [DllImport("libmagic.so.1", EntryPoint = "magic_open")]
    private static extern IntPtr MagicOpen(int flags);
    [DllImport("libmagic.so.1", EntryPoint = "magic_load", CharSet = CharSet.Ansi)]
    private static extern int MagicLoad(IntPtr cookie, string? database);
    [DllImport("libmagic.so.1", EntryPoint = "magic_file", CharSet = CharSet.Ansi)]
    private static extern IntPtr MagicFile(IntPtr cookie, string path);
    [DllImport("libmagic.so.1", EntryPoint = "magic_close")]
    private static extern void MagicClose(IntPtr cookie);
    [DllImport("libc.so.6", EntryPoint = "access", SetLastError = true)]
    private static extern int Access(string path, int mode);

    public string DetectMimeType(string path)
    {
        var cookie = MagicOpen(MagicMimeType);
        if (cookie == IntPtr.Zero) throw new IOException("Unable to initialize libmagic.");
        try
        {
            if (MagicLoad(cookie, null) != 0) throw new IOException("Unable to load the system magic database.");
            var result = MagicFile(cookie, path);
            if (result == IntPtr.Zero) throw new IOException($"Unable to inspect '{path}'.");
            return Marshal.PtrToStringUTF8(result) ?? "application/octet-stream";
        }
        finally { MagicClose(cookie); }
    }

    public bool CanExecute(string path)
    {
        if (!OperatingSystem.IsLinux() || !File.Exists(path) || Directory.Exists(path)) return false;
        var mode = File.GetUnixFileMode(path);
        var anyExecuteBit = (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        return anyExecuteBit && Access(path, XOk) == 0;
    }

    public bool IsNativeExecutableOrShebangScript(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> bytes = stackalloc byte[4];
        var count = stream.Read(bytes);
        return (count >= 4 && bytes[0] == 0x7f && bytes[1] == (byte)'E' && bytes[2] == (byte)'L' && bytes[3] == (byte)'F')
            || (count >= 2 && bytes[0] == (byte)'#' && bytes[1] == (byte)'!');
    }

    public void Open(string path)
    {
        if (Directory.Exists(path)) throw new ArgumentException("Directories are navigated by the file manager.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);
        // Content is examined even if the filename extension is misleading.
        var mime = DetectMimeType(path);
        if (CanExecute(path) && IsNativeExecutableOrShebangScript(path))
        {
            Start(path);
            return;
        }
        // Executable bits alone do not make a document or text file runnable.
        // Unrecognized binaries are handed to the desktop association, not executed.
        if (mime == "application/octet-stream")
            throw new NotSupportedException("No recognized application association. Use Open With to choose an application.");
        Start("xdg-open", path);
    }

    public void OpenWith(string executable, string path)
    {
        if (string.IsNullOrWhiteSpace(executable)) throw new ArgumentException("Choose an application executable.");
        if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);
        Start(executable, path);
    }

    private static void Start(string executable, params string[] args)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(args.FirstOrDefault() ?? executable) ?? "/"
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException($"Unable to launch '{executable}'.");
    }
}
