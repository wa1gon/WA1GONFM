namespace WA1GONFM.Infrastructure;
public sealed class AppSettings
{
    public string TerminalExecutable { get; set; } = "x-terminal-emulator";
    public string TerminalArguments { get; set; } = "";
    public string Theme { get; set; } = "System";
    public bool ShowHidden { get; set; }
    public bool ShowDirectories { get; set; } = true;
    public System.Collections.Generic.List<string> Shortcuts { get; set; } = new();
}
