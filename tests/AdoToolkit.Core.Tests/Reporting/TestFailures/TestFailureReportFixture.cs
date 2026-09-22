using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

internal static class TestFailureReportFixture
{
    internal static readonly Uri Collection = new("https://ado.example.test/tfs/Collection%20A/");
    internal static readonly Uri Untrusted = new("https://untrusted.example.test/ignore-this-response-url");
    internal const string Project = "Équipe / Web?#";
    internal const string Hostile = "</script><script>alert('fixture-only')</script> <img src=x onerror=fixture()> \" & ../CON 日本語 « L’été : 1 234 ! »";
    internal static readonly DateTimeOffset Clock = new(2026, 9, 16, 13, 30, 0, TimeSpan.Zero);

    // Reviewed reports show every failure; the default flaky exclusion has its own tests.
    internal static TestFailureReportOptions Options(string culture) => new()
    { Culture = culture, SessionCulture = CultureInfo.GetCultureInfo("en-US"), GeneratedAt = Clock, ToolkitVersion = "5.3.0-test", IncludeFlaky = true };

    internal static TestFailureReportModel Model(string variant = "failed", string culture = "en-US") => TestFailureReportModelBuilder.Build(Set(variant), Options(culture));

    internal static string Render(string variant = "failed", string culture = "en-US") => Render(Model(variant, culture));

