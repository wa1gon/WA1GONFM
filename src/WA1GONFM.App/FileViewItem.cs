using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;

namespace WA1GONFM.App;

/// <summary>Presentation data shared by Details, Icons, and Compact views.</summary>
public sealed class FileViewItem : INotifyPropertyChanged
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"
    };
    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.Ordinal);
    private static readonly object CacheLock = new();
    private Bitmap? _thumbnail;
    private bool _requested;
    private IntegritySeverity _integrity = IntegritySeverity.Unverified;
    private string _integrityMessage = "Not yet verified";
    private string _detectedMime = "";
    private string _detectedType = "";
    public IntegritySeverity Integrity => _integrity;
    public string IntegrityText => _integrity switch
    {
        IntegritySeverity.HighRisk => "⚠ HIGH RISK",
        IntegritySeverity.Mismatch => "⚠ Mismatch",
        IntegritySeverity.Empty => "Empty",
        IntegritySeverity.Match => "✓",
        IntegritySeverity.Unknown => "?",
        _ => "…"
    };
    public string IntegrityMessage => _integrityMessage;
    public string DetectedMime => _detectedMime;
    public string DetectedType => _detectedType;
    public IBrush? WarningBackground => _integrity switch
    {
        IntegritySeverity.HighRisk => new SolidColorBrush(Color.FromArgb(70, 220, 38, 38)),
        IntegritySeverity.Mismatch => new SolidColorBrush(Color.FromArgb(60, 234, 179, 8)),
        _ => null
    };
    public IBrush? WarningForeground => _integrity switch
    {
        IntegritySeverity.HighRisk => Brushes.OrangeRed,
        IntegritySeverity.Mismatch => Brushes.DarkOrange,
        _ => null
    };
    public void ApplyIntegrity(IntegrityResult result)
    {
        _integrity = result.Severity;
        _integrityMessage = result.Message;
        _detectedMime = result.Mime;
        _detectedType = FileTypeIntegrity.DescribeMime(result.Mime);
        OnPropertyChanged(nameof(IntegrityText));
        OnPropertyChanged(nameof(IntegrityMessage));
        OnPropertyChanged(nameof(DetectedMime));
        OnPropertyChanged(nameof(DetectedType));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(WarningBackground));
        OnPropertyChanged(nameof(WarningForeground));
    }


    public DirectoryEntry Entry { get; }
    public string Name => Entry.Name;
    public string Glyph => Entry.IsDirectory ? "📁" : "📄";
    public string ExtensionText => Entry.IsDirectory ? "" : FilenameExtension.GetExtension(Entry.Name).TrimStart('.').ToLowerInvariant();
    public string TypeText => Entry.IsDirectory ? "Folder"
        : !string.IsNullOrWhiteSpace(_detectedType) ? _detectedType
        : string.IsNullOrWhiteSpace(ExtensionText) ? "Detecting…" : ExtensionText.ToUpperInvariant();
    public string SizeText { get; private set; }
    public string ModifiedText { get; private set; }
    public Bitmap? Thumbnail { get => _thumbnail; private set { _thumbnail = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;

    public FileViewItem(DirectoryEntry entry)
    {
        Entry = entry;
        try
        {
            if (entry.IsDirectory)
            {
                SizeText = "";
                ModifiedText = Directory.GetLastWriteTime(entry.FullPath).ToString("g");
            }
            else
            {
                var info = new FileInfo(entry.FullPath);
                SizeText = info.Length.ToString("N0");
                ModifiedText = info.LastWriteTime.ToString("g");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SizeText = "";
            ModifiedText = "";
        }
    }

    public void RefreshMetadata()
    {
        try
        {
            var size = Entry.IsDirectory ? "" : new FileInfo(Entry.FullPath).Length.ToString("N0");
            var modified = (Entry.IsDirectory
                ? Directory.GetLastWriteTime(Entry.FullPath)
                : File.GetLastWriteTime(Entry.FullPath)).ToString("g");
            if (SizeText != size) { SizeText = size; OnPropertyChanged(nameof(SizeText)); }
            if (ModifiedText != modified) { ModifiedText = modified; OnPropertyChanged(nameof(ModifiedText)); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void LoadThumbnail()
    {
        if (_requested || Entry.IsDirectory || !ImageExtensions.Contains(Path.GetExtension(Entry.Name))) return;
        _requested = true;
        var path = Entry.FullPath;
        lock (CacheLock)
        {
            if (Cache.TryGetValue(path, out var bitmap)) { Thumbnail = bitmap; return; }
        }
        _ = Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(path);
                var bitmap = Bitmap.DecodeToWidth(stream, 96);
                Dispatcher.UIThread.Post(() =>
                {
                    lock (CacheLock) Cache[path] = bitmap;
                    Thumbnail = bitmap;
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Unreadable or unsupported images retain their regular file icon.
            }
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
