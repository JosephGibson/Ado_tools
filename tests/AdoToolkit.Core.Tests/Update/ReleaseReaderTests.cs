using System.Text.Json.Nodes;
using AdoToolkit.Core.Update;

namespace AdoToolkit.Core.Tests.Update;

public sealed class ReleaseReaderTests
{
    private static readonly Version Version = new(1, 2, 3);

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("v0.11.0", "0.11.0")]
    [InlineData("v0.0.0", "0.0.0")]
    [InlineData("v999999999.0.10", "999999999.0.10")]
    public void ParsesStrictVersionTags(string tag, string expected) => Assert.Equal(Version.Parse(expected), ReleaseReader.ParseTag(tag));

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("V1.2.3")]
    [InlineData("v01.2.3")]
    [InlineData("v1.02.3")]
    [InlineData("v1.2")]
    [InlineData("v1.2.3.4")]
    [InlineData("v1.2.3-beta")]
    [InlineData("v1.2.3\n")]
    [InlineData(" v1.2.3")]
    [InlineData("v1234567890.0.0")]
    [InlineData("v١.٢.٣")]
    [InlineData("v1.2.３")]
    [InlineData("")]
    public void RefusesAnyOtherTag(string tag) => Assert.Null(ReleaseReader.ParseTag(tag));

    [Theory]
    [InlineData(AdoToolkitInstallMode.Module, "AdoToolkit-1.2.3.zip")]
    [InlineData(AdoToolkitInstallMode.Portable, "AdoToolkit-1.2.3-win-x64.zip")]
    public void FindsTheArchiveAndChecksumOfTheMode(AdoToolkitInstallMode mode, string archive)
    {
        ReleaseServer server = new(Version, mode, [1, 2, 3]);

        ReleaseReader.Release release = Read(server.Json(), mode);

        Assert.Equal(Version, release.Version);
        Assert.Equal(archive, release.Archive.Name);
        Assert.Equal(3, release.Archive.Size);
        Assert.Equal(UpdateFixture.Sha256([1, 2, 3]), release.Archive.Sha256);
        Assert.Equal(archive + ".sha256", release.Checksum.Name);
    }

    [Fact]
    public void ATagThatIsNotAVersionIsAFormatErrorShowingPrintableText()
    {
        JsonObject json = Release();
        json["tag_name"] = "latest\u0007" + new string('x', 200);

        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() => Read(json.ToJsonString()));

        Assert.Contains("latest x", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 64), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("prerelease")]
    public void ADraftOrAPrereleaseIsRefused(string property)
    {
        JsonObject json = Release();
        json[property] = true;
        Assert.Throws<AdoResponseFormatException>(() => Read(json.ToJsonString()));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{\"tag_name\":\"v1.2.3\",\"draft\":false,\"prerelease\":false}")]
    [InlineData("{\"tag_name\":1,\"draft\":false,\"prerelease\":false,\"assets\":[]}")]
    [InlineData("{\"tag_name\":\"v1.2.3\",\"draft\":false,\"prerelease\":false,\"assets\":[1]}")]
    [InlineData("{\"tag_name\":\"v1.2.3\",\"draft\":false,\"prerelease\":false,\"assets\":[{\"name\":7}]}")]
    [InlineData("{\"tag_name\":\"v1.2.3\",")]
    public void MalformedDescriptionsAreFormatErrors(string json) =>
        Assert.Throws<AdoResponseFormatException>(() => Read(json));

    [Fact]
    public void AMissingAssetIsNotFound()
    {
        ReleaseServer server = new(Version, AdoToolkitInstallMode.Module, [1]) { IncludeChecksum = false };

        AdoNotFoundException error = Assert.Throws<AdoNotFoundException>(() => Read(server.Json()));

        Assert.Contains("AdoToolkit-1.2.3.zip.sha256", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoAssetsWithOneNameAreAFormatError()
    {
        JsonObject json = Release();
        json["assets"]!.AsArray().Add(Asset("AdoToolkit-1.2.3.zip", 5, "sha256:" + new string('b', 64)));
        Assert.Throws<AdoResponseFormatException>(() => Read(json.ToJsonString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1:0123456789abcdef0123456789abcdef01234567")]
    [InlineData("sha256:0123")]
    [InlineData("SHA256:0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("sha256:000000000000000000000000000000000000000000000000000000000000000g")]
    public void AMissingOrMalformedDigestFails(string? digest)
    {
        JsonObject json = Release(archiveDigest: digest);

        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() => Read(json.ToJsonString()));

        Assert.Equal(Messages.Get(AdoMessage.UpdateDigestMissing, UpdateFixture.Culture, "AdoToolkit-1.2.3.zip"), error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ASizeBelowOneIsAFormatError(long size) =>
        Assert.Throws<AdoResponseFormatException>(() => Read(Release(archiveSize: size).ToJsonString()));

    [Fact]
    public void AnArchiveOverTheCapOfItsModeFailsBeforeAnyDownload()
    {
        UpdateHttp.Limits limits = new() { ModuleArchiveBytes = 10 };

        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() =>
            ReleaseReader.Read(UpdateFixture.Utf8(Release(archiveSize: 11).ToJsonString()), AdoToolkitInstallMode.Module, limits, UpdateFixture.Culture));

        Assert.Equal(Messages.Get(AdoMessage.UpdateAssetTooLarge, UpdateFixture.Culture, "AdoToolkit-1.2.3.zip", 11L, 10L), error.Message);
    }

    // The checksum file's cap is 1 KiB; the message gives it in bytes, not as 0 MB.
    [Fact]
    public void AChecksumFileOverItsCapNamesTheCapInBytes()
    {
        JsonObject json = Release();
        json["assets"]![1]!["size"] = 2048;

        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() => Read(json.ToJsonString()));

        Assert.Equal(Messages.Get(AdoMessage.UpdateAssetTooLarge, UpdateFixture.Culture, "AdoToolkit-1.2.3.zip.sha256", 2048L, 1024L), error.Message);
    }

    // A lone surrogate escape cannot be read as a string; that is a malformed description too.
    [Theory]
    [InlineData("{\"tag_name\":\"v1.2.3\\ud800\",\"draft\":false,\"prerelease\":false,\"assets\":[]}")]
    [InlineData("{\"tag_name\":\"v1.2.3\",\"draft\":false,\"prerelease\":false,\"assets\":[{\"name\":\"\\udc00\"}]}")]
    public void AStringThatCannotBeReadIsAFormatError(string json) =>
        Assert.Throws<AdoResponseFormatException>(() => Read(json));

    private static ReleaseReader.Release Read(string json, AdoToolkitInstallMode mode = AdoToolkitInstallMode.Module) =>
        ReleaseReader.Read(UpdateFixture.Utf8(json), mode, new UpdateHttp.Limits(), UpdateFixture.Culture);

    private static JsonObject Release(long archiveSize = 5, string? archiveDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000") =>
        new()
        {
            ["tag_name"] = "v1.2.3", ["draft"] = false, ["prerelease"] = false,
            ["assets"] = new JsonArray(Asset("AdoToolkit-1.2.3.zip", archiveSize, archiveDigest),
                Asset("AdoToolkit-1.2.3.zip.sha256", 88, "sha256:" + new string('1', 64))),
        };

    private static JsonObject Asset(string name, long size, string? digest)
    {
        JsonObject asset = new() { ["name"] = name, ["size"] = size };
        if (digest is not null) asset["digest"] = digest;
        return asset;
    }
}
