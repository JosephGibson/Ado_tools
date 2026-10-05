using System.Net;
using System.Net.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Update;
using static AdoToolkit.Core.Tests.Update.UpdateFixture;

namespace AdoToolkit.Core.Tests.Update;

// Update-AdoToolkit end to end, against a fake GitHub that redirects downloads as GitHub does.
public sealed class ToolkitUpdaterTests
{
    private static readonly Version Running = new(1, 2, 3);
    private static readonly Version Next = new(1, 2, 4);

    [Fact]
    public async Task UpToDateReadsOnlyTheReleaseAndWritesNothing()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        ReleaseServer server = new(Running, AdoToolkitInstallMode.Module, Zip(ModuleItems(Running)));
        Dictionary<string, string> before = Snapshot(directory.Root);

        ToolkitUpdatePlan plan = await Prepare(server, context);

        AdoToolkitUpdate result = Assert.IsType<AdoToolkitUpdate>(plan.Result);
        Assert.Equal(AdoToolkitUpdateStatus.UpToDate, result.Status);
        Assert.Equal(Running, result.LatestVersion);
        Assert.Equal(context.ModuleBase, result.Path);
        Assert.Null(result.PreviousPath);
        Assert.Equal(new Uri("https://github.com/JosephGibson/Ado_tools/releases/tag/v1.2.3"), result.ReleaseUri);
        Assert.Single(server.Handler.Requests);
        Assert.Equal(before, Snapshot(directory.Root));
    }

    [Fact]
    public async Task ANewerRunningVersionIsUpToDate()
    {
        using TestDirectory directory = new();
        ReleaseServer server = new(new Version(1, 2, 2), AdoToolkitInstallMode.Module, [1]);

        ToolkitUpdatePlan plan = await Prepare(server, ModuleInstall(directory, Running));

        Assert.Equal(AdoToolkitUpdateStatus.UpToDate, plan.Result!.Status);
        Assert.Equal(new Version(1, 2, 2), plan.Result.LatestVersion);
        Assert.Equal(Running, plan.Result.CurrentVersion);
    }

    [Fact]
    public async Task ANewerReleaseIsInstalledBesideTheRunningVersion()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Module, Zip(ModuleItems(Next)));
        Dictionary<string, string> running = Snapshot(context.ModuleBase!);
        CapturingLog log = new();
        using ToolkitUpdater updater = Updater(server);

        ToolkitUpdatePlan plan = await updater.PrepareAsync(context, log, Culture, TestContext.Current.CancellationToken);
        Assert.Null(plan.Result);
        AdoToolkitUpdate result = await updater.InstallAsync(plan, log, Culture, TestContext.Current.CancellationToken);

        string target = Path.Combine(directory.Root, "Modules", "AdoToolkit", "1.2.4");
        Assert.Equal(target, plan.TargetPath);
        Assert.Equal(AdoToolkitUpdateStatus.Installed, result.Status);
        Assert.Equal(AdoToolkitInstallMode.Module, result.InstallMode);
        Assert.Equal(target, result.Path);
        Assert.Equal(context.ModuleBase, result.PreviousPath);
        foreach ((string file, byte[] data) in ModuleFiles(Next)) Assert.Equal(data, File.ReadAllBytes(Path.Combine(target, file)));
        Assert.Equal(running, Snapshot(context.ModuleBase!));
        Assert.Equal(["1.2.3", "1.2.4"], Directory.GetFileSystemEntries(Path.Combine(directory.Root, "Modules", "AdoToolkit"))
            .Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(2, server.AssetRequests);
        Assert.Contains(log.ProgressEvents, progress => progress.Phase == AdoProgressPhase.ReleaseDownload);
        Assert.Contains(Messages.Get(AdoMessage.UpdateVerifiedLog, Culture, "AdoToolkit-1.2.4.zip", "AdoToolkit-1.2.4.zip.sha256"), log.Messages);
    }

    [Fact]
    public async Task APortableReleaseGoesIntoANewSiblingFolder()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = PortableInstall(directory, Running);
        string old = Path.Combine(directory.Root, "Portable", "AdoToolkit-1.2.3-win-x64");
        Dictionary<string, string> before = Snapshot(old);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Portable, Zip(PortableItems(Next)));

        AdoToolkitUpdate result = await Install(server, context);

        string target = Path.Combine(directory.Root, "Portable", "AdoToolkit-1.2.4-win-x64");
        Assert.Equal(AdoToolkitUpdateStatus.Installed, result.Status);
        Assert.Equal(AdoToolkitInstallMode.Portable, result.InstallMode);
        Assert.Equal(target, result.Path);
        Assert.Equal(old, result.PreviousPath);
        Assert.True(File.Exists(Path.Combine(target, "Start-AdoToolkit.cmd")));
        Assert.True(File.Exists(Path.Combine(target, "runtime", "pwsh.exe")));
        Assert.Equal(before, Snapshot(old));
        Assert.Equal(["AdoToolkit-1.2.3-win-x64", "AdoToolkit-1.2.4-win-x64"],
            Directory.GetFileSystemEntries(Path.Combine(directory.Root, "Portable")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AValidFolderOfTheNewVersionIsAlreadyInstalledWithoutADownload()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        WriteFiles(Path.Combine(directory.Root, "Modules", "AdoToolkit", "1.2.4"), ModuleFiles(Next));
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Module, Zip(ModuleItems(Next)));

        ToolkitUpdatePlan plan = await Prepare(server, context);

        Assert.Equal(AdoToolkitUpdateStatus.AlreadyInstalled, plan.Result!.Status);
        Assert.Equal(Path.Combine(directory.Root, "Modules", "AdoToolkit", "1.2.4"), plan.Result.Path);
        Assert.Equal(context.ModuleBase, plan.Result.PreviousPath);
        Assert.Equal(0, server.AssetRequests);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("truncated")]
    public async Task AnIncompleteFolderOfTheNewVersionIsReplaced(string damage)
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        string target = Path.Combine(directory.Root, "Modules", "AdoToolkit", "1.2.4");
        WriteFiles(target, ModuleFiles(Next));
        string core = Path.Combine(target, "AdoToolkit.Core.dll");
        if (damage == "missing") File.Delete(core);
        else File.WriteAllBytes(core, File.ReadAllBytes(core)[..300]);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Module, Zip(ModuleItems(Next)));

        AdoToolkitUpdate result = await Install(server, context);

        Assert.Equal(AdoToolkitUpdateStatus.Installed, result.Status);
        Assert.Equal(ModuleArchive.FolderState.Valid, ModuleArchive.Inspect(target, Next, ReadManifest, Culture));
        Assert.Empty(Directory.GetDirectories(Path.Combine(directory.Root, "Modules", "AdoToolkit"), "1.2.4.previous-*"));
    }

    [Fact]
    public async Task AnExistingPortableFolderIsRefusedBeforeAnyDownload()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = PortableInstall(directory, Running);
        string target = Path.Combine(directory.Root, "Portable", "AdoToolkit-1.2.4-win-x64");
        WriteFiles(target, [("mine.txt", Utf8("keep"))]);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Portable, Zip(PortableItems(Next)));

        AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() => Prepare(server, context));

        Assert.Equal(Messages.Get(AdoMessage.UpdateTargetExists, Culture, target), error.Message);
        Assert.Equal(1, server.ApiRequests);
        Assert.Equal(0, server.AssetRequests);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "mine.txt")));
    }

    [Fact]
    public async Task AReleaseThatNeedsANewerPowerShellIsNotInstalled()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Module, Zip(ModuleItems(Next, Manifest(Next, powerShell: "99.0"))));

        await Assert.ThrowsAsync<AdoConfigurationException>(() => Install(server, context));

        AssertNothingInstalled(directory);
    }

    public static TheoryData<string> RefusedReleases => new()
    {
        "manifest-version", "digest-mismatch", "checksum-mismatch", "checksum-digest-mismatch", "truncated", "hostile-entry",
        "server-error", "not-a-zip",
    };

    [Theory]
    [MemberData(nameof(RefusedReleases))]
    public async Task ARefusedDownloadLeavesNothingBehind(string kind)
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Module, Zip(ModuleItems(Next)));
        byte[] valid = server.Archive;
        switch (kind)
        {
            case "manifest-version": server.Archive = Zip(ModuleItems(Next, Manifest(new Version(1, 2, 5)))); break;
            case "digest-mismatch": server.ArchiveDigest = "sha256:" + new string('0', 64); break;
            case "checksum-mismatch": server.Checksum = Utf8(new string('0', 64) + "  AdoToolkit-1.2.4.zip\n"); break;
            case "checksum-digest-mismatch":
                server.Override = uri => uri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) && uri.Host == "release-assets.githubusercontent.com"
                    ? Bytes(Utf8(Sha256(valid) + " *AdoToolkit-1.2.4.zip\n")) : null;
                break;
            case "truncated": server.ArchiveSize = valid.Length + 10; break;
            case "hostile-entry": server.Archive = Zip([.. ModuleItems(Next), new ZipItem("AdoToolkit/1.2.4/../evil.dll", Utf8("x"))]); break;
            case "server-error":
                server.Override = uri => uri.Host == "release-assets.githubusercontent.com" && !uri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                    ? FakeHttpMessageHandler.Response("{}", 500) : null;
                break;
            case "not-a-zip": server.Archive = Utf8("not a zip at all"); break;
        }

        await Assert.ThrowsAnyAsync<AdoException>(() => Install(server, context));

        AssertNothingInstalled(directory);
    }

    [Fact]
    public async Task AMissingDigestOrChecksumAssetOrAnOversizedArchiveFailsBeforeAnyDownload()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        ReleaseServer noDigest = new(Next, AdoToolkitInstallMode.Module, [1, 2]) { OmitArchiveDigest = true };
        ReleaseServer noChecksum = new(Next, AdoToolkitInstallMode.Module, [1, 2]) { IncludeChecksum = false };
        ReleaseServer oversized = new(Next, AdoToolkitInstallMode.Module, [1, 2]) { ArchiveSize = 33L * 1024 * 1024 };

        await Assert.ThrowsAsync<AdoResponseFormatException>(() => Prepare(noDigest, context));
        await Assert.ThrowsAsync<AdoNotFoundException>(() => Prepare(noChecksum, context));
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => Prepare(oversized, context));

        Assert.Equal(0, noDigest.AssetRequests + noChecksum.AssetRequests + oversized.AssetRequests);
    }

    [Fact]
    public async Task ALayoutThatIsNotAnInstallIsRefusedBeforeAnyRequest()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running, onModulePath: false);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Module, [1]);

        await Assert.ThrowsAsync<AdoConfigurationException>(() => Prepare(server, context));

        Assert.Empty(server.Handler.Requests);
    }

    [Fact]
    public async Task CancellingDuringTheDownloadRemovesStagingAndTheLock()
    {
        using TestDirectory directory = new();
        ToolkitInstallContext context = ModuleInstall(directory, Running);
        ReleaseServer server = new(Next, AdoToolkitInstallMode.Module, Zip(ModuleItems(Next)));
        using CancellationTokenSource cancellation = new();
        server.Override = uri =>
        {
            if (uri.Host != "release-assets.githubusercontent.com" || uri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)) return null;
            cancellation.Cancel();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(server.Archive) };
        };
        using ToolkitUpdater updater = Updater(server);
        ToolkitUpdatePlan plan = await updater.PrepareAsync(context, new CapturingLog(), Culture, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updater.InstallAsync(plan, new CapturingLog(), Culture, cancellation.Token));

        AssertNothingInstalled(directory);
    }

    private static ToolkitUpdater Updater(ReleaseServer server) => new(ReadManifest, server.Handler, Limits(), new ManualTimeProvider());

    private static async Task<ToolkitUpdatePlan> Prepare(ReleaseServer server, ToolkitInstallContext context)
    {
        using ToolkitUpdater updater = Updater(server);
        return await updater.PrepareAsync(context, new CapturingLog(), Culture, TestContext.Current.CancellationToken);
    }

    private static async Task<AdoToolkitUpdate> Install(ReleaseServer server, ToolkitInstallContext context)
    {
        using ToolkitUpdater updater = Updater(server);
        ToolkitUpdatePlan plan = await updater.PrepareAsync(context, new CapturingLog(), Culture, TestContext.Current.CancellationToken);
        Assert.Null(plan.Result);
        return await updater.InstallAsync(plan, new CapturingLog(), Culture, TestContext.Current.CancellationToken);
    }

    // Only the running version, no staging folder, no backup and no lock.
    private static void AssertNothingInstalled(TestDirectory directory) =>
        Assert.Equal(["1.2.3"], Directory.GetFileSystemEntries(Path.Combine(directory.Root, "Modules", "AdoToolkit")).Select(Path.GetFileName));
}
