namespace AdoToolkit.Core.Update;

// What PrepareAsync decided: either a finished result (up to date, or already installed), or the
// folder and version that InstallAsync is to install, after the cmdlet's ShouldProcess.
public sealed class ToolkitUpdatePlan
{
    internal ToolkitUpdatePlan(AdoToolkitUpdate result)
    {
        Result = result;
        LatestVersion = result.LatestVersion;
        TargetPath = result.Path;
    }

    internal ToolkitUpdatePlan(ToolkitInstallation installation, ReleaseReader.Release release, string target, Version powerShellVersion)
    {
        Installation = installation;
        Release = release;
        LatestVersion = release.Version;
        TargetPath = target;
        PowerShellVersion = powerShellVersion;
    }

    // Set when there is nothing to install.
    public AdoToolkitUpdate? Result { get; }
    public Version LatestVersion { get; }
    public string TargetPath { get; }
    // The zip that InstallAsync downloads, named from the version; null when nothing is to install.
    public string? ArchiveName => Release?.Archive.Name;
    internal ToolkitInstallation? Installation { get; }
    internal ReleaseReader.Release? Release { get; }
    internal Version? PowerShellVersion { get; }
}
