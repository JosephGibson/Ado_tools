using System.IO;
using System.Net.Http;
using AdoToolkit.Core.Update;

namespace AdoToolkit;

// Installs the newest GitHub release beside the running copy. The updater is isolated from the
// Azure DevOps side: no connection, profile, configuration or AdoHttpPipeline. A binary module cannot
// load a newer copy of itself into a process that loaded it (PowerShell/PowerShell#13575), so the
// command never imports the new version or opens a window: the user opens a new one.
[Cmdlet(VerbsData.Update, "AdoToolkit", SupportsShouldProcess = true, DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoToolkitUpdate))]
public sealed class UpdateAdoToolkitCommand : AdoCmdletBase
{
    private const int ProgressActivityId = 1;
    // PSHOST makes the messages visible by default, like Write-Host, while the success stream holds
    // only the result.
    private static readonly string[] InformationTags = ["PSHOST"];
    private Version? downloading;
    private string? archive;
    private int? downloadTotal;

    // The test seam: the Pester tests set a handler here through reflection, so that no test reaches
    // GitHub. Production leaves it null; nothing in src/ assigns it (UpdateIsolationTests).
    internal static HttpMessageHandler? TestTransport { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        PSModuleInfo? module = MyInvocation.MyCommand.Module;
        ToolkitInstallContext context = new()
        {
            ModuleBase = module?.ModuleBase,
            ModuleVersion = module?.Version,
            ModulePaths = (Environment.GetEnvironmentVariable("PSModulePath") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            PowerShellVersion = PSVersionInfo.PSVersion,
        };
        using ToolkitUpdater updater = new(ModuleManifestReader.Read, TestTransport);
        // Reads the release and writes nothing, so -WhatIf runs it too.
        ToolkitUpdatePlan plan = RunWorker((log, token) => updater.PrepareAsync(context, log, MessageCulture, token));
        if (plan.Result is { } current)
        {
            Announce(current);
            WriteObject(current);
            return;
        }
        if (!ShouldProcess(plan.TargetPath, Messages.Get(AdoMessage.UpdateAction, MessageCulture, Text(plan.LatestVersion)))) return;
        downloading = plan.LatestVersion;
        archive = plan.ArchiveName;
        AdoToolkitUpdate result;
        try { result = RunWorker((log, token) => updater.InstallAsync(plan, log, MessageCulture, token)); }
        finally { CompleteProgress(); }
        Announce(result);
        WriteObject(result);
    });

    private void Announce(AdoToolkitUpdate result)
    {
        string message = result.Status switch
        {
            AdoToolkitUpdateStatus.UpToDate when result.LatestVersion == result.CurrentVersion =>
                Messages.Get(AdoMessage.UpdateUpToDate, MessageCulture, Text(result.CurrentVersion)),
            AdoToolkitUpdateStatus.UpToDate =>
                Messages.Get(AdoMessage.UpdateNewerThanRelease, MessageCulture, Text(result.CurrentVersion), Text(result.LatestVersion)),
            AdoToolkitUpdateStatus.AlreadyInstalled =>
                Messages.Get(AdoMessage.UpdateAlreadyInstalledModule, MessageCulture, Text(result.LatestVersion), result.Path),
            _ when result.InstallMode == AdoToolkitInstallMode.Portable =>
                Messages.Get(AdoMessage.UpdateInstalledPortable, MessageCulture, Text(result.LatestVersion), result.Path,
                    Text(result.CurrentVersion), result.PreviousPath),
            _ => Messages.Get(AdoMessage.UpdateInstalledModule, MessageCulture, Text(result.LatestVersion), result.Path, Text(result.CurrentVersion)),
        };
        WriteInformation(new HostInformationMessage { Message = message }, InformationTags);
    }

    private protected override void OnProgress(AdoProgress progress)
    {
        if (progress.Phase != AdoProgressPhase.ReleaseDownload || downloading is null) return;
        int total = progress.Total ?? progress.Completed;
        downloadTotal = total;
        WriteProgress(new ProgressRecord(ProgressActivityId, Messages.Get(AdoMessage.UpdateProgressActivity, MessageCulture, Text(downloading)),
            Messages.Get(AdoMessage.UpdateProgressDownload, MessageCulture, archive, progress.Completed, total))
        {
            PercentComplete = total > 0 ? (int)Math.Min(100, progress.Completed * 100L / total) : -1,
        });
    }

    private void CompleteProgress()
    {
        if (downloading is null || downloadTotal is not int total) return;
        WriteProgress(new ProgressRecord(ProgressActivityId, Messages.Get(AdoMessage.UpdateProgressActivity, MessageCulture, Text(downloading)),
            Messages.Get(AdoMessage.UpdateProgressDownload, MessageCulture, archive, total, total))
        { RecordType = ProgressRecordType.Completed });
    }

    private static string Text(Version version) => version.ToString(3);
}
