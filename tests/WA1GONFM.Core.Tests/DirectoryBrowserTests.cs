namespace WA1GONFM.Core.Tests;
public sealed class DirectoryBrowserTests
{
    [Fact] public void FiltersHiddenFilesAndDirectories()
    {
        var path = Path.Combine(Path.GetTempPath(), "wa1gonfm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            File.WriteAllText(Path.Combine(path, "visible.txt"), "hello");
            File.WriteAllText(Path.Combine(path, ".hidden"), "secret");
            Directory.CreateDirectory(Path.Combine(path, "subdir"));
            var browser = new DirectoryBrowser();
            Assert.Single(browser.List(path, false, false));
            Assert.Equal(3, browser.List(path, true, true).Count);
        }
        finally { Directory.Delete(path, true); }
    }
}
