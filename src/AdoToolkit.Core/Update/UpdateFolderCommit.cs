using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Update;

// Installs one release folder beside the running copy, under a lock, from staging on the same volume.
// Module-only mode replaces an incomplete version folder as Install-AdoToolkit.ps1 replaces one (step
// 3); portable mode never replaces anything. The commit boundary is the move of staging onto the
// target: a failure up to and including it leaves the previous state, and after it the new version
// stays and cleanup failures only warn.
internal sealed partial class UpdateFolderCommit(UpdateHttp.Limits limits, Action<UpdateCommitStep, string>? fault = null)
{
    private const string ModuleLock = ".update.lock";
    private const string LockFile = "lock";
    private const string ModuleStaging = ".update-";

    [GeneratedRegex(@"^\.update-[0-9a-f]{32}\z", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ModuleLeftover();

    [GeneratedRegex(@"^([0-9]+\.[0-9]+\.[0-9]+)\.previous-[0-9a-f]{32}\z", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PreviousCopy();

    private static readonly EnumerationOptions DirectChildren = new()
    {
        RecurseSubdirectories = false, AttributesToSkip = 0, IgnoreInaccessible = false,
        ReturnSpecialDirectories = false, MatchType = MatchType.Simple,
    };

    // build fills the staging folder it is given and returns the folder to move onto the target.
    internal async Task<AdoToolkitUpdateStatus> RunAsync(ToolkitInstallation installation, Version version, string target,
        Func<string, ModuleManifestFacts?> readManifest, Func<string, CancellationToken, Task<string>> build, Action<string> warning,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(warning);
        bool module = installation.Mode == AdoToolkitInstallMode.Module;
        string root = installation.Root;
        string destination = Path.GetFileName(target);
        // One lock per module root; one per portable destination, so that copies started from
        // different portable folders towards the same new folder share it.
        string lockFolder = Path.Combine(root, module ? ModuleLock : "." + destination + ".lock");
        string staging = Path.Combine(root, module ? ModuleStaging + Guid.NewGuid().ToString("N") : "." + destination + "." + Guid.NewGuid().ToString("N") + ".tmp");
        // The path that a write failure names.
        string current = Path.Combine(lockFolder, LockFile);
        FileStream? held = null;
        bool staged = false;
        try
        {
            held = await LockAsync(lockFolder, root, culture, cancellationToken).ConfigureAwait(false);
            // Another session may have finished while this one read the release.
            current = target;
            if (module)
            {
                if (ModuleArchive.Inspect(target, version, readManifest, culture) == ModuleArchive.FolderState.Valid) return AdoToolkitUpdateStatus.AlreadyInstalled;
            }
            else if (Path.Exists(target)) throw TargetExists(target, culture);
            RemoveLeftovers(root, module ? ModuleLeftover() : PortableLeftover(destination));
            cancellationToken.ThrowIfCancellationRequested();
            current = staging;
            fault?.Invoke(UpdateCommitStep.Staging, staging);
            Directory.CreateDirectory(staging);
            staged = true;
            string built = await build(staging, cancellationToken).ConfigureAwait(false);
            fault?.Invoke(UpdateCommitStep.Build, built);
            cancellationToken.ThrowIfCancellationRequested();
            current = target;
            return module
                ? await CommitModuleAsync(built, target, version, root, readManifest, warning, culture).ConfigureAwait(false)
                : await CommitPortableAsync(built, target, culture).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw UpdateGuards.IsDiskFull(error)
                ? new AdoFileOutputException(Messages.Get(AdoMessage.UpdateDiskFull, culture, root, UpdateHttp.VersionText(version)), error)
                : new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, current), error);
        }
        finally
        {
            if (staged) UpdateGuards.TryDeleteTree(staging);
            if (held is not null)
            {
                held.Dispose();
                Unlock(lockFolder);
            }
        }
    }

    // A file in a folder of its own: a standard user may create folders, but not files, at the root
    // of the system drive, where a portable folder may sit. DeleteOnClose removes the file with the
    // handle, also when the process ends. A held lock fails at once; a folder that another session
    // is removing, which Windows reports as missing or, while the delete is pending, as access
    // denied, is retried with growing delays.
    private async Task<FileStream> LockAsync(string folder, string root, CultureInfo culture, CancellationToken cancellationToken)
    {
        string path = Path.Combine(folder, LockFile);
        TimeSpan delay = limits.CommitRetryDelay;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                fault?.Invoke(UpdateCommitStep.Lock, path);
                if (UpdateGuards.IsLink(Directory.CreateDirectory(folder)))
                    throw new AdoConfigurationException(Messages.Get(AdoMessage.UpdateLinkedPath, culture, folder));
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException error) when (UpdateGuards.IsSharingViolation(error))
            {
                throw new AdoFileOutputException(Messages.Get(AdoMessage.UpdateBusy, culture, root), error);
            }
            catch (Exception error) when ((error is DirectoryNotFoundException or UnauthorizedAccessException) && attempt < limits.CommitAttempts)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay += delay;
            }
        }
    }

    // The folder goes once it is empty; a session that holds a new lock file in it keeps it.
    private static void Unlock(string folder)
    {
        try
        {
            DirectoryInfo info = new(folder);
            if (info.Exists && !UpdateGuards.IsLink(info)) info.Delete(recursive: false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private async Task<AdoToolkitUpdateStatus> CommitModuleAsync(string built, string target, Version version, string root,
        Func<string, ModuleManifestFacts?> readManifest, Action<string> warning, CultureInfo culture)
    {
        string? backup = null;
        if (Directory.Exists(target))
        {
            if (UpdateGuards.FindLinkInside(target) is { } link)
                throw new AdoConfigurationException(Messages.Get(AdoMessage.UpdateLinkedPath, culture, link));
            string previous = target + ".previous-" + Guid.NewGuid().ToString("N");
            // Loaded assemblies stay locked until their PowerShell process ends.
            try
            {
                fault?.Invoke(UpdateCommitStep.Backup, target);
                Directory.Move(target, previous);
                backup = previous;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new AdoFileOutputException(Messages.Get(AdoMessage.UpdateInUse, culture, UpdateHttp.VersionText(version)), error);
            }
        }
        // From the backup to the commit or the rollback, cancellation is not observed: a stop must not
        // leave only a backup behind.
        try { await MoveAsync(built, target).ConfigureAwait(false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (backup is not null)
            {
                // Never discard the only old copy: when the rollback fails too, the backup stays.
                try
                {
                    fault?.Invoke(UpdateCommitStep.Rollback, backup);
                    Directory.Move(backup, target);
                }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException) { }
            }
            // Another session or the installer committed this version first.
            else if (ModuleArchive.Inspect(target, version, readManifest, culture) == ModuleArchive.FolderState.Valid)
                return AdoToolkitUpdateStatus.AlreadyInstalled;
            throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, target), error);
        }
        Cleanup(root, backup, warning, culture);
        return AdoToolkitUpdateStatus.Installed;
    }

    private async Task<AdoToolkitUpdateStatus> CommitPortableAsync(string built, string target, CultureInfo culture)
    {
        try { await MoveAsync(built, target).ConfigureAwait(false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Directory.Move never replaces: a folder that appeared meanwhile is refused, not overwritten.
            if (Path.Exists(target)) throw new AdoFileOutputException(Messages.Get(AdoMessage.UpdateTargetExists, culture, target), error);
            throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, target), error);
        }
        return AdoToolkitUpdateStatus.Installed;
    }

    // The commit. A sharing violation, from an antivirus scan of freshly written files, is retried
    // with growing delays; a target that exists is never retried.
    private async Task MoveAsync(string source, string target)
    {
        TimeSpan delay = limits.CommitRetryDelay;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                fault?.Invoke(UpdateCommitStep.Commit, target);
                Directory.Move(source, target);
                return;
            }
            catch (Exception error) when (attempt < limits.CommitAttempts && !Path.Exists(target)
                && (UpdateGuards.IsSharingViolation(error) || error is UnauthorizedAccessException))
            {
                await Task.Delay(delay, CancellationToken.None).ConfigureAwait(false);
                delay += delay;
            }
        }
    }

    // After the boundary: the backup goes, and earlier backups only beside their own version, as in
    // the installer, because a backup whose version folder is missing may be its only copy.
    private void Cleanup(string root, string? backup, Action<string> warning, CultureInfo culture)
    {
        try
        {
            fault?.Invoke(UpdateCommitStep.Cleanup, root);
            if (backup is not null && !UpdateGuards.TryDeleteTree(backup))
                warning(Messages.Get(AdoMessage.UpdatePreviousInUse, culture, backup));
            foreach (DirectoryInfo leftover in new DirectoryInfo(root).EnumerateDirectories("*", DirectChildren))
            {
                Match match = PreviousCopy().Match(leftover.Name);
                if (match.Success && !string.Equals(leftover.FullName, backup, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(Path.Combine(root, match.Groups[1].Value)))
                    UpdateGuards.TryDeleteTree(leftover.FullName);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            if (backup is not null && Directory.Exists(backup)) warning(Messages.Get(AdoMessage.UpdatePreviousInUse, culture, backup));
        }
    }

    // Under the lock, staging folders that an ended update left behind; the installer's .staging-*
    // folders are its own.
    private static void RemoveLeftovers(string root, Regex pattern)
    {
        try
        {
            foreach (DirectoryInfo leftover in new DirectoryInfo(root).EnumerateDirectories("*", DirectChildren))
                if (pattern.IsMatch(leftover.Name)) UpdateGuards.TryDeleteTree(leftover.FullName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or RegexMatchTimeoutException) { }
    }

    private static Regex PortableLeftover(string destination) =>
        new("^" + Regex.Escape("." + destination + ".") + "[0-9a-f]{32}\\.tmp\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static AdoFileOutputException TargetExists(string target, CultureInfo culture) =>
        new(Messages.Get(AdoMessage.UpdateTargetExists, culture, target));
}
