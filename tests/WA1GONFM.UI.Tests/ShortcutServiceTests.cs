using WA1GONFM.Infrastructure;

namespace WA1GONFM.UI.Tests;

public sealed class ShortcutServiceTests
{
    [Fact]
    public void RemovingCustomShortcutDoesNotDeleteDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "wagonfm-shortcut-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            var settings = new AppSettings();
            settings.Shortcuts.Add(path);
            Assert.True(ShortcutService.Remove(settings, path, "/home/tester"));
            Assert.DoesNotContain(path, settings.Shortcuts);
            Assert.True(Directory.Exists(path));
        }
        finally { Directory.Delete(path); }
    }

    [Theory]
    [InlineData("/home/tester")]
    [InlineData("/home/tester/Downloads")]
    [InlineData("/home/tester/Documents")]
    public void BuiltInShortcutsCannotBeRemoved(string path)
    {
        var settings = new AppSettings();
        settings.Shortcuts.Add(path);
        Assert.False(ShortcutService.Remove(settings, path, "/home/tester"));
        Assert.Contains(path, settings.Shortcuts);
    }
}
