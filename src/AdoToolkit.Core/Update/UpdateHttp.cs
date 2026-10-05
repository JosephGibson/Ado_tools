using System.Net;
using System.Net.Http;

namespace AdoToolkit.Core.Update;

// The one place that names GitHub. The owner, the repository and the hosts are constants: no
// parameter, variable or setting changes where a release comes from. Every URL is built here from
// the parsed version numbers and constant asset names.
internal static class UpdateHttp
{
    private const string Owner = "JosephGibson";
    private const string Repository = "Ado_tools";
    internal const string LatestOperation = "GitHubLatestRelease";
    internal const string DownloadOperation = "GitHubReleaseDownload";
    internal const string ArchiveOperation = "GitHubReleaseArchive";
    internal const string ApiAccept = "application/vnd.github+json";
    internal const string DownloadAccept = "application/octet-stream";
    internal const string ApiVersionHeader = "X-GitHub-Api-Version";
    internal const string ApiVersion = "2022-11-28";
    internal const string Product = "AdoToolkit";

    // Exact host names: the API, the release pages and, through a redirect, the release assets.
    internal static readonly string[] AllowedHosts = ["api.github.com", "github.com", "release-assets.githubusercontent.com"];

    internal static Uri LatestRelease { get; } = new("https://api.github.com/repos/" + Owner + "/" + Repository + "/releases/latest");

    // The README section that describes installing a release by hand.
    internal static Uri ManualInstall { get; } = new("https://github.com/" + Owner + "/" + Repository + "#installation");

    internal static string VersionText(Version version) => version.ToString(3);

    internal static Uri ReleasePage(Version version) =>
        new("https://github.com/" + Owner + "/" + Repository + "/releases/tag/v" + VersionText(version));

    internal static Uri Download(Version version, string asset) =>
        new("https://github.com/" + Owner + "/" + Repository + "/releases/download/v" + VersionText(version) + "/" + asset);

    internal static string ArchiveName(Version version, AdoToolkitInstallMode mode) =>
        "AdoToolkit-" + VersionText(version) + (mode == AdoToolkitInstallMode.Portable ? "-win-x64.zip" : ".zip");

    // No credentials go to GitHub. The system proxy, PAC files included, may receive the user's
    // Windows credentials, which an authenticating proxy needs. GitHubClient follows redirects itself.
    internal static SocketsHttpHandler CreateHandler() => new()
    {
        UseProxy = true,
        Proxy = null,
        DefaultProxyCredentials = CredentialCache.DefaultCredentials,
        Credentials = null,
        PreAuthenticate = false,
        UseCookies = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(30),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    // Caps in bytes, and timers; tests lower them. The portable caps leave room above the 0.10.5
    // bundle: a 110 MB zip, 669 entries, 247 MB unpacked, the largest entry 18.7 MB.
    internal sealed record Limits
    {
        internal long ReleaseBytes { get; init; } = 1024 * 1024;
        internal long ChecksumBytes { get; init; } = 1024;
        internal long ModuleArchiveBytes { get; init; } = 32L * 1024 * 1024;
        internal long PortableArchiveBytes { get; init; } = 256L * 1024 * 1024;
        // $maximumEntryBytes of Install-AdoToolkit.ps1.
        internal long EntryBytes { get; init; } = 64L * 1024 * 1024;
        internal int ModuleEntries { get; init; } = 64;
        internal int PortableEntries { get; init; } = 5000;
        internal long PortableBytes { get; init; } = 1024L * 1024 * 1024;
        internal long BundleBytes { get; init; } = 4096;
        internal long FreeSpaceMargin { get; init; } = 16L * 1024 * 1024;
        internal int MaximumRedirects { get; init; } = 5;
        internal TimeSpan RequestTime { get; init; } = TimeSpan.FromSeconds(60);
        internal TimeSpan ModuleDownloadTime { get; init; } = TimeSpan.FromMinutes(5);
        internal TimeSpan PortableDownloadTime { get; init; } = TimeSpan.FromMinutes(30);
        internal TimeSpan StallTime { get; init; } = TimeSpan.FromSeconds(60);
        // Antivirus scans hold freshly written files; the commit move retries a sharing violation.
        internal int CommitAttempts { get; init; } = 5;
        internal TimeSpan CommitRetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    }
}
