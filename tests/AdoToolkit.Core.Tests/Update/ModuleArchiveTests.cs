using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using AdoToolkit.Core.Update;
using static AdoToolkit.Core.Tests.Update.UpdateFixture;

namespace AdoToolkit.Core.Tests.Update;

// The cases of the planning spike, where Install-AdoToolkit.ps1 and a C# prototype agreed on every
// verdict, and the checks that the updater adds. Update.Pester.ps1 runs the same table through the
// installer and the cmdlet.
public sealed class ModuleArchiveTests
{
    private const string Folder = "AdoToolkit/1.2.3/";
    private static readonly Version Version = new(1, 2, 3);

    public static TheoryData<string, bool> Cases()
    {
        TheoryData<string, bool> cases = new()
        {
            { "valid", true }, { "valid-with-directories", true }, { "symlink-attribute", true },
            { "backslash", false }, { "colon-stream", false }, { "absolute", false }, { "dot-dot", false }, { "dot", false },
            { "empty-segment", false }, { "wrong-root", false }, { "root-case", false }, { "version-not-numeric", false },
            { "version-four-parts", false }, { "two-versions", false }, { "file-at-root", false }, { "extra-file", false },
            { "case-different-file", false }, { "duplicate-entry", false }, { "nonempty-directory-entry", false },
            { "oversize-entry", false }, { "manifest-version", false }, { "manifest-root-module", false }, { "manifest-unparsable", false },
            { "header-length-lie", false },
            // Stricter than the installer by design.
            { "other-version-folder", false }, { "leading-zero-folder", false }, { "unicode-digit-folder", false },
            { "dll-not-an-assembly", false }, { "dll-other-name", false }, { "dll-other-version", false }, { "too-many-entries", false },
        };
        foreach (string file in ModuleArchive.Layout) cases.Add("missing-" + file, false);
        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachCaseHasTheSpikesVerdict(string name, bool accepted)
    {
        using TestDirectory directory = new();
        string output = Path.Combine(directory.Root, "module");
        void Install()
        {
            using ZipArchive zip = Open(Case(name));
            ModuleArchive.Extract(zip, Version, output, new UpdateHttp.Limits { EntryBytes = 4096, ModuleEntries = 16 }, "AdoToolkit-1.2.3.zip", Culture);
            ModuleArchive.Check(output, Version, new Version(7, 6, 6), ReadManifest, "AdoToolkit-1.2.3.zip", Culture);
        }

        if (!accepted)
        {
            Assert.ThrowsAny<AdoException>(Install);
            return;
        }
        Install();
        foreach ((string file, byte[] data) in ModuleFiles(Version)) Assert.Equal(data, File.ReadAllBytes(Path.Combine(output, file)));
        Assert.Equal(ModuleArchive.Layout.Order(StringComparer.Ordinal), Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(output, path).Replace('\\', '/')).Order(StringComparer.Ordinal));
    }

    // Version.TryParse allows white space around each part; the message shows the parsed version.
    [Theory]
    [InlineData("7.7")]
    [InlineData(" 7.7\t")]
    public void AManifestThatRequiresANewerPowerShellIsAConfigurationError(string required)
    {
        using TestDirectory directory = new();
        string output = Path.Combine(directory.Root, "module");
        using ZipArchive zip = Open(Zip(ModuleItems(Version, Manifest(Version, powerShell: required))));
        ModuleArchive.Extract(zip, Version, output, new UpdateHttp.Limits(), "AdoToolkit-1.2.3.zip", Culture);

        AdoConfigurationException error = Assert.Throws<AdoConfigurationException>(() =>
            ModuleArchive.Check(output, Version, new Version(7, 6, 6), ReadManifest, "AdoToolkit-1.2.3.zip", Culture));

        Assert.Equal(Messages.Get(AdoMessage.UpdatePowerShellTooOld, Culture, "1.2.3", "7.7", "7.6.6"), error.Message);
        // Portable mode brings its own PowerShell and does not check.
        ModuleArchive.Check(output, Version, null, ReadManifest, "AdoToolkit-1.2.3.zip", Culture);
    }

    // A central directory that understates a length: .NET stops the entry at the declared size, as it
    // does for the installer, and the updater never writes past it either. A length that overstates
    // is the header-length-lie case, refused as incomplete.
    [Fact]
    public void AnEntryIsNeverWrittenPastItsDeclaredLength()
    {
        using TestDirectory directory = new();
        byte[] lying = WithCentralSize(Zip(ModuleItems(Version)), Folder + "AdoToolkit.Format.ps1xml", 3);
        using ZipArchive zip = Open(lying);

        Exception? error = Record.Exception(() =>
            ModuleArchive.Extract(zip, Version, Path.Combine(directory.Root, "module"), new UpdateHttp.Limits(), "AdoToolkit-1.2.3.zip", Culture));

        Assert.True(error is null or AdoResponseFormatException, error?.ToString());
        string written = Path.Combine(directory.Root, "module", "AdoToolkit.Format.ps1xml");
        Assert.True(!File.Exists(written) || new FileInfo(written).Length <= 3);
    }

