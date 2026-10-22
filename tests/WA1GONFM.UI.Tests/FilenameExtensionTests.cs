namespace WA1GONFM.UI.Tests;

public sealed class FilenameExtensionTests
{
    [Theory]
    [InlineData(".bash_history", "")]
    [InlineData(".gitignore", "")]
    [InlineData(".env", "")]
    [InlineData(".config.json", ".json")]
    [InlineData("archive.tar.gz", ".gz")]
    [InlineData("notes.txt", ".txt")]
    [InlineData("LICENSE", "")]
    [InlineData("filename.", "")]
    [InlineData(".bash_history.txt", ".txt")]
    public void ReturnsExpectedExtension(string filename, string extension)
    {
        Assert.Equal(extension, FilenameExtension.GetExtension(filename));
    }
}
