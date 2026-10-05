namespace AdoToolkit.Core.Update;

// The manifest values the updater checks. Core cannot parse a PowerShell data file (it references no
// System.Management.Automation), so the PowerShell side reads them as Import-PowerShellDataFile does.
// A value that is not a string is null.
public sealed class ModuleManifestFacts
{
    public string? ModuleVersion { get; init; }
    public string? RootModule { get; init; }
    public string? PowerShellVersion { get; init; }
}
