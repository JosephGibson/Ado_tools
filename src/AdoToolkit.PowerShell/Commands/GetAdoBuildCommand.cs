using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoBuild", DefaultParameterSetName = "ByDefinition")]
[OutputType(typeof(AdoBuild))]
public sealed class GetAdoBuildCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByDefinition")]
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
            object? value = Definition is PSObject wrapped ? wrapped.BaseObject : Definition;
            if (value is int number && number > 0) id = number;
            else if (value is string text && !string.IsNullOrWhiteSpace(text)) name = text;
            else throw new AdoRequestException(Messages.Get(AdoMessage.InvalidBuildDefinition, MessageCulture));
        }
        BuildQuery query = new() { DefinitionId = id, DefinitionName = name, Branch = Branch, Latest = Latest, Status = Status, Result = Result, Top = Top };
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        IReadOnlyList<AdoBuild> builds = RunWorker((log, token) => new BuildService(lease.Client, connection, log)
            .GetBuildsAsync(project, query, MessageCulture, token));
        foreach (AdoBuild build in builds) WriteObject(build);
    });
}