    [Fact]
    public void AValidExistingFolderIsInstalled()
    {
        using TestDirectory directory = new();
        string folder = Path.Combine(directory.Root, "1.2.3");
        Assert.Equal(ModuleArchive.FolderState.Missing, ModuleArchive.Inspect(folder, Version, ReadManifest, Culture));
        WriteFiles(folder, ModuleFiles(Version));
        Assert.Equal(ModuleArchive.FolderState.Valid, ModuleArchive.Inspect(folder, Version, ReadManifest, Culture));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("manifest")]
    [InlineData("truncated")]
    [InlineData("relabeled")]
    public void AnExistingFolderThatWouldFailAnInstallIsIncomplete(string damage)
    {
        using TestDirectory directory = new();
        string folder = Path.Combine(directory.Root, "1.2.3");
        WriteFiles(folder, ModuleFiles(Version));
        string core = Path.Combine(folder, "AdoToolkit.Core.dll");
        switch (damage)
        {
            case "missing": File.Delete(Path.Combine(folder, "fr", "AdoToolkit.PowerShell.dll-Help.xml")); break;
            case "extra": File.WriteAllText(Path.Combine(folder, "notes.txt"), "x"); break;
            case "manifest": File.WriteAllText(Path.Combine(folder, "AdoToolkit.psd1"), Manifest(new Version(1, 2, 4))); break;
            // A cut copy keeps its name, and the manifest still reads as valid.
            case "truncated": File.WriteAllBytes(core, File.ReadAllBytes(core)[..200]); break;
            case "relabeled": File.WriteAllBytes(core, Assembly("AdoToolkit.Core", new Version(1, 2, 4, 0))); break;
        }

        Assert.Equal(ModuleArchive.FolderState.Incomplete, ModuleArchive.Inspect(folder, Version, ReadManifest, Culture));
    }

    [Fact]
    public void AnExistingFolderWithALinkInsideIsRefused()
    {
        using TestDirectory directory = new();
        string folder = Path.Combine(directory.Root, "1.2.3");
        WriteFiles(folder, ModuleFiles(Version));
        Directory.CreateDirectory(Path.Combine(directory.Root, "elsewhere"));
        string link = Path.Combine(folder, "fr", "linked");
        CreateJunction(link, Path.Combine(directory.Root, "elsewhere"));

        AdoConfigurationException error = Assert.Throws<AdoConfigurationException>(() => ModuleArchive.Inspect(folder, Version, ReadManifest, Culture));

        Assert.Equal(Messages.Get(AdoMessage.UpdateLinkedPath, Culture, link), error.Message);
        Directory.Delete(link);
    }

    // A subfolder that the user may not list, as one that another account made can be: the folder is
    // named in a file error, not left as a raw I/O error.
    [Fact]
    [SupportedOSPlatform("windows")]
    public void AnExistingFolderThatCannotBeReadIsNamed()
    {
        using TestDirectory directory = new();
        string folder = Path.Combine(directory.Root, "1.2.3");
        WriteFiles(folder, ModuleFiles(Version));
        DirectoryInfo locked = Directory.CreateDirectory(Path.Combine(folder, "fr", "locked"));
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        FileSystemAccessRule deny = new(identity.User!, FileSystemRights.ListDirectory, AccessControlType.Deny);
        DirectorySecurity security = locked.GetAccessControl();
        security.AddAccessRule(deny);
        locked.SetAccessControl(security);
        try
        {
            AdoFileOutputException error = Assert.Throws<AdoFileOutputException>(() => ModuleArchive.Inspect(folder, Version, ReadManifest, Culture));

            Assert.Equal(Messages.Get(AdoMessage.UpdateFolderUnreadable, Culture, folder), error.Message);
            Assert.IsType<UnauthorizedAccessException>(error.InnerException);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            locked.SetAccessControl(security);
        }
    }

    [Fact]
    public void AFileWhereTheFolderGoesIsRefused()
    {
        using TestDirectory directory = new();
        string target = Path.Combine(directory.Root, "1.2.3");
        File.WriteAllText(target, "x");
        Assert.Throws<AdoFileOutputException>(() => ModuleArchive.Inspect(target, Version, ReadManifest, Culture));
    }

