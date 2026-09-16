using AdoToolkit.Completion;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoTestSuite", DefaultParameterSetName = "ByPlanId")]
[OutputType(typeof(AdoTestSuite))]
public sealed class GetAdoTestSuiteCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByPlanId")]
    [ValidateRange(1, int.MaxValue)]
    public int PlanId { get; set; }

    // A typed plan binds by value only, so no other object's Id can become a plan ID.
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "ByPlan")]
    [ValidateNotNull]
    public AdoTestPlan? InputObject { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? SuiteId { get; set; }

    [Parameter]
    public SwitchParameter Recurse { get; set; }

    [Parameter(ParameterSetName = "ByPlanId")]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = ResolveConnection(Connection);
        int planId = PlanId;
        string project;
        if (InputObject is not null)
        {
            EnsureSameCollection(InputObject.CollectionUri, connection);
            planId = InputObject.Id;
            project = InputObject.TeamProject;
        }
        else project = ResolveProject(Project, connection);
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        IReadOnlyList<AdoTestSuite> suites = RunWorker((log, token) => new TestSuiteService(lease.Client, connection, log)
            .GetSuitesAsync(project, planId, SuiteId, Recurse, MessageCulture, token));
        foreach (AdoTestSuite suite in suites) WriteObject(suite);
    });
}
