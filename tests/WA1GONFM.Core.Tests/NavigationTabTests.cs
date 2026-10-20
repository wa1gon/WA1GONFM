namespace WA1GONFM.Core.Tests;
public sealed class NavigationTabTests
{
    [Fact] public void BackAndForwardRestoreDirectory()
    {
        var tab = new NavigationTab(Path.GetTempPath());
        tab.Navigate("/");
        Assert.Equal("/", tab.CurrentPath);
        tab.Back();
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()), tab.CurrentPath);
        tab.Forward();
        Assert.Equal("/", tab.CurrentPath);
    }
    [Fact] public void UpFromRootDoesNotNavigate() { var tab = new NavigationTab("/"); tab.Up(); Assert.Equal("/", tab.CurrentPath); }
}
