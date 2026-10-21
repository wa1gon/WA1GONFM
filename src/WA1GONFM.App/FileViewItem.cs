using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
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

    public DirectoryEntry Entry { get; }
    public string Name => Entry.Name;
    public string Glyph => Entry.IsDirectory ? "📁" : "📄";
    public string TypeText => Entry.IsDirectory ? "Folder" : Path.GetExtension(Entry.Name).TrimStart('.').ToUpperInvariant();
    public string SizeText { get; }
    public string ModifiedText { get; }
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
