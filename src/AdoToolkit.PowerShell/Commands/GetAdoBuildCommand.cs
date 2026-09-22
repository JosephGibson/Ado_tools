using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoBuild", DefaultParameterSetName = "ByDefinition")]
[OutputType(typeof(AdoBuild))]
public sealed class GetAdoBuildCommand : AdoCmdletBase
{
    // Optional so the connected profile's default definition can apply.
    [Parameter(Position = 0, ParameterSetName = "ByDefinition")]
    [ValidateNotNull]
    public object? Definition { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "ByDefinitionObject")]
    [ValidateNotNull]
    public AdoBuildDefinition? InputObject { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Branch { get; set; }

    [Parameter]
    public SwitchParameter Latest { get; set; }

    [Parameter]
    public BuildStatus? Status { get; set; }

    [Parameter]
    public BuildResult? Result { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? Top { get; set; }

    [Parameter(ParameterSetName = "ByDefinition")]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = ResolveConnection(Connection);
        int? id = null;
        string? name = null;
        string project;
        if (InputObject is not null)
        {
            EnsureSameCollection(InputObject.CollectionUri, connection);
            id = InputObject.Id;
            project = InputObject.TeamProject;
        }
        else
        {
            project = ResolveProject(Project, connection);
            (id, name) = ResolveDefinition(Definition, connection);
        }
        BuildQuery query = new()
        {
            DefinitionId = id, DefinitionName = name, Branch = Branch ?? connection.DefaultBranch, Latest = Latest, Status = Status,
            Result = Result, Top = Top,
        };
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        IReadOnlyList<AdoBuild> builds = RunWorker((log, token) => new BuildService(lease.Client, connection, log)
            .GetBuildsAsync(project, query, MessageCulture, token));
        foreach (AdoBuild build in builds) WriteObject(build);
    });
}
