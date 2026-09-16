using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoBuildTimeline", DefaultParameterSetName = "ByBuildId")]
[OutputType(typeof(AdoTimelineRecord))]
public sealed class GetAdoBuildTimelineCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByBuildId")]
    [ValidateRange(1, int.MaxValue)]
    public int BuildId { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "ByBuild")]
    [ValidateNotNull]
    public AdoBuild? InputObject { get; set; }

    [Parameter(ParameterSetName = "ByBuildId")]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = ResolveConnection(Connection);
        if (InputObject is not null) EnsureSameCollection(InputObject.CollectionUri, connection);
        string project = InputObject?.TeamProject ?? ResolveProject(Project, connection);
        int id = InputObject?.Id ?? BuildId;
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        IReadOnlyList<AdoTimelineRecord> records = RunWorker((log, token) => new TimelineService(lease.Client, connection, log)
            .GetTimelineAsync(project, id, MessageCulture, token));
        foreach (AdoTimelineRecord record in records) WriteObject(record);
    });
}
