using AdoToolkit.Completion;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit;

[Cmdlet(VerbsLifecycle.Invoke, "AdoWiql", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoWiqlResult), typeof(AdoWorkItem))]
public sealed class InvokeAdoWiqlCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Query { get; set; } = "";

    [Parameter]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter]
    [ValidateRange(1, WiqlService.MaximumResults)]
    public int? Top { get; set; }

    [Parameter]
    public SwitchParameter Hydrate { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = ResolveConnection(Connection);
        string project = ResolveProject(Project, connection);
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        AdoWiqlResult result;
        try
        {
            result = RunWorker((log, token) => new WiqlService(lease.Client, connection, log)
                .QueryAsync(project, Query, Top, MessageCulture, token));
        }
        catch (NotSupportedException error)
        {
            WriteError(new ErrorRecord(error, "NotSupported", ErrorCategory.NotImplemented, null) { ErrorDetails = new ErrorDetails(error.Message) });
            return;
        }
        if (!Hydrate)
        {
            WriteObject(result);
            return;
        }
        if (result.Ids.Count == 0) return;
        IReadOnlyList<AdoWorkItem> items = RunWorker((log, token) => new WiqlService(lease.Client, connection, log)
            .HydrateAsync(result, MessageCulture, token));
        Dictionary<int, AdoWorkItem> byId = items.ToDictionary(static item => item.Id);
        foreach (int id in result.Ids)
        {
            if (byId.TryGetValue(id, out AdoWorkItem? item)) WriteObject(item);
            else
            {
                AdoNotFoundException error = new(Messages.Get(AdoMessage.WorkItemNotFound, MessageCulture, id.ToString(CultureInfo.InvariantCulture)))
                { Operation = "WorkItemsBatch" };
                ErrorRecord record = ErrorRecordFactory.Create(error);
                WriteError(new ErrorRecord(error, record.FullyQualifiedErrorId, record.CategoryInfo.Category, id) { ErrorDetails = record.ErrorDetails });
            }
        }
    });
}
