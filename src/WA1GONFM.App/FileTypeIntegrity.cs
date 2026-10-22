using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;

namespace WA1GONFM.App;

public enum IntegritySeverity { Unverified, Empty, Match, Unknown, Mismatch, HighRisk }

public sealed record IntegrityResult(IntegritySeverity Severity, string Mime, string Message);

/// <summary>Content-based MIME verification. Detection is advisory, not malware scanning.</summary>
public static class FileTypeIntegrity
{
    private static readonly ConcurrentDictionary<string, (long Length, long Ticks, IntegrityResult Result)> Cache = new();
    private static readonly ConcurrentDictionary<string, string[]> Expected = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"], [".png"] = ["image/png"], [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"], [".gif"] = ["image/gif"], [".webp"] = ["image/webp"],
        [".bmp"] = ["image/bmp", "image/x-ms-bmp"], [".zip"] = ["application/zip", "application/x-zip"],
        [".gz"] = ["application/gzip", "application/x-gzip"], [".7z"] = ["application/x-7z-compressed"],
        [".rar"] = ["application/vnd.rar", "application/x-rar"], [".elf"] = ["application/x-executable", "application/x-pie-executable"],
        [".exe"] = ["application/x-dosexec", "application/vnd.microsoft.portable-executable"],
        [".txt"] = ["text/plain"], [".csv"] = ["text/plain", "text/csv"],
        [".html"] = ["text/html"], [".htm"] = ["text/html"], [".xml"] = ["text/xml", "application/xml"],
        [".json"] = ["application/json", "text/plain"], [".svg"] = ["image/svg+xml", "text/xml", "application/xml"],
        [".mp3"] = ["audio/mpeg"], [".ogg"] = ["audio/ogg", "application/ogg"],
        [".wav"] = ["audio/x-wav", "audio/wav"], [".mp4"] = ["video/mp4"],
        [".webm"] = ["video/webm"], [".sqlite"] = ["application/vnd.sqlite3", "application/x-sqlite3"],
        [".sh"] = ["text/x-shellscript", "text/plain", "application/x-shellscript"],
        [".cs"] = ["text/plain"], [".md"] = ["text/plain", "text/markdown"],
        // These formats use ZIP containers internally; ZIP is not an anomaly.
        [".docx"] = ["application/zip", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        [".xlsx"] = ["application/zip", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [".pptx"] = ["application/zip", "application/vnd.openxmlformats-officedocument.presentationml.presentation"],
        [".jar"] = ["application/zip", "application/java-archive"],
    };
    private static readonly string[] ExecutableMime = ["application/x-executable", "application/x-pie-executable", "application/x-sharedlib", "application/x-dosexec", "application/vnd.microsoft.portable-executable", "application/x-mach-binary", "application/x-shellscript", "text/x-shellscript"];
    private const int MagicMimeType = 0x000010;
    [DllImport("libmagic.so.1", EntryPoint="magic_open")] private static extern IntPtr Open(int flags);
    [DllImport("libmagic.so.1", EntryPoint="magic_load")] private static extern int Load(IntPtr handle, IntPtr database);
    [DllImport("libmagic.so.1", EntryPoint="magic_file", CharSet=CharSet.Ansi)] private static extern IntPtr Identify(IntPtr handle, string filename);
    [DllImport("libmagic.so.1", EntryPoint="magic_close")] private static extern void Close(IntPtr handle);

    public static IntegrityResult Check(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                return new(IntegritySeverity.Unverified, "", "Not scanned (missing file or symbolic link)");
            if (Cache.TryGetValue(path, out var cached) && cached.Length == info.Length && cached.Ticks == info.LastWriteTimeUtc.Ticks)
                return cached.Result;
            // Empty files have no signature to verify. libmagic reports inode/x-empty,
            // which is not evidence of a mismatched or dangerous file type.
            if (info.Length == 0)
            {
                var empty = new IntegrityResult(IntegritySeverity.Empty, "inode/x-empty",
                    "Empty file: no content available for file type verification (not a security mismatch)");
                Cache[path] = (info.Length, info.LastWriteTimeUtc.Ticks, empty);
                return empty;
            }
            // .adi is ambiguous: libmagic may label amateur-radio ADIF logs as AAC audio.
            // A validated ADIF structure takes precedence over that generic signature.
            if (FilenameExtension.GetExtension(path).Equals(".adi", StringComparison.OrdinalIgnoreCase) && AdifDetector.IsAdif(path))
            {
                var adif = new IntegrityResult(IntegritySeverity.Match, "application/x-adif",
                    "Validated amateur radio ADIF log (content verified; not an audio file)");
                Cache[path] = (info.Length, info.LastWriteTimeUtc.Ticks, adif);
                return adif;
            }
            var mime = DetectMime(path);
            if (mime is null) return new(IntegritySeverity.Unverified, "", "libmagic unavailable or file could not be identified");
            var ext = FilenameExtension.GetExtension(path);
            IntegrityResult result;
            if (string.IsNullOrEmpty(ext)) result = new(IntegritySeverity.Unknown, mime, $"Extensionless file; detected {DescribeMime(mime)} ({mime}). No mismatch warning.");
            else if (!Expected.TryGetValue(ext, out var expected)) result = new(IntegritySeverity.Unknown, mime, $"No comparison rule for {ext}; detected {mime}");
            else if (Array.Exists(expected, e => string.Equals(e, mime, StringComparison.OrdinalIgnoreCase))) result = new(IntegritySeverity.Match, mime, $"Matches {ext}: {mime}");
            else if (Array.Exists(ExecutableMime, e => string.Equals(e, mime, StringComparison.OrdinalIgnoreCase))) result = new(IntegritySeverity.HighRisk, mime, $"WARNING: {ext} claims {string.Join(" or ", expected)} but contents are {mime}. Do not run this file without verification.");
            else result = new(IntegritySeverity.Mismatch, mime, $"Mismatch: {ext} expects {string.Join(" or ", expected)}, detected {mime}. Not necessarily malicious.");
            Cache[path] = (info.Length, info.LastWriteTimeUtc.Ticks, result);
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { return new(IntegritySeverity.Unverified, "", "Unable to verify file type"); }
    }

    /// <summary>Human-readable description without exposing raw MIME identifiers in the Type column.</summary>
    public static string DescribeMime(string mime) => MimeDescriptionResolver.Describe(mime);

    private static string? DetectMime(string path)
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = Open(MagicMimeType);
            if (handle == IntPtr.Zero || Load(handle, IntPtr.Zero) != 0) return null;
            var ptr = Identify(handle, path);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr)?.Split(';')[0].Trim();
        }
        finally { if (handle != IntPtr.Zero) Close(handle); }
    }
}
