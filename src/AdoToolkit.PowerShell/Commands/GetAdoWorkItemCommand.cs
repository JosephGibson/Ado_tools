using AdoToolkit.Core.WorkItems;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoWorkItem", DefaultParameterSetName = "Fields")]
[OutputType(typeof(AdoWorkItem))]
public sealed class GetAdoWorkItemCommand : AdoCmdletBase
{
    private readonly List<int> ids = [];
    private readonly HashSet<int> seen = [];
    private AdoConnection? resolved;

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateRange(1, int.MaxValue)]
    [ValidateNotNullOrEmpty]
    public int[] Id { get; set; } = [];

    [Parameter(ParameterSetName = "Fields")]
    [ValidateNotNullOrEmpty]
    public string[]? Field { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Relations")]
    public SwitchParameter IncludeRelations { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    // Bind provenance alongside Id, before the pipeline input is reduced to integers.
    [Parameter(ValueFromPipelineByPropertyName = true, DontShow = true)]
    public Uri? CollectionUri { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        resolved ??= ResolveConnection(Connection);
        if (CollectionUri is not null) EnsureSameCollection(CollectionUri, resolved);
        foreach (int id in Id)
            if (seen.Add(id)) ids.Add(id);
    });

    protected override void EndProcessing() => RunLocal(() =>
    {
        if (ids.Count == 0) return;
        using ClientLease lease = SessionStateRegistry.Current.Acquire(resolved!);
        IReadOnlyList<AdoWorkItem> items = RunWorker((log, token) => new WorkItemService(lease.Client, resolved!, log)
            .GetWorkItemsAsync(ids, MessageCulture, token, Field, IncludeRelations));
        Dictionary<int, AdoWorkItem> byId = items.ToDictionary(static item => item.Id);
        foreach (int id in ids)
        {
            if (byId.TryGetValue(id, out AdoWorkItem? item)) WriteObject(item);
            else
            {
                AdoNotFoundException error = new(Messages.Get(AdoMessage.WorkItemNotFound, MessageCulture, id.ToString(CultureInfo.InvariantCulture)))
                { Operation = "WorkItemsBatch" };
                ErrorRecord record = ErrorRecordFactory.Create(error, MessageCulture);
                WriteError(new ErrorRecord(error, record.FullyQualifiedErrorId, record.CategoryInfo.Category, id)
                    { ErrorDetails = record.ErrorDetails });
            }
        }
    });
}
