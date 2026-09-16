using System.Text;
using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoBuildDefinition", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoBuildDefinition))]
public sealed class GetAdoBuildDefinitionCommand : AdoCmdletBase
{
    [Parameter]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? Id { get; set; }

    [Parameter(Position = 0)]
    [SupportsWildcards]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = ResolveConnection(Connection);
        string project = ResolveProject(Project, connection);
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        IReadOnlyList<AdoBuildDefinition> definitions = RunWorker((log, token) => new BuildDefinitionService(lease.Client, connection, log)
            .GetDefinitionsAsync(project, MessageCulture, token));
        WildcardPattern? pattern = Name is null ? null
            : new(Name.Normalize(NormalizationForm.FormC), WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
        if (Id.HasValue && !definitions.Any(item => item.Id == Id.Value))
            throw new AdoNotFoundException(Messages.Get(AdoMessage.BuildDefinitionNotFound, MessageCulture, Id.Value.ToString(CultureInfo.InvariantCulture)))
            { Operation = "BuildDefinitionsList", Project = project };
        foreach (AdoBuildDefinition definition in definitions)
            if ((!Id.HasValue || definition.Id == Id.Value) && (pattern is null || pattern.IsMatch(definition.Name.Normalize(NormalizationForm.FormC))))
                WriteObject(definition);
    });
}
