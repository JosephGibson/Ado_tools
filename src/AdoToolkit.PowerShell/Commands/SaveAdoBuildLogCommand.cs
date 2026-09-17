using System.IO;
using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.IO;

namespace AdoToolkit;

[Cmdlet(VerbsData.Save, "AdoBuildLog", SupportsShouldProcess = true, DefaultParameterSetName = "ByLogId")]
[OutputType(typeof(FileInfo))]
public sealed class SaveAdoBuildLogCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = "ByLogId")]
    [ValidateRange(1, int.MaxValue)]
    public int BuildId { get; set; }

    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "ByLogId")]
    [AllowNull]
    [ValidateOptionalId]
    public int? LogId { get; set; }

    [Parameter] [ValidateRange(1, int.MaxValue)] public int? Tail { get; set; }
    [Parameter] [ValidateNotNullOrEmpty] public string? Path { get; set; }
    [Parameter] public AdoConnection? Connection { get; set; }
    [Parameter(ValueFromPipelineByPropertyName = true)] [Alias("TeamProject")]
    [ArgumentCompleter(typeof(ProjectNameCompleter))] [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter(ValueFromPipelineByPropertyName = true, DontShow = true)]
    public Uri? CollectionUri { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        if (!LogId.HasValue)
        {
            WriteError(new ErrorRecord(new InvalidOperationException(Messages.Get(AdoMessage.BuildLogNotAvailable,
                MessageCulture, BuildId.ToString(CultureInfo.InvariantCulture))),
                "BuildLogNotAvailable", ErrorCategory.ObjectNotFound, BuildId));
            return;
        }
        AdoConnection connection = ResolveConnection(Connection);
        if (CollectionUri is not null) EnsureSameCollection(CollectionUri, connection);
        string project = ResolveProject(Project, connection);
        string? resolved = null;
        if (Path is not null)
        {
            resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path, out ProviderInfo provider, out _);
            if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase))
                ThrowTerminatingError(new ErrorRecord(new ArgumentException(Messages.Get(AdoMessage.FileSystemPathRequired, MessageCulture)),
                    "FileSystemPathRequired", ErrorCategory.InvalidArgument, Path));
            if (!Directory.Exists(resolved))
                throw new AdoFileOutputException(Messages.Get(AdoMessage.BuildLogDirectoryRequired, MessageCulture, resolved));
        }
        string destination = ReportFileNames.Resolve(resolved, ReportFileNames.BuildLog(BuildId, LogId.Value), MessageCulture);
        if (!ShouldProcess(destination, Messages.Get(AdoMessage.SaveBuildLog, MessageCulture))) return;
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        FileInfo file = RunWorker((log, token) => new BuildLogService(lease.Client, connection, log)
            .SaveAsync(project, BuildId, LogId.Value, destination, Tail, MessageCulture, token));
        WriteObject(file);
    });
}
