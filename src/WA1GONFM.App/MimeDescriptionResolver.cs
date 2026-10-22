using System;
using System.Collections.Generic;
using System.Linq;

namespace WA1GONFM.App;

/// <summary>Display-only MIME labels. Integrity comparisons continue to use original MIME identifiers.</summary>
public static class MimeDescriptionResolver
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["inode/x-empty"] = "Empty File",
        ["application/x-adif"] = "Amateur Radio ADIF Log",
        ["text/plain"] = "Text Document",
        ["text/x-shellscript"] = "Shell Script",
        ["application/x-shellscript"] = "Shell Script",
        ["text/x-python"] = "Python Script",
        ["text/x-perl"] = "Perl Script",
        ["text/x-ruby"] = "Ruby Script",
        ["text/x-c"] = "C Source Code",
        ["text/x-c++"] = "C++ Source Code",
        ["text/html"] = "HTML Document",
        ["text/css"] = "CSS Stylesheet",
        ["text/xml"] = "XML Document",
        ["application/xml"] = "XML Document",
        ["text/markdown"] = "Markdown Document",
        ["text/csv"] = "CSV Document",
        ["application/json"] = "JSON Document",
        ["application/pdf"] = "PDF Document",
        ["image/png"] = "PNG Image",
        ["image/jpeg"] = "JPEG Image",
        ["image/gif"] = "GIF Image",
        ["image/webp"] = "WebP Image",
        ["image/bmp"] = "BMP Image",
        ["image/x-ms-bmp"] = "BMP Image",
        ["image/svg+xml"] = "SVG Image",
        ["image/tiff"] = "TIFF Image",
        ["image/heic"] = "HEIC Image",
        ["application/zip"] = "ZIP Archive",
        ["application/x-zip"] = "ZIP Archive",
        ["application/gzip"] = "Gzip Archive",
        ["application/x-gzip"] = "Gzip Archive",
        ["application/x-7z-compressed"] = "7-Zip Archive",
        ["application/x-rar"] = "RAR Archive",
        ["application/vnd.rar"] = "RAR Archive",
        ["application/x-tar"] = "TAR Archive",
        ["application/x-bzip2"] = "Bzip2 Archive",
        ["application/x-xz"] = "XZ Archive",
        ["application/x-executable"] = "ELF Executable",
        ["application/x-pie-executable"] = "ELF Executable",
        ["application/x-sharedlib"] = "Shared Library",
        ["application/x-dosexec"] = "Windows Executable",
        ["application/vnd.microsoft.portable-executable"] = "Windows Executable",
        ["application/x-sqlite3"] = "SQLite Database",
        ["application/vnd.sqlite3"] = "SQLite Database",
        ["audio/mpeg"] = "MP3 Audio",
        ["audio/ogg"] = "Ogg Audio",
        ["application/ogg"] = "Ogg Audio",
        ["audio/wav"] = "WAV Audio",
        ["audio/x-wav"] = "WAV Audio",
        ["audio/aac"] = "AAC Audio",
        ["audio/x-aac"] = "AAC Audio",
        ["audio/x-hx-aac-adif"] = "AAC Audio",
        ["audio/flac"] = "FLAC Audio",
        ["audio/x-flac"] = "FLAC Audio",
        ["video/mp4"] = "MP4 Video",
        ["video/webm"] = "WebM Video",
        ["video/x-matroska"] = "Matroska Video",
        ["application/octet-stream"] = "Binary File",
        ["application/vnd.oasis.opendocument.text"] = "OpenDocument Text",
        ["application/vnd.oasis.opendocument.text.odt"] = "OpenDocument Text",
        ["application/vnd.oasis.opendocument.spreadsheet"] = "OpenDocument Spreadsheet",
        ["application/vnd.oasis.opendocument.presentation"] = "OpenDocument Presentation",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "Word Document",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "Excel Spreadsheet",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = "PowerPoint Presentation",
        ["application/msword"] = "Word Document",
        ["application/vnd.ms-excel"] = "Excel Spreadsheet",
        ["application/vnd.ms-powerpoint"] = "PowerPoint Presentation",
        ["application/rtf"] = "Rich Text Document",
        ["application/java-archive"] = "Java Archive",
        ["application/x-elf"] = "ELF Binary"
    };

    public static string Describe(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime)) return "Unknown";
        var normalized = mime.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (Names.TryGetValue(normalized, out var label)) return label;
        var slash = normalized.IndexOf('/');
        if (slash <= 0 || slash == normalized.Length - 1) return "Unknown File";
        var category = normalized[..slash];
        var subtype = normalized[(slash + 1)..];
        // Unmapped vendor MIME names are often opaque. Avoid displaying raw MIME strings.
        if (subtype.StartsWith("vnd.", StringComparison.Ordinal) || subtype.Length > 42)
            return category switch
            {
                "audio" => "Audio File", "video" => "Video File", "image" => "Image File",
                "text" => "Text Document", "application" => "Document or Data File", _ => "Unknown File"
            };
        if (subtype.StartsWith("x-", StringComparison.Ordinal)) subtype = subtype[2..];
        var words = subtype.Replace('+', ' ').Replace('-', ' ').Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "Unknown File";
        var name = string.Join(" ", words.Select(w => w.Length <= 4 ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..]));
        return category switch
        {
            "audio" => $"{name} Audio", "video" => $"{name} Video", "image" => $"{name} Image",
            "text" => $"{name} Text", "application" => $"{name} File", "font" => $"{name} Font",
            _ => "Unknown File"
        };
    }
}