    internal static string Render(TestFailureReportModel model)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        HtmlTestFailureRenderer.Render(model, writer);
        return writer.ToString();
    }

    internal static AdoBuildTestFailureSet Set(string variant)
    {
        if (variant == "grouped") return GroupedSet();
        bool hostile = variant == "hostile";
        bool partial = variant is "partial" or "review";
        bool flaky = variant is "flaky" or "review";
        List<AdoDiagnostic> diagnostics = [];
        if (partial) diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.FailureLimitExceeded, CultureInfo.GetCultureInfo("en-US"), arguments: ["8", "1"]));
        if (partial || hostile) diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedTestCase, CultureInfo.GetCultureInfo("en-US"), 902, ["902"]));
        if (hostile) diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.InvalidTestCaseReference, CultureInfo.GetCultureInfo("en-US"), arguments: ["11", "201"]));
        List<AdoTestFailure> failures = [Failure(hostile, false, partial, 1)];
        if (flaky) failures.Add(Failure(false, true, false, 2));
        if (hostile) failures.Add(new AdoTestFailure { Ordinal = 2, ShortName = "InvalidReference", CollectionUri = Collection,
            TestName = "Synthetic.Security.InvalidReference", Attempts = [Attempt(1, false, true)] });
        AdoBuildTestSummary summary = Summary(401, true, true, failures.Count(f => f.Classification == AdoTestFailureClassification.Failed), flaky ? 1 : 0);
        return new AdoBuildTestFailureSet
        {
            Build = new AdoBuild { Id = 401, BuildNumber = hostile ? Hostile : "20260916.3", Definition = new AdoBuildDefinitionRef { Id = 12, Name = "Synthetic UI tests" },
                SourceBranch = "refs/heads/main", SourceVersion = "0123456789abcdef0123456789abcdef01234567", RepositoryType = "TfsGit",
                RepositoryId = "11111111-2222-3333-4444-555555555555", Result = "failed", FinishTime = Clock.AddMinutes(-12),
                TeamProject = Project, CollectionUri = Collection, WebUrl = Untrusted },
            Runs = [new AdoTestRun { Id = 201, Name = "Synthetic Windows tests", BuildId = 401, State = "Completed", StartedDate = Clock.AddMinutes(-15),
                TeamProject = Project, CollectionUri = Collection }],
            Summary = summary, History = [Summary(398, false, false, 0, 0), Summary(399, false, true, 0, 0), Summary(400, false, true, 1, 0), summary],
            Failures = failures, FailedCount = summary.Failed + (partial ? 7 : 0), FlakyCount = summary.Flaky,
            Status = partial ? AdoTestFailureStatus.Partial : AdoTestFailureStatus.Complete, Diagnostics = diagnostics, RetrievedAt = Clock.AddMinutes(-1), CollectionUri = Collection,
        };
    }

    // English and French stages. Run 200 is outside the attachment window; English then passes,
    // and French fails twice with the same message and trace.
    private static AdoBuildTestFailureSet GroupedSet()
    {
        AdoTestRun Run(int id, string stage, int attempt, DateTimeOffset started) => new()
        {
            Id = id, Name = "Synthetic " + stage, BuildId = 401, State = "Completed", StartedDate = started, StageName = stage, PhaseName = "UiTests",
            JobName = "__default", PipelineAttempt = attempt, TotalTests = 40, PassedTests = 38, TeamProject = Project, CollectionUri = Collection,
        };
        AdoTestAttachment File(int id, int run, int result, string name) => new()
        { Id = id, RunId = run, ResultId = result, FileName = name, Size = 128, AttachmentType = "GeneralAttachment", Kind = AttachmentKinds.FromFileName(name) };
        AdoTestAttempt Attempt(int number, int run, string? error, params AdoTestAttachment[] attachments) => new()
        {
            Number = number, Source = number == 1 ? AdoTestAttemptSource.Single : AdoTestAttemptSource.RunAttempt, RunId = run, ResultId = 10 + number,
            Outcome = error is null ? "Passed" : "Failed", OutcomeClass = error is null ? AdoTestOutcomeClass.Pass : AdoTestOutcomeClass.Failure,
            StartedDate = Clock.AddMinutes(-15), CompletedDate = Clock.AddMinutes(-14), Duration = TimeSpan.FromSeconds(12.5), ComputerName = "SYNTHETIC-AGENT",
            ErrorMessage = error, StackTrace = error is null ? null : @"   at Synthetic.CheckoutTests.SubmitOrder() in C:\synthetic\Checkout.cs:line " + error.Length.ToString(CultureInfo.InvariantCulture),
            Attachments = attachments,
        };
        const string French = "Assert.Equal() Failure: Expected: « Confirmée » Actual: « Confirmed »";
        AdoTestFailure[] failures =
        [
            new() { Ordinal = 1, Classification = AdoTestFailureClassification.Failed, TestName = "Synthetic.CheckoutTests.SubmitOrder", ShortName = "SubmitOrder",
                Storage = "Synthetic.Tests.dll", CollectionUri = Collection,
                TestCase = new AdoTestCaseLink { Id = 901, Title = "Valider la commande", State = "Ready", IsResolved = true, WebUrl = Untrusted },
                // Resolved is not a Completed category, so the bug is still open.
                Bugs = [Bug(804, "Libellé « Confirmée » absent", "Resolved", "Resolved", true, associated: false, linked: true)],
                Attempts = [Attempt(1, 200, "Timed out waiting for #submit", File(61, 200, 11, "old-run.txt")), Attempt(2, 201, null),
                    Attempt(3, 202, French, File(62, 202, 13, "Details.json"), File(63, 202, 13, "console.log")), Attempt(4, 203, French)] },
            new() { Ordinal = 2, Classification = AdoTestFailureClassification.Failed, TestName = "Synthetic.HomeTests.ShowBanner", ShortName = "ShowBanner",
                Storage = "Synthetic.Tests.dll", CollectionUri = Collection,
                TestCase = new AdoTestCaseLink { Id = 903, IsResolved = false, WebUrl = Untrusted },
                Attempts = [Attempt(1, 201, "Banner not shown"), Attempt(2, 202, null)] },
        ];
        AdoBuildTestSummary summary = Summary(401, true, true, 2, 0);
        return new AdoBuildTestFailureSet
        {
            Build = new AdoBuild { Id = 401, BuildNumber = "20260916.4", Definition = new AdoBuildDefinitionRef { Id = 12, Name = "Synthetic UI tests" },
                SourceBranch = "refs/heads/main", Result = "failed", FinishTime = Clock.AddMinutes(-5), TeamProject = Project, CollectionUri = Collection, WebUrl = Untrusted },
            Runs = [Run(200, "Tests_EN", 1, Clock.AddDays(-10)), Run(201, "Tests_EN", 2, Clock.AddMinutes(-20)), Run(202, "Tests_FR", 1, Clock.AddMinutes(-19)),
                Run(203, "Tests_FR", 2, Clock.AddMinutes(-10))],
            Summary = summary, History = [summary], Failures = failures, FailedCount = 2, Status = AdoTestFailureStatus.Complete,
            RetrievedAt = Clock.AddMinutes(-1), CollectionUri = Collection,
        };
    }

    private static AdoTestFailure Failure(bool hostile, bool flaky, bool unresolved, int ordinal) => new()
    {
        Ordinal = ordinal, Classification = flaky ? AdoTestFailureClassification.Flaky : AdoTestFailureClassification.Failed,
        TestName = hostile ? Hostile : flaky ? "Synthetic.CheckoutTests.RetryPayment" : "Synthetic.CheckoutTests.SubmitOrder",
        ShortName = hostile ? Hostile : flaky ? "RetryPayment" : "SubmitOrder", Storage = "Synthetic.Tests.dll", Title = "Valider la commande",
        Owner = new AdoIdentityRef { DisplayName = "Fictional Tester", UniqueName = "tester@example.test" }, Priority = 2,
        TestCase = new AdoTestCaseLink { Id = unresolved || hostile ? 902 : 901, Title = hostile ? Hostile : "Valider la commande", State = "Ready", Rev = 4,
            IsResolved = !unresolved && !hostile, WebUrl = Untrusted },
        // An open bug found on both sources and a closed one; the flaky test has only the closed bug,
        // and in the partial report the closed bug could not be read.
        Bugs = flaky ? [Closed()]
            : [Bug(801, hostile ? Hostile : "Le total ignore la remise", hostile ? Hostile : "Active", "InProgress", true, linked: true),
                unresolved ? new AdoTestBug { Id = 802, IsAssociatedWithResult = true, WebUrl = Untrusted } : Closed()],
        Attempts = flaky ? [Attempt(1, false, hostile), Attempt(2, true, hostile)] : [Attempt(1, false, hostile)],
        History = [History(398, AdoTestHistoryOutcome.Unavailable), History(399, AdoTestHistoryOutcome.NotRun), History(400, AdoTestHistoryOutcome.Failed),
            History(401, flaky ? AdoTestHistoryOutcome.Flaky : AdoTestHistoryOutcome.Failed)], CollectionUri = Collection,
    };

    private static AdoTestAttempt Attempt(int number, bool passed, bool hostile) => new()
    {
        Number = number, Source = number == 1 ? AdoTestAttemptSource.Single : AdoTestAttemptSource.RunAttempt, RunId = 201, ResultId = 10 + number,
        Outcome = passed ? "Passed" : "Failed", OutcomeClass = passed ? AdoTestOutcomeClass.Pass : AdoTestOutcomeClass.Failure,
        StartedDate = Clock.AddMinutes(-15), CompletedDate = Clock.AddMinutes(-14), Duration = TimeSpan.FromMilliseconds(1234.5),
        ComputerName = hostile ? Hostile : "SYNTHETIC-AGENT", RunBy = new AdoIdentityRef { DisplayName = "Fictional Runner", UniqueName = "runner@example.test" },
        FailureType = passed ? null : "Regression", ResolutionState = passed ? null : "Unresolved", FailingSinceBuildId = passed ? null : 400,
        Comment = hostile ? Hostile : "« Résultat attendu : commande confirmée »", AssociatedBugIds = [801, 802],
        ErrorMessage = passed ? null : hostile ? Hostile : "Assert.Equal() Failure: Expected: \"Confirmed\" Actual: \"Pending\"\nhttps://repo.example.test/source?q=1&line=42",
        StackTrace = passed ? null : hostile ? Hostile : "System.InvalidOperationException: Synthetic failure\n   at System.Threading.Tasks.Task.Execute()\n   at Synthetic.CheckoutTests.SubmitOrder() in C:\\synthetic\\Checkout.cs:line 42\n   à Synthetic.Assertions.Vérifier() dans C:\\synthetic\\Assertions.cs:ligne 18",
        SubResults = passed ? [] : [new AdoTestSubResult { Id = 301, DisplayName = "Data row A", SequenceId = 1, ResultGroupType = "DataDriven", Outcome = "Failed",
            OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = "Sub-result message <expected>", StackTrace = "   at Synthetic.Data.Row()",
            Comment = "Sub-result comment", ComputerName = "SYNTHETIC-DATA", StartedDate = Clock.AddMinutes(-15), CompletedDate = Clock.AddMinutes(-14), Duration = TimeSpan.FromSeconds(1),
            SubResults = [new AdoTestSubResult { Id = 302, DisplayName = "Nested row", Outcome = "Blocked", OutcomeClass = AdoTestOutcomeClass.Other, ErrorMessage = "Nested message", StackTrace = "Nested trace" }] }],
        Iterations = passed ? [] : [new AdoTestIteration { Id = 7, Outcome = "Failed", ErrorMessage = "Iteration message", Parameters = [new AdoTestIterationParameter { Name = "quantity", Value = "3 < 4" }],
            ActionResults = [new AdoTestActionResult { ActionPath = "00000001", StepIdentifier = "step-A", Outcome = "Failed", ErrorMessage = "Action message" }] }],
        Attachments = passed ? [] : [new AdoTestAttachment { Id = 51, RunId = 201, ResultId = 11, FileName = hostile ? Hostile : "Screenshot.PNG", Size = 1234, Comment = "Synthetic screenshot metadata", AttachmentType = "GeneralAttachment", Kind = AdoTestAttachmentKind.Png },
            new AdoTestAttachment { Id = 52, RunId = 201, ResultId = 11, SubResultId = 301, FileName = "Details.json", Size = 256, Kind = AdoTestAttachmentKind.Json }],
        CustomFields = new Dictionary<string, object?>(StringComparer.Ordinal) { ["SyntheticFlag"] = true, ["SyntheticNumber"] = 1234.5m },
        AdditionalFields = new Dictionary<string, object?>(StringComparer.Ordinal) { ["futureField"] = hostile ? Hostile : "Forward-compatible value" }, WebUrl = Untrusted,
    };

    private static AdoTestBug Bug(int id, string title, string state, string category, bool open, bool associated = true, bool linked = false) => new()
    {
        Id = id, Title = title, State = state, WorkItemType = "Bug", TeamProject = Project, StateCategory = category, IsOpen = open, IsResolved = true,
        IsAssociatedWithResult = associated, IsLinkedToTestCase = linked, WebUrl = Untrusted,
    };

    private static AdoTestBug Closed() => Bug(802, "Ancien délai d’expiration", "Closed", "Completed", false);

    private static AdoTestHistoryEntry History(int build, AdoTestHistoryOutcome outcome) => new()
    { BuildId = build, BuildNumber = "20260916." + (build - 398).ToString(CultureInfo.InvariantCulture), Outcome = outcome, IsCurrent = build == 401, WebUrl = Untrusted };

    private static AdoBuildTestSummary Summary(int build, bool current, bool available, int failed, int flaky) => new()
    {
        BuildId = build, BuildNumber = "20260916." + (build - 398).ToString(CultureInfo.InvariantCulture), IsCurrent = current, IsAvailable = available,
        Passed = available ? 42 : 0, Failed = failed, Flaky = flaky, Other = available ? 2 : 0,
        SourceBranch = "refs/heads/main", FinishTime = Clock.AddDays(build - 401), Result = "failed", WebUrl = Untrusted,
    };
}
