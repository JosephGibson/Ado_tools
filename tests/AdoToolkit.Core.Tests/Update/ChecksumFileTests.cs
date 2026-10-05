using AdoToolkit.Core.Update;

namespace AdoToolkit.Core.Tests.Update;

// The checksum file as Install-AdoToolkit.ps1 reads it, and as New-AdoReleaseArchive writes it:
// "<lowercase hash>  <name>" and LF, UTF-8 without BOM.
public sealed class ChecksumFileTests
{
    private const string Name = "AdoToolkit-1.2.3.zip";
    private static readonly string Hash = new string('a', 32) + new string('F', 32);

    [Theory]
    [InlineData("{0}  AdoToolkit-1.2.3.zip\n")]
    [InlineData("{0} *AdoToolkit-1.2.3.zip")]
    [InlineData("{0}\tAdoToolkit-1.2.3.zip\r\n")]
    [InlineData("{0}")]
    [InlineData("\n   \r\n{0}  AdoToolkit-1.2.3.zip\nsomething else")]
    [InlineData("﻿{0}  AdoToolkit-1.2.3.zip\n")]
    public void ReadsTheHashOfTheFirstNonBlankLine(string format) =>
        Assert.Equal(Hash, Read(UpdateFixture.Utf8(string.Format(CultureInfo.InvariantCulture, format, Hash))));

    [Theory]
    [InlineData("")]
    [InlineData("\n \n")]
    [InlineData("{0}  AdoToolkit-1.2.4.zip")]
    [InlineData("{0}  adotoolkit-1.2.3.zip")]
    [InlineData("{0}  AdoToolkit-1.2.3.zip extra")]
    [InlineData("abc  AdoToolkit-1.2.3.zip")]
    [InlineData("{0}0  AdoToolkit-1.2.3.zip")]
    [InlineData("sha256:{0}  AdoToolkit-1.2.3.zip")]
    public void AnythingElseIsInvalid(string format)
    {
        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() =>
            Read(UpdateFixture.Utf8(string.Format(CultureInfo.InvariantCulture, format, Hash))));
        Assert.Equal(Messages.Get(AdoMessage.UpdateChecksumFileInvalid, UpdateFixture.Culture, Name + ".sha256", Name), error.Message);
    }

    [Fact]
    public void BytesThatAreNotUtf8AreInvalid() =>
        Assert.Throws<AdoResponseFormatException>(() => Read([0xC3, 0x28, 0x20, 0x20]));

    private static string Read(byte[] content) => ChecksumFile.ReadHash(content, Name + ".sha256", Name, UpdateFixture.Culture);
}
