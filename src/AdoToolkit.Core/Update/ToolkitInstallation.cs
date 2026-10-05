namespace AdoToolkit.Core.Update;

// The install that runs this copy, detected from the module's own base folder before any request:
// - Portable: <folder>\module beside Start-AdoToolkit.cmd and runtime\pwsh.exe. A new version goes
//   into a new sibling folder, because the running pwsh.exe is locked.
// - Module-only: <root>\AdoToolkit\<version>, with <root> on PSModulePath, which is what lets
//   Import-Module AdoToolkit pick the newest version. A copy of that shape elsewhere, such as the
//   gate's artifacts/verify/AdoToolkit/<version>, is not an install.
// Anything else is refused: a source build, a copy under artifacts/, a hand-made layout.
internal sealed class ToolkitInstallation
{
    private const string ModuleRootName = "AdoToolkit";
    private const string PortableModuleFolder = "module";

    private ToolkitInstallation(AdoToolkitInstallMode mode, string folder, string root, Version currentVersion)
    {
        Mode = mode;
        Folder = folder;
        Root = root;
        CurrentVersion = currentVersion;
    }

    internal AdoToolkitInstallMode Mode { get; }
    // The running copy: <root>\AdoToolkit\<version>, or the portable folder.
    internal string Folder { get; }
    // Where new versions go: <root>\AdoToolkit, or the portable folder's parent.
    internal string Root { get; }
    internal Version CurrentVersion { get; }

    internal string TargetFor(Version version) => Mode == AdoToolkitInstallMode.Module
        ? Path.Combine(Root, UpdateHttp.VersionText(version))
        : Path.Combine(Root, "AdoToolkit-" + UpdateHttp.VersionText(version) + "-win-x64");

    internal static ToolkitInstallation Detect(ToolkitInstallContext context, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(culture);
        string? moduleBase = context.ModuleBase;
        Version? version = context.ModuleVersion;
        if (string.IsNullOrWhiteSpace(moduleBase) || version is null || version.Build < 0 || version.Revision >= 0)
            throw Unsupported(moduleBase ?? "", culture);
        DirectoryInfo baseFolder = new(Path.TrimEndingDirectorySeparator(Path.GetFullPath(moduleBase)));
        if (string.Equals(baseFolder.Name, PortableModuleFolder, StringComparison.OrdinalIgnoreCase)
            && baseFolder.Parent is { Parent: { } parent } folder
            && File.Exists(Path.Combine(folder.FullName, "Start-AdoToolkit.cmd"))
            && File.Exists(Path.Combine(folder.FullName, "runtime", "pwsh.exe")))
        {
            // Writes go beside the portable folder: the lock, staging and the new folder.
            EnsureNoLink(parent.FullName, culture);
            return new ToolkitInstallation(AdoToolkitInstallMode.Portable, folder.FullName, parent.FullName, version);
        }
        if (string.Equals(baseFolder.Name, UpdateHttp.VersionText(version), StringComparison.Ordinal)
            && baseFolder.Parent is { Parent: { } root } moduleRoot
            && string.Equals(moduleRoot.Name, ModuleRootName, StringComparison.OrdinalIgnoreCase)
            && IsOnModulePath(root.FullName, context.ModulePaths))
        {
            EnsureNoLink(moduleRoot.FullName, culture);
            return new ToolkitInstallation(AdoToolkitInstallMode.Module, baseFolder.FullName, moduleRoot.FullName, version);
        }
        throw Unsupported(baseFolder.FullName, culture);
    }

    // Compared as Install-AdoToolkit.ps1 compares its destination with PSModulePath: full paths
    // without a trailing separator, ignoring case.
    private static bool IsOnModulePath(string root, IReadOnlyList<string> modulePaths)
    {
        string wanted = Path.TrimEndingDirectorySeparator(root);
        foreach (string entry in modulePaths)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            string full;
            try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(entry.Trim())); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (string.Equals(full, wanted, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // A path whose links cannot be checked is not written to.
    private static void EnsureNoLink(string folder, CultureInfo culture)
    {
        string? link;
        try { link = UpdateGuards.FindLink(folder); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AdoFileOutputException(Messages.Get(AdoMessage.UpdateFolderUnreadable, culture, folder), error);
        }
        if (link is not null) throw new AdoConfigurationException(Messages.Get(AdoMessage.UpdateLinkedPath, culture, link));
    }

    private static AdoConfigurationException Unsupported(string folder, CultureInfo culture) =>
        new(Messages.Get(AdoMessage.UpdateUnsupportedLayout, culture, folder, UpdateHttp.ManualInstall.AbsoluteUri));
}
