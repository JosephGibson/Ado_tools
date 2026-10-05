using AdoToolkit.Core.Update;

namespace AdoToolkit.Core.Tests.Update;

public sealed class InstallationDetectionTests
{
    private static readonly Version Version = new(1, 2, 3);

    [Fact]
    public void AVersionFolderUnderAdoToolkitOnPSModulePathIsModuleOnly()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = UpdateFixture.ModuleInstall(directory, Version);

        ToolkitInstallation installation = ToolkitInstallation.Detect(context, UpdateFixture.Culture);

        Assert.Equal(AdoToolkitInstallMode.Module, installation.Mode);
        Assert.Equal(context.ModuleBase, installation.Folder);
        Assert.Equal(Path.Combine(directory.Root, "Modules", "AdoToolkit"), installation.Root);
        Assert.Equal(Path.Combine(directory.Root, "Modules", "AdoToolkit", "1.2.4"), installation.TargetFor(new Version(1, 2, 4)));
    }

    [Fact]
    public void PSModulePathIsComparedLikeTheInstallerDoes()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = UpdateFixture.ModuleInstall(directory, Version, onModulePath: false);
        string modules = Path.Combine(directory.Root, "Modules");
        ToolkitInstallContext variant = new()
        {
            ModuleBase = context.ModuleBase + Path.DirectorySeparatorChar, ModuleVersion = Version, PowerShellVersion = context.PowerShellVersion,
            ModulePaths = ["", "  ", "C:\\bad\0path", " " + modules.ToUpperInvariant() + Path.DirectorySeparatorChar + " "],
        };

        Assert.Equal(AdoToolkitInstallMode.Module, ToolkitInstallation.Detect(variant, UpdateFixture.Culture).Mode);
    }

    [Fact]
    public void TheSameShapeOffPSModulePathIsRefusedLikeTheGateStaging()
    {
        using TestDirectory directory = new();
        string staged = Path.Combine(directory.Root, "artifacts", "verify", "AdoToolkit", "1.2.3");
        UpdateFixture.WriteFiles(staged, UpdateFixture.ModuleFiles(Version));
        ToolkitInstallContext context = new()
        {
            ModuleBase = staged, ModuleVersion = Version, PowerShellVersion = new Version(7, 6, 6),
            ModulePaths = [Path.Combine(directory.Root, "artifacts")],
        };

        AdoConfigurationException error = Assert.Throws<AdoConfigurationException>(() => ToolkitInstallation.Detect(context, UpdateFixture.Culture));

        Assert.Equal(Messages.Get(AdoMessage.UpdateUnsupportedLayout, UpdateFixture.Culture, staged,
            "https://github.com/JosephGibson/Ado_tools#installation"), error.Message);
    }

    public static TheoryData<string, string?> RefusedLayouts => new()
    {
        { "Modules/AdoToolkit/1.2.4", "1.2.3" },
        { "Modules/Other/1.2.3", "1.2.3" },
        { "src/AdoToolkit.PowerShell/bin/Release/net10.0", "1.2.3" },
        { "Modules/AdoToolkit/1.2.3", null },
        { "Modules/AdoToolkit/1.2.3.0", "1.2.3.0" },
        { "Portable/AdoToolkit-1.2.3-win-x64/notmodule", "1.2.3" },
    };

    [Theory]
    [MemberData(nameof(RefusedLayouts))]
    public void AnyOtherLayoutIsRefusedBeforeAnyRequest(string relative, string? version)
    {
        using TestDirectory directory = new();
        string folder = Path.Combine(directory.Root, relative);
        Directory.CreateDirectory(folder);
        ToolkitInstallContext context = new()
        {
            ModuleBase = folder, ModuleVersion = version is null ? null : Version.Parse(version), PowerShellVersion = new Version(7, 6, 6),
            ModulePaths = [Path.Combine(directory.Root, "Modules"), Path.Combine(directory.Root, "src")],
        };

        Assert.Throws<AdoConfigurationException>(() => ToolkitInstallation.Detect(context, UpdateFixture.Culture));
    }

    [Fact]
    public void AMissingModuleBaseIsRefused() =>
        Assert.Throws<AdoConfigurationException>(() => ToolkitInstallation.Detect(
            new ToolkitInstallContext { ModuleBase = null, ModuleVersion = Version, PowerShellVersion = new Version(7, 6) }, UpdateFixture.Culture));

    [Fact]
    public void AModuleFolderBesideTheLauncherAndRuntimeIsPortable()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = UpdateFixture.PortableInstall(directory, Version);

        ToolkitInstallation installation = ToolkitInstallation.Detect(context, UpdateFixture.Culture);

        Assert.Equal(AdoToolkitInstallMode.Portable, installation.Mode);
        Assert.Equal(Path.Combine(directory.Root, "Portable", "AdoToolkit-1.2.3-win-x64"), installation.Folder);
        Assert.Equal(Path.Combine(directory.Root, "Portable"), installation.Root);
        Assert.Equal(Path.Combine(directory.Root, "Portable", "AdoToolkit-2.0.0-win-x64"), installation.TargetFor(new Version(2, 0, 0)));
    }

    [Theory]
    [InlineData("Start-AdoToolkit.cmd")]
    [InlineData("runtime/pwsh.exe")]
    public void APortableFolderWithoutItsLauncherOrRuntimeIsRefused(string missing)
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = UpdateFixture.PortableInstall(directory, Version);
        File.Delete(Path.Combine(directory.Root, "Portable", "AdoToolkit-1.2.3-win-x64", missing));

        Assert.Throws<AdoConfigurationException>(() => ToolkitInstallation.Detect(context, UpdateFixture.Culture));
    }

    [Fact]
    public void AModuleFolderAtADriveRootIsRefused()
    {
        string root = Path.GetPathRoot(Path.GetTempPath())!;
        ToolkitInstallContext context = new()
        {
            ModuleBase = Path.Combine(root, "module"), ModuleVersion = Version, PowerShellVersion = new Version(7, 6, 6), ModulePaths = [root],
        };

        Assert.Throws<AdoConfigurationException>(() => ToolkitInstallation.Detect(context, UpdateFixture.Culture));
    }

    [Fact]
    public void AnInstallPathThroughAJunctionIsRefused()
    {
        using TestDirectory directory = new();
        string real = Path.Combine(directory.Root, "Real");
        UpdateFixture.WriteFiles(Path.Combine(real, "AdoToolkit", "1.2.3"), UpdateFixture.ModuleFiles(Version));
        string linked = Path.Combine(directory.Root, "Linked");
        UpdateFixture.CreateJunction(linked, real);
        ToolkitInstallContext context = new()
        {
            ModuleBase = Path.Combine(linked, "AdoToolkit", "1.2.3"), ModuleVersion = Version, PowerShellVersion = new Version(7, 6, 6),
            ModulePaths = [linked],
        };

        AdoConfigurationException error = Assert.Throws<AdoConfigurationException>(() => ToolkitInstallation.Detect(context, UpdateFixture.Culture));

        Assert.Equal(Messages.Get(AdoMessage.UpdateLinkedPath, UpdateFixture.Culture, linked), error.Message);
        Directory.Delete(linked);
    }

    // The new folder, the lock and staging go beside the portable folder, so its parent is checked too.
    [Fact]
    public void APortableFolderWhoseParentIsAJunctionIsRefused()
    {
        using TestDirectory directory = new();
        UpdateFixture.PortableInstall(directory, Version);
        string linked = Path.Combine(directory.Root, "Linked");
        UpdateFixture.CreateJunction(linked, Path.Combine(directory.Root, "Portable"));
        ToolkitInstallContext context = new()
        {
            ModuleBase = Path.Combine(linked, "AdoToolkit-1.2.3-win-x64", "module"), ModuleVersion = Version, PowerShellVersion = new Version(7, 6, 6),
            ModulePaths = [],
        };

        AdoConfigurationException error = Assert.Throws<AdoConfigurationException>(() => ToolkitInstallation.Detect(context, UpdateFixture.Culture));

        Assert.Equal(Messages.Get(AdoMessage.UpdateLinkedPath, UpdateFixture.Culture, linked), error.Message);
        Directory.Delete(linked);
    }
}
