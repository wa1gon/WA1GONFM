using System;
using System.IO;
using System.Text;

namespace WA1GONFM.App;

/// <summary>Conservative, bounded inspection of text ADIF (.adi) files. Never executes content.</summary>
public static class AdifDetector
{
    private const int MaxBytes = 256 * 1024;

    public static bool IsAdif(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var bytes = new byte[(int)Math.Min(stream.Length, MaxBytes)];
            var count = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            return IsAdif(bytes.AsSpan(0, count));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public static bool IsAdif(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty || content.IndexOf((byte)0) >= 0) return false;
        var text = Encoding.UTF8.GetString(content);
        var index = 0;
        var fields = 0;
        var qsoFields = 0;
        var hasEndOfRecord = false;
        var hasHeaderEnd = false;
        var hasAdifVersion = false;
        var inHeader = true;
        while (index < text.Length)
        {
            var start = text.IndexOf('<', index);
            if (start < 0) break;
            var end = text.IndexOf('>', start + 1);
            if (end < 0 || end - start > 90) return false;
            var tag = text.Substring(start + 1, end - start - 1).Trim();
            if (tag.Equals("EOH", StringComparison.OrdinalIgnoreCase))
            {
                hasHeaderEnd = true;
                inHeader = false;
                index = end + 1;
                continue;
            }
            if (tag.Equals("EOR", StringComparison.OrdinalIgnoreCase))
            {
                if (qsoFields == 0) return false;
                hasEndOfRecord = true;
                inHeader = false;
                index = end + 1;
                continue;
            }
            var parts = tag.Split(':');
            if (parts.Length is < 2 or > 3 || parts[0].Length == 0 ||
                !IsFieldName(parts[0]) || !int.TryParse(parts[1], out var length) ||
                length < 0 || length > MaxBytes ||
                (parts.Length == 3 && !parts[2].Equals("S", StringComparison.OrdinalIgnoreCase) &&
                 !parts[2].Equals("N", StringComparison.OrdinalIgnoreCase) &&
                 !parts[2].Equals("D", StringComparison.OrdinalIgnoreCase) &&
                 !parts[2].Equals("T", StringComparison.OrdinalIgnoreCase) &&
                 !parts[2].Equals("M", StringComparison.OrdinalIgnoreCase) &&
                 !parts[2].Equals("B", StringComparison.OrdinalIgnoreCase))) return false;
            var valueStart = end + 1;
            if (length > text.Length - valueStart) return false;
            if (parts[0].Equals("ADIF_VER", StringComparison.OrdinalIgnoreCase)) hasAdifVersion = true;
            if (!inHeader || parts[0].Equals("CALL", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("QSO_DATE", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("BAND", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("MODE", StringComparison.OrdinalIgnoreCase)) qsoFields++;
            fields++;
            index = valueStart + length;
        }
        return fields >= 2 && (hasEndOfRecord || (hasHeaderEnd && hasAdifVersion));
    }

    private static bool IsFieldName(string name)
    {
        foreach (var ch in name)
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '_') return false;
        return true;
    }
}
