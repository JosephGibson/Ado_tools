using System.IO.Compression;
using AdoToolkit.Core.Update;
using static AdoToolkit.Core.Tests.Update.UpdateFixture;

namespace AdoToolkit.Core.Tests.Update;

// The portable zip as New-AdoPortableArchive writes it, and the entry rules that
// tools/package/Portable.Common.ps1 applies to the PowerShell archive.
public sealed class PortableArchiveTests
{
    private const string Name = "AdoToolkit-1.2.3-win-x64.zip";
    private static readonly Version Version = new(1, 2, 3);

    [Fact]
    public void AValidBundleIsExtractedWhole()
    {
        using TestDirectory directory = new();
        string output = Path.Combine(directory.Root, "bundle");

        Extract(Zip(PortableItems(Version)), output);

        foreach (ZipItem item in PortableItems(Version)) Assert.Equal(item.Data, File.ReadAllBytes(Path.Combine(output, item.Name)));
        Assert.Equal(PortableItems(Version).Count, Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Count());
    }

    [Theory]
    [InlineData("runtime/a\\b.dll")]
    [InlineData("runtime/a:b.dll")]
    [InlineData("runtime/a<b.dll")]
    [InlineData("runtime/a|b.dll")]
    [InlineData("runtime/a?b.dll")]
    [InlineData("runtime/a\u0001b.dll")]
    [InlineData("/runtime/x.dll")]
    [InlineData("runtime/./x.dll")]
    [InlineData("runtime/../x.dll")]
    [InlineData("runtime//x.dll")]
    [InlineData("runtime/x.")]
    [InlineData("runtime/x ")]
    [InlineData("runtime/CON")]
    [InlineData("runtime/con.txt")]
    [InlineData("runtime/LPT1.dll")]
    [InlineData("runtime/Modules/NUL/x.psd1")]
    [InlineData("other.txt")]
    [InlineData("tools/x.ps1")]
    [InlineData("runtime/PWSH.EXE")]
    public void AnUnsafeUnexpectedOrDuplicateEntryIsRefused(string entry)
    {
        using TestDirectory directory = new();
        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() =>
            Extract(Zip([.. PortableItems(Version), new(entry, Utf8("x"))]), Path.Combine(directory.Root, "bundle")));
        Assert.StartsWith(Messages.Get(AdoMessage.UpdateArchiveEntry, Culture, Name, "").TrimEnd(), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("symlink")]
    [InlineData("collision")]
    [InlineData("nonempty-directory")]
    [InlineData("unknown-directory")]
    public void LinksCollisionsAndStrayFoldersAreRefused(string kind)
    {
        using TestDirectory directory = new();
        List<ZipItem> items = PortableItems(Version);
        switch (kind)
        {
            case "symlink": items.Add(new ZipItem("runtime/link.dll", Utf8("target"), unchecked((int)(0xA1FFu << 16)))); break;
            case "collision": items.Add(new ZipItem("runtime/pwsh.exe/inner.txt", Utf8("x"))); break;
            case "nonempty-directory": items.Add(new ZipItem("runtime/Modules/", Utf8("x"))); break;
            case "unknown-directory": items.Add(new ZipItem("tools/")); break;
        }

        Assert.Throws<AdoResponseFormatException>(() => Extract(Zip(items), Path.Combine(directory.Root, "bundle")));
    }

    [Theory]
    [InlineData("module/AdoToolkit.psd1")]
    [InlineData("runtime/pwsh.exe")]
    [InlineData("runtime/hostfxr.dll")]
    [InlineData("README.txt")]
    [InlineData("Start-AdoToolkit.cmd")]
    public void AMissingFileOfTheLayoutIsRefused(string missing)
    {
        using TestDirectory directory = new();
        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() =>
            Extract(Zip(PortableItems(Version).Where(item => item.Name != missing)), Path.Combine(directory.Root, "bundle")));
        Assert.Equal(Messages.Get(AdoMessage.UpdateArchiveLayout, Culture, Name), error.Message);
    }

    [Fact]
    public void AnExtraModuleFileIsRefused()
    {
        using TestDirectory directory = new();
        Assert.Throws<AdoResponseFormatException>(() =>
            Extract(Zip([.. PortableItems(Version), new("module/extra.dll", Utf8("x"))]), Path.Combine(directory.Root, "bundle")));
    }

    [Fact]
    public void AModuleDllWithoutTheReleaseIdentityIsRefused()
    {
        using TestDirectory directory = new();
        List<ZipItem> items = [.. PortableItems(Version).Where(item => item.Name != "module/AdoToolkit.Core.dll"),
            new("module/AdoToolkit.Core.dll", Assembly("AdoToolkit.Core", new Version(1, 2, 2, 0)))];

        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() => Extract(Zip(items), Path.Combine(directory.Root, "bundle")));

        Assert.Equal(Messages.Get(AdoMessage.UpdateArchiveAssembly, Culture, Name, "1.2.3", "AdoToolkit.Core.dll"), error.Message);
    }

    [Theory]
    [InlineData("{\"ModuleVersion\":\"1.2.4\",\"Architecture\":\"win-x64\"}")]
    [InlineData("{\"ModuleVersion\":\"1.2.3\",\"Architecture\":\"arm64\"}")]
    [InlineData("{\"ModuleVersion\":1.2,\"Architecture\":\"win-x64\"}")]
    [InlineData("{\"Architecture\":\"win-x64\"}")]
    [InlineData("[]")]
    [InlineData("{\"ModuleVersion\":")]
    [InlineData("{\"ModuleVersion\":\"\\ud800\",\"Architecture\":\"win-x64\"}")]
    public void ABundleDescriptionOfAnotherReleaseIsRefused(string bundle)
    {
        using TestDirectory directory = new();
        AdoResponseFormatException error = Assert.Throws<AdoResponseFormatException>(() =>
            Extract(Zip(PortableItems(Version, bundle)), Path.Combine(directory.Root, "bundle")));
        Assert.Equal(Messages.Get(AdoMessage.UpdateManifestMismatch, Culture, Name, "1.2.3"), error.Message);
    }

    [Fact]
    public void EachCapIsEnforced()
    {
        using TestDirectory directory = new();
        string output = Path.Combine(directory.Root, "bundle");
        byte[] zip = Zip(PortableItems(Version));
        int entries = PortableItems(Version).Count;

        Assert.Throws<AdoResponseFormatException>(() => Extract(zip, output, new UpdateHttp.Limits { PortableEntries = entries - 1 }));
        Assert.Throws<AdoResponseFormatException>(() => Extract(zip, output, new UpdateHttp.Limits { EntryBytes = 100 }));
        Assert.Throws<AdoResponseFormatException>(() => Extract(zip, output, new UpdateHttp.Limits { PortableBytes = 1000 }));
        Assert.Throws<AdoResponseFormatException>(() => Extract(Zip(PortableItems(Version, Bundle(Version) + new string(' ', 5000))), output));
        Assert.False(Directory.Exists(output));
    }

    private static void Extract(byte[] zip, string output, UpdateHttp.Limits? limits = null)
    {
        using ZipArchive archive = Open(zip);
        PortableArchive.Extract(archive, Version, output, limits ?? new UpdateHttp.Limits(), Name, ReadManifest, Culture);
    }
}
