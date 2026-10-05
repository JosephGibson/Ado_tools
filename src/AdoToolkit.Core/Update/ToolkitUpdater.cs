using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace AdoToolkit.Core.Update;

// The engine of Update-AdoToolkit, isolated from the Azure DevOps side: its own HttpClient, no
// AdoHttpPipeline, credential provider, profile or configuration. PrepareAsync detects the install,
// reads the latest release and decides; it writes nothing, so -WhatIf runs it. InstallAsync downloads,
// verifies and installs beside the running copy, which it never touches.
public sealed class ToolkitUpdater : IDisposable
{
    private readonly HttpClient client;
    private readonly Func<string, ModuleManifestFacts?> readManifest;
    private readonly UpdateHttp.Limits limits;
    private readonly TimeProvider time;
    private readonly Action<UpdateCommitStep, string>? fault;

    // transport replaces the network in tests and is never disposed here. Without it, the updater
    // owns a handler that uses the system proxy and sends no credentials to GitHub.
    public ToolkitUpdater(Func<string, ModuleManifestFacts?> readManifest, HttpMessageHandler? transport = null)
        : this(readManifest, transport, new UpdateHttp.Limits(), TimeProvider.System) { }

    internal ToolkitUpdater(Func<string, ModuleManifestFacts?> readManifest, HttpMessageHandler? transport, UpdateHttp.Limits limits,
        TimeProvider time, Action<UpdateCommitStep, string>? fault = null)
    {
        ArgumentNullException.ThrowIfNull(readManifest);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(time);
        this.readManifest = readManifest;
        this.limits = limits;
        this.time = time;
        this.fault = fault;
        client = transport is null ? new HttpClient(UpdateHttp.CreateHandler(), disposeHandler: true) : new HttpClient(transport, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public void Dispose() => client.Dispose();

    public async Task<ToolkitUpdatePlan> PrepareAsync(ToolkitInstallContext context, IAdoLog log, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(culture);
        // Refuses a copy that is not an install before any request.
        ToolkitInstallation installation = ToolkitInstallation.Detect(context, culture);
        GitHubClient github = new(client, limits, time, installation.CurrentVersion, log);
        byte[] json = await github.GetLatestReleaseAsync(culture, cancellationToken).ConfigureAwait(false);
        ReleaseReader.Release release = ReleaseReader.Read(json, installation.Mode, limits, culture);
        if (release.Version <= installation.CurrentVersion)
            return new ToolkitUpdatePlan(Result(AdoToolkitUpdateStatus.UpToDate, installation, release.Version, installation.Folder, previous: null));
        string target = installation.TargetFor(release.Version);
        if (installation.Mode == AdoToolkitInstallMode.Module)
        {
            if (ModuleArchive.Inspect(target, release.Version, readManifest, culture) == ModuleArchive.FolderState.Valid)
                return new ToolkitUpdatePlan(Result(AdoToolkitUpdateStatus.AlreadyInstalled, installation, release.Version, target, installation.Folder));
        }
        // A portable folder is never replaced, so an existing one is refused before any download.
        else if (Path.Exists(target)) throw UpdateFolderCommit.TargetExists(target, culture);
        return new ToolkitUpdatePlan(installation, release, target, context.PowerShellVersion);
    }

    public async Task<AdoToolkitUpdate> InstallAsync(ToolkitUpdatePlan plan, IAdoLog log, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(culture);
        if (plan.Installation is not { } installation || plan.Release is not { } release) throw new ArgumentException(null, nameof(plan));
        GitHubClient github = new(client, limits, time, installation.CurrentVersion, log);
        UpdateFolderCommit commit = new(limits, fault);
        AdoToolkitUpdateStatus status = await commit.RunAsync(installation, release.Version, plan.TargetPath, readManifest,
            (staging, token) => BuildAsync(github, installation, release, plan.PowerShellVersion, staging, log, culture, token),
            log.Warning, culture, cancellationToken).ConfigureAwait(false);
        return Result(status, installation, release.Version, plan.TargetPath, installation.Folder);
    }

    // Downloads the checksum file and the zip into staging, verifies both SHA-256 values before the
    // zip is opened, and extracts it. The zip stays open without sharing from its first byte to the
    // end of extraction.
    private async Task<string> BuildAsync(GitHubClient github, ToolkitInstallation installation, ReleaseReader.Release release,
        Version? powerShellVersion, string staging, IAdoLog log, CultureInfo culture, CancellationToken cancellationToken)
    {
        ReleaseReader.Asset archive = release.Archive;
        ReleaseReader.Asset checksum = release.Checksum;
        byte[] checksumFile = await github.DownloadSmallAsync(release.Version, checksum, culture, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(checksumFile)), checksum.Sha256, StringComparison.OrdinalIgnoreCase))
            throw DigestMismatch(checksum.Name, culture);
        string expected = ChecksumFile.ReadHash(checksumFile, checksum.Name, archive.Name, culture);
        bool portable = installation.Mode == AdoToolkitInstallMode.Portable;
        using FileStream file = new(Path.Combine(staging, "release.zip"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            int total = Kilobytes(archive.Size);
            await github.DownloadAsync(release.Version, archive, file, hash, portable ? limits.PortableDownloadTime : limits.ModuleDownloadTime,
                received => log.Progress(new AdoProgress { Phase = AdoProgressPhase.ReleaseDownload, Completed = Kilobytes(received), Total = total }),
                culture, cancellationToken).ConfigureAwait(false);
            await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            string actual = Convert.ToHexString(hash.GetHashAndReset());
            if (!string.Equals(actual, archive.Sha256, StringComparison.OrdinalIgnoreCase)) throw DigestMismatch(archive.Name, culture);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.UpdateChecksumMismatch, culture, archive.Name, checksum.Name))
                { Operation = UpdateHttp.DownloadOperation };
        }
        log.Verbose(Messages.Get(AdoMessage.UpdateVerifiedLog, culture, archive.Name, checksum.Name));
        cancellationToken.ThrowIfCancellationRequested();
        string output = Path.Combine(staging, portable ? "bundle" : "module");
        file.Position = 0;
        try
        {
            using ZipArchive zip = new(file, ZipArchiveMode.Read, leaveOpen: true);
            if (portable) PortableArchive.Extract(zip, release.Version, output, limits, archive.Name, readManifest, culture);
            else
            {
                ModuleArchive.Extract(zip, release.Version, output, limits, archive.Name, culture);
                ModuleArchive.Check(output, release.Version, powerShellVersion, readManifest, archive.Name, culture);
            }
        }
        catch (InvalidDataException error)
        {
            throw ModuleArchive.ArchiveLayout(archive.Name, culture, error);
        }
        return output;
    }

    private static int Kilobytes(long bytes) => (int)Math.Min(int.MaxValue, bytes / 1024);

    private static AdoResponseFormatException DigestMismatch(string name, CultureInfo culture) =>
        new(Messages.Get(AdoMessage.UpdateDigestMismatch, culture, name)) { Operation = UpdateHttp.DownloadOperation };

    private static AdoToolkitUpdate Result(AdoToolkitUpdateStatus status, ToolkitInstallation installation, Version latest, string path, string? previous) => new()
    {
        Status = status,
        InstallMode = installation.Mode,
        CurrentVersion = installation.CurrentVersion,
        LatestVersion = latest,
        Path = path,
        PreviousPath = previous,
        ReleaseUri = UpdateHttp.ReleasePage(latest),
    };
}
