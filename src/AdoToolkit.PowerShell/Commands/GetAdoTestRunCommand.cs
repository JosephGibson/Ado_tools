using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoTestRun", DefaultParameterSetName = "ByBuildId")]
[OutputType(typeof(AdoTestRun))]
public sealed class GetAdoTestRunCommand : AdoCmdletBase
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
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        IReadOnlyList<AdoTestRun> runs = RunWorker(async (log, token) =>
        {
            AdoBuild build = InputObject ?? await new BuildService(lease.Client, connection, log)
                .GetAsync(project, BuildId, MessageCulture, token).ConfigureAwait(false);
            return await new TestRunService(lease.Client, connection, log)
                .GetRunsAsync(build, MessageCulture, token).ConfigureAwait(false);
        });
        foreach (AdoTestRun run in runs) WriteObject(run);
    });
}
