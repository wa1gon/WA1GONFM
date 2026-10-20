namespace WA1GONFM.Core.Tests;
public sealed class OverwriteTests
{
    [Fact]
    public void CopyBetweenTabsCanOverwriteWhenAllowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "wa1gonfm-v6-" + Guid.NewGuid());
        var from = Path.Combine(root, "from");
        var to = Path.Combine(root, "to");
        Directory.CreateDirectory(from); Directory.CreateDirectory(to);
        try
        {
            File.WriteAllText(Path.Combine(from, "test.txt"), "new");
            File.WriteAllText(Path.Combine(to, "test.txt"), "old");
            var ops = new FileOperations();
            Assert.Throws<IOException>(() => ops.Copy(Path.Combine(from, "test.txt"), to));
            ops.Copy(Path.Combine(from, "test.txt"), to, overwrite: true);
            Assert.Equal("new", File.ReadAllText(Path.Combine(to, "test.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void MoveBetweenTabsCanOverwriteWhenAllowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "wa1gonfm-v6-" + Guid.NewGuid());
        var from = Path.Combine(root, "from");
        var to = Path.Combine(root, "to");
        Directory.CreateDirectory(from); Directory.CreateDirectory(to);
        try
        {
            var source = Path.Combine(from, "test.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(Path.Combine(to, "test.txt"), "old");
            var ops = new FileOperations();
            ops.Move(source, to, overwrite: true);
            Assert.False(File.Exists(source));
            Assert.Equal("new", File.ReadAllText(Path.Combine(to, "test.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void CopyIntoSameFolderIsRejectedEvenWithOverwrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "wa1gonfm-v6-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "test.txt");
            File.WriteAllText(source, "original");
            Assert.Throws<IOException>(() => new FileOperations().Copy(source, root, overwrite: true));
            Assert.Equal("original", File.ReadAllText(source));
        }
        finally { Directory.Delete(root, true); }
    }
}
