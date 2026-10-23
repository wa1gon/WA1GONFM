namespace WA1GONFM.Infrastructure;
public sealed class AppSettings
{
    public string TerminalExecutable { get; set; } = "x-terminal-emulator";
    public string TerminalArguments { get; set; } = "";
    public string Theme { get; set; } = "System";
    public string SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; }
    public bool FoldersFirst { get; set; } = true;
    public string ClickMode { get; set; } = "Single";
    public string DefaultViewMode { get; set; } = "Details";
    public bool ConfirmMove { get; set; } = false;
    public bool ConfirmOverwrite { get; set; } = true;
    public bool ConfirmTrash { get; set; } = false;
    public bool ConfirmPermanentDelete { get; set; } = true;
    public bool AutoRefresh { get; set; } = true;
    public bool ShowHidden { get; set; }
    public bool ShowDirectories { get; set; } = true;
    public System.Collections.Generic.List<string> Shortcuts { get; set; } = new();
}
