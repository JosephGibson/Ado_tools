using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

// Three cases in two suites; case 10 belongs to both, so the document has four occurrences.
internal static class MultiCaseFixture
{
    internal static readonly AdoTestSuiteRef First = Suite(51, "Connexion");
    internal static readonly AdoTestSuiteRef Second = Suite(52, "Paiement <b>");

    internal static IReadOnlyList<AdoTestCase> Cases() =>
    [
        Occurrence(10, "nested", First), Occurrence(11, "direct", First),
        Occurrence(10, "nested", Second), Occurrence(12, "partial", Second),
    ];

    internal static ReportDocumentModel Model(string culture = "en-US", IReadOnlyList<AdoTestCase>? cases = null) =>
        ReportModelBuilder.Build(cases ?? Cases(), new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options(culture));

    internal static AdoTestCase Occurrence(int id, string variant, AdoTestSuiteRef? suite)
    {
        AdoTestCase source = ReportFixture.Case(variant);
        return new AdoTestCase
        {
            Id = id, Rev = source.Rev, Title = "Synthetic case " + id.ToString(CultureInfo.InvariantCulture) + " <title>",
            WorkItemType = source.WorkItemType, TeamProject = source.TeamProject, State = source.State, Priority = source.Priority,
            AutomationStatus = source.AutomationStatus, AreaPath = source.AreaPath, IterationPath = source.IterationPath,
            AssignedTo = source.AssignedTo, ChangedBy = source.ChangedBy, ChangedDate = source.ChangedDate, RetrievedAt = source.RetrievedAt,
            WebUrl = source.WebUrl, CollectionUri = source.CollectionUri, Steps = source.Steps, SharedSteps = source.SharedSteps,
            Diagnostics = source.Diagnostics, Parameters = source.Parameters, Suite = suite,
        };
    }

    private static AdoTestSuiteRef Suite(int suiteId, string name) => new()
    {
        PlanId = 40, SuiteId = suiteId, PlanName = "Plan « Été »", SuiteName = name, SuitePath = ["Racine", name],
        TeamProject = ReportFixture.Project, CollectionUri = ReportFixture.Collection, WebUrl = ReportFixture.Untrusted, PlanWebUrl = ReportFixture.Untrusted,
    };
}
