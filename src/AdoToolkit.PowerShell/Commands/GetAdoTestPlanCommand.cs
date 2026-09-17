using System.Text;
using AdoToolkit.Completion;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoTestPlan", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoTestPlan))]
public sealed class GetAdoTestPlanCommand : AdoCmdletBase
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
        IReadOnlyList<AdoTestPlan> plans = RunWorker((log, token) => new TestPlanService(lease.Client, connection, log)
            .GetPlansAsync(project, MessageCulture, token));
        // Accents are significant and composed/decomposed forms match (§11.5); output keeps the original text.
        WildcardPattern? pattern = Name is null ? null
            : new(Name.Normalize(NormalizationForm.FormC), WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
        bool found = false;
        foreach (AdoTestPlan plan in plans)
        {
            if (Id.HasValue && plan.Id != Id.Value) continue;
            found = true;
            if (pattern is not null && !pattern.IsMatch(plan.Name.Normalize(NormalizationForm.FormC))) continue;
            WriteObject(plan);
        }
        if (Id.HasValue && !found)
        {
            AdoNotFoundException error = new(Messages.Get(AdoMessage.TestPlanNotFound, MessageCulture, Id.Value.ToString(CultureInfo.InvariantCulture)))
            { Operation = "TestPlansList", Project = project };
            ErrorRecord record = ErrorRecordFactory.Create(error, MessageCulture);
            WriteError(new ErrorRecord(error, record.FullyQualifiedErrorId, record.CategoryInfo.Category, Id.Value) { ErrorDetails = record.ErrorDetails });
        }
    });
}