    private static byte[] Case(string name)
    {
        List<ZipItem> valid = ModuleItems(Version);
        List<ZipItem> Replace(string file, ZipItem item) => [.. valid.Where(entry => entry.Name != Folder + file), item];
        List<ZipItem> Rename(Func<string, string> rename) => [.. valid.Select(item => item with { Name = rename(item.Name) })];
        if (name.StartsWith("missing-", StringComparison.Ordinal))
            return Zip(valid.Where(item => item.Name != Folder + name["missing-".Length..]));
        return name switch
        {
            "valid" => Zip(valid),
            "valid-with-directories" => Zip([new("AdoToolkit/"), new(Folder), new(Folder + "fr/"), .. valid]),
            "symlink-attribute" => Zip(Replace("AdoToolkit.Format.ps1xml", new(Folder + "AdoToolkit.Format.ps1xml",
                Utf8("<Configuration><ViewDefinitions /></Configuration>"), unchecked((int)(0xA1FFu << 16))))),
            "backslash" => Zip([.. valid, new("AdoToolkit\\1.2.3\\x.dll", Utf8("x"))]),
            "colon-stream" => Zip([.. valid, new(Folder + "AdoToolkit.psd1:evil", Utf8("x"))]),
            "absolute" => Zip([.. valid, new("/" + Folder + "x.dll", Utf8("x"))]),
            "dot-dot" => Zip([.. valid, new(Folder + "../1.2.3/x.dll", Utf8("x"))]),
            "dot" => Zip([.. valid, new(Folder + "./x.dll", Utf8("x"))]),
            "empty-segment" => Zip([.. valid, new(Folder + "/x.dll", Utf8("x"))]),
            "wrong-root" => Zip(Rename(item => "Other/" + item["AdoToolkit/".Length..])),
            "root-case" => Zip(Rename(item => "adotoolkit/" + item["AdoToolkit/".Length..])),
            "version-not-numeric" => Zip(Rename(item => item.Replace("1.2.3", "latest", StringComparison.Ordinal))),
            "version-four-parts" => Zip(Rename(item => item.Replace("1.2.3", "1.2.3.4", StringComparison.Ordinal))),
            "two-versions" => Zip([.. valid, new("AdoToolkit/1.2.4/AdoToolkit.Core.dll", Utf8("x"))]),
            "file-at-root" => Zip([.. valid, new("AdoToolkit/readme.txt", Utf8("x"))]),
            "extra-file" => Zip([.. valid, new(Folder + "extra.dll", Utf8("x"))]),
            "case-different-file" => Zip(Rename(item => item.Replace("AdoToolkit.Format", "adotoolkit.format", StringComparison.Ordinal))),
            "duplicate-entry" => Zip([.. valid, new(Folder + "AdoToolkit.Core.dll", Utf8("second copy"))]),
            "nonempty-directory-entry" => Zip([.. valid, new(Folder + "fr/", Utf8("x"))]),
            "oversize-entry" => Zip(Replace("AdoToolkit.Format.ps1xml", new(Folder + "AdoToolkit.Format.ps1xml", Size: 4097))),
            "manifest-version" => Zip(ModuleItems(Version, Manifest(new Version(1, 2, 4)))),
            "manifest-root-module" => Zip(ModuleItems(Version, Manifest(Version, rootModule: "Other.dll"))),
            "manifest-unparsable" => Zip(ModuleItems(Version, "@{ ModuleVersion = ")),
            "header-length-lie" => WithCentralSize(Zip(valid), Folder + "AdoToolkit.Format.ps1xml", 999),
            "other-version-folder" => Zip(ModuleItems(new Version(1, 2, 4))),
            "leading-zero-folder" => Zip(Rename(item => item.Replace("1.2.3/", "01.2.3/", StringComparison.Ordinal))),
            "unicode-digit-folder" => Zip(Rename(item => item.Replace("1.2.3/", "1.2.٣/", StringComparison.Ordinal))),
            "dll-not-an-assembly" => Zip(Replace("AdoToolkit.Core.dll", new(Folder + "AdoToolkit.Core.dll", Utf8("MZ not an assembly")))),
            "dll-other-name" => Zip(Replace("AdoToolkit.Core.dll", new(Folder + "AdoToolkit.Core.dll", Assembly("Other", new Version(1, 2, 3, 0))))),
            "dll-other-version" => Zip(Replace("AdoToolkit.PowerShell.dll", new(Folder + "AdoToolkit.PowerShell.dll",
                Assembly("AdoToolkit.PowerShell", new Version(1, 2, 2, 0))))),
            "too-many-entries" => Zip([.. Enumerable.Range(0, 10).Select(index => new ZipItem("AdoToolkit/d" + index.ToString(CultureInfo.InvariantCulture) + "/")), .. valid]),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }
}
