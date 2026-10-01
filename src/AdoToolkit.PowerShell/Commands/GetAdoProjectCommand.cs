namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoProject", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoProject))]
public sealed class GetAdoProjectCommand : AdoCmdletBase
{
    [Parameter]
    public AdoConnection? Connection { get; set; }

    [Parameter(Position = 0)]
    [SupportsWildcards]
    public string? Name { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Top { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = ResolveConnection(Connection);
        SessionStateHolder state = SessionStateRegistry.Current;
        string key = SessionStateHolder.Key(connection);
        if (!state.Projects.TryGet(key + ":all", out IReadOnlyList<AdoProject>? projects))
        {
            using ClientLease lease = state.Acquire(connection);
            projects = RunWorker((log, token) => new ProjectService(lease.Client, connection, log).GetProjectsAsync(MessageCulture, token));
            state.Projects.Set(key + ":completion", projects.Take(256).ToArray());
            if (projects.Count <= 256) state.Projects.Set(key + ":all", projects);
        }
        NameFilter filter = new(Name);
        int emitted = 0;
        foreach (AdoProject project in projects)
        {
            if (!filter.IsMatch(project.Name)) continue;
            WriteObject(project);
            if (Top > 0 && ++emitted >= Top) break;
        }
    });
}
