namespace AdoToolkit.Core.Update;

// What Update-AdoToolkit found or did. The folders are local; the versions and the release page are
// built from the parsed tag, never from remote text.
public sealed class AdoToolkitUpdate
{
    public AdoToolkitUpdateStatus Status { get; init; }
    public AdoToolkitInstallMode InstallMode { get; init; }
    public required Version CurrentVersion { get; init; }
    public required Version LatestVersion { get; init; }
    // The folder of the version to use next: the new one once it is installed, the running one when
    // nothing was newer.
    public required string Path { get; init; }
    // The running copy's folder when a newer version is installed; null when it is up to date.
    public string? PreviousPath { get; init; }
    public required Uri ReleaseUri { get; init; }
}
