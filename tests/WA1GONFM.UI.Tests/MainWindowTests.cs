namespace WA1GONFM.UI.Tests;
public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void WindowHasExpectedTitle()
    {
        var window = new MainWindow();
        Assert.Equal("WA1GONFM", window.Title);
        Assert.NotNull(window.FindControl<Avalonia.Controls.Primitives.ToggleButton>("HiddenToggle"));
        Assert.NotNull(window.FindControl<Avalonia.Controls.TabControl>("Tabs"));
        window.Close();
    }
}
