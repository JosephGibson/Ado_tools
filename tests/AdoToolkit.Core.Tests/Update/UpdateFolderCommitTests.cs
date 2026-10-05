using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Update;
using static AdoToolkit.Core.Tests.Update.UpdateFixture;

namespace AdoToolkit.Core.Tests.Update;

// The commit on either side of its boundary, the move of staging onto the target.
public sealed class UpdateFolderCommitTests
{
    private static readonly Version Running = new(1, 2, 3);
    private static readonly Version Next = new(1, 2, 4);

    public static TheoryData<int, bool> FaultsBeforeTheBoundary => new()
    {
        { (int)UpdateCommitStep.Lock, false }, { (int)UpdateCommitStep.Staging, false }, { (int)UpdateCommitStep.Build, false },
        { (int)UpdateCommitStep.Commit, false }, { (int)UpdateCommitStep.Lock, true }, { (int)UpdateCommitStep.Build, true },
        { (int)UpdateCommitStep.Backup, true }, { (int)UpdateCommitStep.Commit, true },
    };

    [Theory]
    [MemberData(nameof(FaultsBeforeTheBoundary))]
    public async Task AFaultUpToTheBoundaryLeavesThePreviousStateAndRemovesStaging(int step, bool incompleteTarget)
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        if (incompleteTarget) WriteFiles(installation.TargetFor(Next), ModuleFiles(Next).Take(3));
        Dictionary<string, string> before = Snapshot(installation.Root);

        AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() =>
            RunModule(installation, (current, _) => { if ((int)current == step) throw new IOException("Injected " + current); }));

        Assert.IsType<IOException>(error.InnerException);
        Assert.Equal(before, Snapshot(installation.Root));
    }

    [Fact]
    public async Task AFailedRollbackKeepsTheBackup()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        string target = installation.TargetFor(Next);
        WriteFiles(target, ModuleFiles(Next).Take(3));

        await Assert.ThrowsAsync<AdoFileOutputException>(() => RunModule(installation, (current, _) =>
        {
            if (current is UpdateCommitStep.Commit or UpdateCommitStep.Rollback) throw new IOException("Injected " + current);
        }));

        Assert.False(Directory.Exists(target));
        string backup = Assert.Single(Directory.GetDirectories(installation.Root, "1.2.4.previous-*"));
        Assert.Equal(3, Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public async Task AFaultAfterTheBoundaryKeepsTheNewVersionAndWarns()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        string target = installation.TargetFor(Next);
        WriteFiles(target, ModuleFiles(Next).Take(3));
        CapturingLog log = new();

        AdoToolkitUpdateStatus status = await RunModule(installation,
            (current, _) => { if (current == UpdateCommitStep.Cleanup) throw new IOException("Injected"); }, log);

        Assert.Equal(AdoToolkitUpdateStatus.Installed, status);
        Assert.Equal(ModuleArchive.FolderState.Valid, ModuleArchive.Inspect(target, Next, ReadManifest, Culture));
        string backup = Assert.Single(Directory.GetDirectories(installation.Root, "1.2.4.previous-*"));
        Assert.Equal([Messages.Get(AdoMessage.UpdatePreviousInUse, Culture, backup)], log.Warnings);
        Assert.Empty(Directory.GetDirectories(installation.Root, ".update-*"));
    }

    [Theory]
    [InlineData(unchecked((int)0x80070070))]
    [InlineData(unchecked((int)0x80070027))]
    public async Task AFullDiskIsNamed(int hresult)
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);

        AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() => RunModule(installation,
            (current, _) => { if (current == UpdateCommitStep.Build) throw new IOException("There is not enough space on the disk.", hresult); }));

        Assert.Equal(Messages.Get(AdoMessage.UpdateDiskFull, Culture, installation.Root, "1.2.4"), error.Message);
        Assert.Empty(Directory.GetDirectories(installation.Root, ".update-*"));
    }

    [Fact]
    public async Task AHeldLockRefusesASecondUpdateBeforeItDownloads()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        using (HoldLock(Path.Combine(installation.Root, ".update.lock")))
        {
            bool built = false;

            AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() => new UpdateFolderCommit(Limits()).RunAsync(
                installation, Next, installation.TargetFor(Next), ReadManifest, (_, _) => { built = true; return Task.FromResult(""); },
                _ => { }, Culture, TestContext.Current.CancellationToken));

            Assert.Equal(Messages.Get(AdoMessage.UpdateBusy, Culture, installation.Root), error.Message);
            Assert.False(built);
        }

        // Once the other session lets go, the lock is free again.
        Assert.Equal(AdoToolkitUpdateStatus.Installed, await RunModule(installation, null));
        Assert.False(Directory.Exists(Path.Combine(installation.Root, ".update.lock")));
    }

    [Fact]
    public async Task PortableUpdatesFromSiblingFoldersShareTheDestinationLock()
    {
        using TestDirectory directory = new();
        ToolkitInstallation first = ToolkitInstallation.Detect(PortableInstall(directory, Running), Culture);
        ToolkitInstallation sibling = ToolkitInstallation.Detect(PortableInstall(directory, new Version(1, 2, 2)), Culture);
        Assert.Equal(first.TargetFor(Next), sibling.TargetFor(Next));
        using FileStream held = HoldLock(Path.Combine(first.Root, ".AdoToolkit-1.2.4-win-x64.lock"));
        bool built = false;

        await Assert.ThrowsAsync<AdoFileOutputException>(() => new UpdateFolderCommit(Limits()).RunAsync(sibling, Next, sibling.TargetFor(Next),
            ReadManifest, (_, _) => { built = true; return Task.FromResult(""); }, _ => { }, Culture, TestContext.Current.CancellationToken));

        Assert.False(built);
    }

    // While another session's delete of the empty lock folder is pending, Windows reports access denied.
    [Fact]
    public async Task ALockFolderBeingRemovedIsRetried()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        int attempts = 0;

        AdoToolkitUpdateStatus status = await RunModule(installation, (current, _) =>
        {
            if (current == UpdateCommitStep.Lock && ++attempts < 3) throw new UnauthorizedAccessException("Delete pending");
        });

        Assert.Equal(AdoToolkitUpdateStatus.Installed, status);
        Assert.Equal(3, attempts);
        Assert.False(Directory.Exists(Path.Combine(installation.Root, ".update.lock")));
    }

    [Fact]
    public async Task ALockFolderThatIsALinkIsRefusedAndKept()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        string elsewhere = Path.Combine(directory.Root, "Elsewhere");
        Directory.CreateDirectory(elsewhere);
        string lockFolder = Path.Combine(installation.Root, ".update.lock");
        CreateJunction(lockFolder, elsewhere);
        try
        {
            AdoConfigurationException error = await Assert.ThrowsAsync<AdoConfigurationException>(() => RunModule(installation, null));

            Assert.Equal(Messages.Get(AdoMessage.UpdateLinkedPath, Culture, lockFolder), error.Message);
            Assert.True(Directory.Exists(lockFolder));
            Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
            Assert.False(Directory.Exists(installation.TargetFor(Next)));
        }
        finally { Directory.Delete(lockFolder); }
    }

    [Fact]
    public async Task AWriteFailureNamesThePathThatFailed()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        Dictionary<UpdateCommitStep, string?> named = [];
        foreach (UpdateCommitStep step in new[] { UpdateCommitStep.Lock, UpdateCommitStep.Staging })
        {
            string? failed = null;

            AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() => RunModule(installation, (current, path) =>
            {
                if (current != step) return;
                failed = path;
                throw new UnauthorizedAccessException("Injected");
            }));

            Assert.Equal(Messages.Get(AdoMessage.FileOutput, Culture, failed), error.Message);
            named[step] = failed;
        }

        Assert.Equal(Path.Combine(installation.Root, ".update.lock", "lock"), named[UpdateCommitStep.Lock]);
        Assert.StartsWith(Path.Combine(installation.Root, ".update-"), named[UpdateCommitStep.Staging], StringComparison.Ordinal);
        Assert.Equal(["1.2.3"], Directory.GetFileSystemEntries(installation.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task OnlyOwnLeftoversAndBackupsBesideTheirVersionAreRemoved()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        string root = installation.Root;
        string hex = Guid.NewGuid().ToString("N");
        foreach (string name in new[] { ".update-" + hex, ".staging-" + hex, "1.2.2.previous-" + hex, "1.2.3.previous-" + hex, ".update-notours" })
            WriteFiles(Path.Combine(root, name), [("file.txt", Utf8("x"))]);

        Assert.Equal(AdoToolkitUpdateStatus.Installed, await RunModule(installation, null));

        Assert.Equal(new[] { "1.2.2.previous-" + hex, "1.2.3", "1.2.4", ".staging-" + hex, ".update-notours" }.Order(StringComparer.Ordinal),
            Directory.GetDirectories(root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task PortableLeftoversOfTheSameDestinationAreRemoved()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(PortableInstall(directory, Running), Culture);
        string hex = Guid.NewGuid().ToString("N");
        WriteFiles(Path.Combine(installation.Root, ".AdoToolkit-1.2.4-win-x64." + hex + ".tmp"), [("file.txt", Utf8("x"))]);
        WriteFiles(Path.Combine(installation.Root, ".AdoToolkit-1.2.5-win-x64." + hex + ".tmp"), [("file.txt", Utf8("x"))]);

        Assert.Equal(AdoToolkitUpdateStatus.Installed, await RunPortable(installation, null));

        Assert.Equal(new[] { "AdoToolkit-1.2.3-win-x64", "AdoToolkit-1.2.4-win-x64", ".AdoToolkit-1.2.5-win-x64." + hex + ".tmp" }.Order(StringComparer.Ordinal),
            Directory.GetDirectories(installation.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AModuleFolderThatAppearsAtTheCommitIsAlreadyInstalled()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        string target = installation.TargetFor(Next);

        AdoToolkitUpdateStatus status = await RunModule(installation, (current, path) =>
        {
            if (current == UpdateCommitStep.Commit && !Directory.Exists(target)) WriteFiles(target, ModuleFiles(Next));
        });

        Assert.Equal(AdoToolkitUpdateStatus.AlreadyInstalled, status);
    }

    [Fact]
    public async Task APortableFolderThatAppearsAtTheCommitIsRefusedAndKept()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(PortableInstall(directory, Running), Culture);
        string target = installation.TargetFor(Next);

        AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() => RunPortable(installation, (current, _) =>
        {
            if (current == UpdateCommitStep.Commit && !Directory.Exists(target)) WriteFiles(target, [("theirs.txt", Utf8("other session"))]);
        }));

        Assert.Equal(Messages.Get(AdoMessage.UpdateTargetExists, Culture, target), error.Message);
        Assert.Equal(["theirs.txt"], Directory.GetFiles(target).Select(Path.GetFileName));
    }

    [Fact]
    public async Task ASharingViolationAtTheCommitIsRetried()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        int attempts = 0;

        AdoToolkitUpdateStatus status = await RunModule(installation, (current, _) =>
        {
            if (current == UpdateCommitStep.Commit && ++attempts < 3) throw new IOException("In use", unchecked((int)0x80070020));
        });

        Assert.Equal(AdoToolkitUpdateStatus.Installed, status);
        Assert.Equal(3, attempts);

        using TestDirectory again = new();
        ToolkitInstallation other = ToolkitInstallation.Detect(ModuleInstall(again, Running), Culture);
        int tries = 0;
        await Assert.ThrowsAsync<AdoFileOutputException>(() => RunModule(other, (current, _) =>
        {
            if (current == UpdateCommitStep.Commit) { tries++; throw new IOException("In use", unchecked((int)0x80070020)); }
        }));
        Assert.Equal(5, tries);
    }

    [Fact]
    public async Task CancellationIsNotObservedBetweenTheBackupAndTheCommit()
    {
        using TestDirectory directory = new();
        ToolkitInstallation installation = ToolkitInstallation.Detect(ModuleInstall(directory, Running), Culture);
        string target = installation.TargetFor(Next);
        WriteFiles(target, ModuleFiles(Next).Take(3));
        using CancellationTokenSource cancellation = new();

        AdoToolkitUpdateStatus status = await new UpdateFolderCommit(Limits(), (current, _) => { if (current == UpdateCommitStep.Backup) cancellation.Cancel(); })
            .RunAsync(installation, Next, target, ReadManifest, (staging, _) => Task.FromResult(Build(staging, Next)), _ => { }, Culture, cancellation.Token);

        Assert.Equal(AdoToolkitUpdateStatus.Installed, status);
        Assert.Equal(ModuleArchive.FolderState.Valid, ModuleArchive.Inspect(target, Next, ReadManifest, Culture));
        Assert.Empty(Directory.GetDirectories(installation.Root, "1.2.4.previous-*"));
    }

    private static Task<AdoToolkitUpdateStatus> RunModule(ToolkitInstallation installation, Action<UpdateCommitStep, string>? fault, CapturingLog? log = null) =>
        new UpdateFolderCommit(Limits(), fault).RunAsync(installation, Next, installation.TargetFor(Next), ReadManifest,
            (staging, _) => Task.FromResult(Build(staging, Next)), (log ?? new CapturingLog()).Warning, Culture, TestContext.Current.CancellationToken);

    private static Task<AdoToolkitUpdateStatus> RunPortable(ToolkitInstallation installation, Action<UpdateCommitStep, string>? fault) =>
        new UpdateFolderCommit(Limits(), fault).RunAsync(installation, Next, installation.TargetFor(Next), ReadManifest,
            (staging, _) =>
            {
                string bundle = Path.Combine(staging, "bundle");
                WriteFiles(bundle, PortableItems(Next).Select(item => (item.Name, item.Data!)));
                return Task.FromResult(bundle);
            }, _ => { }, Culture, TestContext.Current.CancellationToken);

    private static string Build(string staging, Version version)
    {
        string module = Path.Combine(staging, "module");
        WriteFiles(module, ModuleFiles(version));
        return module;
    }

    // Another session's lock: the file in the lock folder, held without sharing.
    private static FileStream HoldLock(string folder)
    {
        Directory.CreateDirectory(folder);
        return new FileStream(Path.Combine(folder, "lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
    }
}
