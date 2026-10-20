namespace WA1GONFM.Core.Tests;
public sealed class FileOperationsTests
{
    [Fact]
    public void CopyMoveRenameDeleteFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "wa1gonfm-tests-" + Guid.NewGuid());
        var src = Path.Combine(root, "source"); var dest = Path.Combine(root, "dest");
        Directory.CreateDirectory(src); Directory.CreateDirectory(dest);
        try
        {
            File.WriteAllText(Path.Combine(src, "hello.txt"), "hello");
            var ops = new FileOperations();
            ops.Copy(Path.Combine(src, "hello.txt"), dest);
            Assert.True(File.Exists(Path.Combine(dest, "hello.txt")));
            ops.Rename(Path.Combine(dest, "hello.txt"), "renamed.txt");
            Assert.True(File.Exists(Path.Combine(dest, "renamed.txt")));
            ops.Move(Path.Combine(dest, "renamed.txt"), src);
            Assert.True(File.Exists(Path.Combine(src, "renamed.txt")));
            ops.Delete(Path.Combine(src, "renamed.txt"));
            Assert.False(File.Exists(Path.Combine(src, "renamed.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void RejectsDuplicateDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "wa1gonfm-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "a.txt"); File.WriteAllText(file, "a");
            Assert.Throws<IOException>(() => new FileOperations().Copy(file, root));
        }
        finally { Directory.Delete(root, true); }
    }
}
