namespace AdoToolkit.Core.Update;

// What the cmdlet knows about the running copy: the module's own base folder and version, the
// folders of PSModulePath, and the version of the PowerShell that runs it.
public sealed class ToolkitInstallContext
{
    public string? ModuleBase { get; init; }
    public Version? ModuleVersion { get; init; }
    public IReadOnlyList<string> ModulePaths { get; init; } = [];
    public required Version PowerShellVersion { get; init; }
}
