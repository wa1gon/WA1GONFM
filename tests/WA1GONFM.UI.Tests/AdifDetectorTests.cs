using System.Text;

namespace WA1GONFM.UI.Tests;

public sealed class AdifDetectorTests
{
    [Fact]
    public void ValidAdifLogIsRecognized()
    {
        var text = "<ADIF_VER:5>3.1.5<PROGRAMID:5>ACLog<EOH>" +
                   "<CALL:6>WA1GON<QSO_DATE:8>20261022<BAND:3>20M<MODE:3>SSB<EOR>";
        Assert.True(AdifDetector.IsAdif(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void CaseInsensitiveTagsAreRecognized()
    {
        var text = "<adif_ver:5>3.1.5<eoh><call:6>WA1GON<mode:3>SSB<eor>";
        Assert.True(AdifDetector.IsAdif(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData("<CALL:100>WA1GON<MODE:3>SSB<EOR>")]
    [InlineData("<CALL:6>WA1GON<MODE:3>SSB")]
    [InlineData("<CALL:6>WA1GON<MODE:3>SSB<EOH>")]
    [InlineData("Not an ADIF file")]
    [InlineData("<CALL:bad>WA1GON<EOR>")]
    public void InvalidOrIncompleteAdifIsRejected(string text)
    {
        Assert.False(AdifDetector.IsAdif(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void BinaryContentIsRejected()
    {
        Assert.False(AdifDetector.IsAdif(new byte[] { 0xFF, 0x00, 0xAB, 0x12 }));
    }

    [Fact]
    public void ConfirmedAdifGetsReadableDescription()
    {
        Assert.Equal("Amateur Radio ADIF Log", MimeDescriptionResolver.Describe("application/x-adif"));
    }
}
