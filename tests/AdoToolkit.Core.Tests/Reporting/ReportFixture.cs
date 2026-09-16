using System.Text.Json;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Json;
using AdoToolkit.Core.TestManagement;
using Json.Schema;

namespace AdoToolkit.Core.Tests.Reporting;

internal static class ReportFixture
{
    internal static readonly Uri Collection = new("https://ado.example.test/tfs/Collection%20A/");
    internal static readonly Uri Untrusted = new("https://untrusted.example.test/wrong");
    internal static readonly DateTimeOffset Timestamp = new(2026, 9, 15, 10, 30, 0, TimeSpan.FromHours(-3));
    internal const string Project = "Équipe / Web?#";
    internal const string FrenchText = "« L’été : 1 234 ! » 😀 é 日本語 <script> & \"quoted\"\nnext";

    private static readonly JsonSchema Schema = JsonSchema.FromFile(
        Path.Combine(TestDirectory.RepositoryRoot, "docs", "schemas", "testcase.v1.schema.json"),
        new BuildOptions { SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => throw new InvalidOperationException("External schema fetch forbidden") } });

    internal static ReportDocumentModel Model(string variant = "direct", string culture = "en-US") =>
        ReportModelBuilder.Build(Case(variant), new AdoConnection { CollectionUri = Collection, DefaultProject = "Wrong" }, Options(culture));

    internal static ReportModelOptions Options(string culture = "en-US") => new()
    {
        Culture = CultureInfo.GetCultureInfo(culture), GeneratedAt = Timestamp, ToolkitVersion = "2.1.0-test",
    };

    internal static AdoTestCase Case(string variant = "direct")
    {
        AdoSharedStepInfo shared = new() { Id = 20, Rev = 4, Title = "Shared <title>", TeamProject = "Autre / équipe", WebUrl = Untrusted, ReferenceCount = 1 };
        AdoSharedStepInfo inner = new() { Id = 30, Rev = 2, Title = "Nested", TeamProject = Project, WebUrl = Untrusted, ReferenceCount = 1 };
        AdoSharedStepInfo unresolved = new() { Id = 99, ReferenceCount = 1 };
        List<AdoTestStep> steps =
        [
            new() { Sequence = 1, Number = "1", Kind = AdoTestStepKind.Action, Action = variant is "french" or "review" ? FrenchText : "Enter @user",
                ExpectedResult = "", ActionSource = "<p>Original &amp; @user</p>", ExpectedResultSource = "",
                SourceWorkItemId = 10, SourceRev = 3, SourceStepId = 11 },
        ];
        List<AdoSharedStepInfo> sharedSteps = [];
        List<AdoDiagnostic> diagnostics = [];
        if (variant is "nested" or "review")
        {
            sharedSteps.AddRange([shared, inner]);
            steps.Add(new() { Sequence = 2, Number = "2", Kind = AdoTestStepKind.SharedStep, SharedStep = shared,
                IsExpanded = true, SourceWorkItemId = 10, SourceRev = 3 });
            steps.Add(new() { Sequence = 3, Number = "2.1", Kind = AdoTestStepKind.SharedStep, SharedStep = inner,
                IsExpanded = true, SourceWorkItemId = 20, SourceRev = 4, SharedStepPath = [shared] });
            steps.Add(new() { Sequence = 4, Number = "2.1.1", Kind = AdoTestStepKind.Validate, Action = "Confirm",
                ExpectedResult = "Visible", ActionSource = "<p>Confirm</p>", ExpectedResultSource = "<b>Visible</b>",
                SourceWorkItemId = 30, SourceRev = 2, SourceStepId = 21, SharedStepPath = [shared, inner] });
        }
        if (variant == "partial")
        {
            sharedSteps.Add(unresolved);
            steps.Add(new() { Sequence = 2, Number = "2", Kind = AdoTestStepKind.SharedStep, SharedStep = unresolved,
                SourceWorkItemId = 10, SourceRev = 3, DiagnosticCode = DiagnosticCodes.UnresolvedSharedStep });
            steps.Add(new() { Sequence = 3, Number = "3", Kind = AdoTestStepKind.Truncated, SourceWorkItemId = 10,
                SourceRev = 3, DiagnosticCode = DiagnosticCodes.ExpansionLimitExceeded });
            foreach ((string code, string number) in new[] { (DiagnosticCodes.UnresolvedSharedStep, "2"), (DiagnosticCodes.ExpansionLimitExceeded, "3") })
                diagnostics.Add(DiagnosticMessageRenderer.Create(code, CultureInfo.GetCultureInfo("en-US"), 10,
                    stepNumber: number, referenceChain: [10, 99]));
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.MalformedStepsXml, CultureInfo.GetCultureInfo("en-US"), 99, ["7", "19"]));
        }
        bool full = variant is "parameterized" or "review";
        return new AdoTestCase
        {
            Id = 10, Rev = 3, Title = variant == "french" ? FrenchText : "Synthetic case <title>",
            WorkItemType = "Cas personnalisé", TeamProject = Project, State = "Ready", CollectionUri = Collection,
            WebUrl = Untrusted, ChangedDate = Timestamp.AddDays(-2), RetrievedAt = Timestamp.AddMinutes(-1),
            Priority = full ? 2 : null, AutomationStatus = full ? "Not Automated" : null,
            AreaPath = full ? "Area\\Équipe" : null, IterationPath = full ? "Iteration\\One" : null,
            AssignedTo = full ? new AdoIdentityRef { Id = "opaque-id", DisplayName = "Fictional Person", UniqueName = "person@example.test" } : null,
            ChangedBy = full ? new AdoIdentityRef { DisplayName = "Another Person" } : null,
            Steps = steps, SharedSteps = sharedSteps, Diagnostics = diagnostics,
            Suite = full ? new AdoTestSuiteRef { PlanId = 40, SuiteId = 50, PlanName = "Plan", SuiteName = "Suite", SuitePath = ["Root", "Suite"],
                TeamProject = Project, CollectionUri = Collection, WebUrl = Untrusted, PlanWebUrl = Untrusted } : null,
            Parameters = full ? new AdoTestParameters
            {
                Source = AdoParameterSource.Shared, Names = ["user", "résultat"],
                Rows = [new Dictionary<string, string>(StringComparer.Ordinal) { ["user"] = "alice", ["résultat"] = FrenchText }],
                SharedParameterSets = [new() { Id = 60, Rev = 2, Title = "Shared values", TeamProject = "Paramètres / A", WebUrl = Untrusted }],
            } : new(),
        };
    }

    internal static string Render(ReportDocumentModel model, bool includeSource = false)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        JsonTestCaseRenderer.Render(model, writer, includeSource);
        string json = writer.ToString();
        AssertValid(json);
        return json;
    }

    internal static void AssertValid(string json)
    {
        EvaluationResults result = Evaluate(json);
        Assert.True(result.IsValid, JsonSerializer.Serialize(result));
    }

    internal static EvaluationResults Evaluate(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List, RequireFormatValidation = true, Culture = CultureInfo.GetCultureInfo("en-US"),
        });
    }
}
