using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace WA1GONFM.App;
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    private static string? ResolveStartupDirectory(string[]? args)
    {
        if (args is null || args.Length == 0) return null;
        try
        {
            var argument = args[0];
            string path;
            if (Uri.TryCreate(argument, UriKind.Absolute, out var uri) && uri.IsFile)
                path = uri.LocalPath;
            else if (argument.Contains("://", StringComparison.Ordinal))
                return null; // Unsupported URI scheme
            else
                path = argument;

            path = Path.GetFullPath(path);
            return Directory.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException or UriFormatException)
        {
            return null;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var directory = ResolveStartupDirectory(desktop.Args);
            desktop.MainWindow = new MainWindow(directory);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
