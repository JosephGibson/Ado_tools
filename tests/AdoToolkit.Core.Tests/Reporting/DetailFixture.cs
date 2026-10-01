using AdoToolkit.Core.RichText;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting;

// What -IncludeDetail adds to a Test Case: summary fields, links of every kind, attachments and
// test points with each outcome, plus one lookup diagnostic.
internal static class DetailFixture
{
    internal const string Description = "<p>Checks the <b>order</b> form.</p><ul><li>Uses the sandbox</li><li>See https://docs.example.test/orders</li></ul>";

    internal static IReadOnlyDictionary<int, AdoTestCaseDetail> Details(params int[] ids) => ids.ToDictionary(static id => id, static id => Detail(id));

    internal static AdoTestCaseDetail Detail(int id = 10, IReadOnlyList<AdoTestPoint>? points = null) => new()
    {
        Id = id, IsResolved = true,
        Description = PlainTextConverter.Convert(Description, CultureInfo.GetCultureInfo("en-US")).Text, DescriptionSource = Description,
        Tags = ["Smoke", "Été <b>"], CreatedBy = new AdoIdentityRef { DisplayName = "Fictional Author", UniqueName = "author@example.test" },
        CreatedDate = ReportFixture.Timestamp.AddDays(-30).ToUniversalTime(),
        AutomatedTestName = "Synthetic.Orders.OrderTests.Creates<T>", AutomatedTestStorage = "Synthetic.Orders.Tests.dll", AutomatedTestType = "Unit Test",
        Links =
        [
            Link(3050, "Microsoft.VSTS.Common.TestedBy-Reverse", "Tests", "User Story", "Order entry <story>", "Active", ReportFixture.Project),
            Link(3001, "System.LinkTypes.Related", "Related", "Bug", "Total is wrong", "Closed", "Autre / équipe", "See <also>"),
            new AdoLinkedWorkItem
            {
                Id = 3099, LinkType = "Contoso.LinkTypes.Blocks-Forward", WebUrl = AdoWebLinks.WorkItem(ReportFixture.Collection, ReportFixture.Project, 3099),
            },
        ],
        Hyperlinks = [new() { Url = "https://wiki.example.test/orders", Comment = "Specification" }, new() { Url = "javascript:alert(1)" }],
        Attachments = [new() { Name = "capture <1>.png", Size = 20480, Comment = "Screen" }, new() { Name = "notes.txt" }],
        Points = points ??
        [
            Point(1, 40, "Plan « Été »", 51, "Connexion", "Windows 11", "Passed", 701),
            Point(2, 40, "Plan « Été »", 52, "Paiement <b>", "Windows 11", "Failed", 702),
            Point(3, 41, null, 60, null, "Chrome", "Blocked", null),
            Point(4, 41, null, 60, null, "Edge", null, null),
        ],
        Diagnostics = [DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedLinkedWorkItem, CultureInfo.GetCultureInfo("en-US"), id, ["3099"])],
    };

    internal static AdoTestPoint Point(int id, int plan, string? planName, int suite, string? suiteName, string configuration, string? outcome, int? run) => new()
    {
        Id = id, PlanId = plan, PlanName = planName, SuiteId = suite, SuiteName = suiteName, ConfigurationName = configuration,
        Outcome = outcome, HasRun = outcome is not null,
        OutcomeClass = outcome switch { "Passed" => AdoTestOutcomeClass.Pass, "Failed" => AdoTestOutcomeClass.Failure, _ => AdoTestOutcomeClass.Other },
        State = outcome is null ? "Ready" : "Completed", Tester = new AdoIdentityRef { DisplayName = "Fictional Tester" },
        LastRunId = run, LastResultId = run is null ? null : 100000 + id, LastUpdated = run is null ? null : ReportFixture.Timestamp.AddDays(-1).ToUniversalTime(),
        TeamProject = ReportFixture.Project,
        PlanWebUrl = AdoWebLinks.TestPlan(ReportFixture.Collection, ReportFixture.Project, plan),
        SuiteWebUrl = AdoWebLinks.TestPlan(ReportFixture.Collection, ReportFixture.Project, plan, suite),
        LastRunWebUrl = run is int runId ? AdoWebLinks.TestRun(ReportFixture.Collection, ReportFixture.Project, runId) : null,
    };

    private static AdoLinkedWorkItem Link(int id, string type, string name, string workItemType, string title, string state, string project, string? comment = null) => new()
    {
        Id = id, LinkType = type, LinkName = name, Comment = comment, WorkItemType = workItemType, Title = title, State = state, TeamProject = project,
        IsResolved = true, WebUrl = AdoWebLinks.WorkItem(ReportFixture.Collection, project, id),
    };
}
