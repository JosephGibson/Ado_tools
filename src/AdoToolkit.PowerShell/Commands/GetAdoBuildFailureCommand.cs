using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoBuildFailure", DefaultParameterSetName = "ByBuildId")]
[OutputType(typeof(AdoBuildFailure))]
public sealed class GetAdoBuildFailureCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByBuildId")]
    [ValidateRange(1, int.MaxValue)]
    public int BuildId { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "ByBuild")]
    [ValidateNotNull]
    public AdoBuild? InputObject { get; set; }

    [Parameter]
    public SwitchParameter IncludeWarnings { get; set; }

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
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        IReadOnlyList<AdoBuildFailure> failures = RunWorker(async (log, token) =>
        {
            AdoBuild build = InputObject ?? await new BuildService(lease.Client, connection, log)
                .GetByIdAsync(project, BuildId, MessageCulture, token).ConfigureAwait(false);
            return await new TimelineService(lease.Client, connection, log)
                .GetFailuresAsync(build, IncludeWarnings, MessageCulture, token).ConfigureAwait(false);
        });
        foreach (AdoBuildFailure failure in failures) WriteObject(failure);
    });
}
