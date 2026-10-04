using AdoToolkit.Core.Builds;
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
        if (variant == "large") return LargeSet();
        bool hostile = variant == "hostile";
        bool partial = variant == "partial";
        bool flaky = variant == "flaky";
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
                RepositoryId = "11111111-2222-3333-4444-555555555555", Result = "failed", QueueTime = Clock.AddMinutes(-20), FinishTime = Clock.AddMinutes(-12),
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
                // Filed three minutes after the build went into the queue, so the chip carries the marker.
                Bugs = [Bug(804, "Libellé « Confirmée » absent", "Resolved", "Resolved", true, associated: false, linked: true,
                    created: Clock.AddMinutes(-22), assignee: "Nadia Roy")],
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
                SourceBranch = "refs/heads/main", Result = "failed", QueueTime = Clock.AddMinutes(-25), FinishTime = Clock.AddMinutes(-5),
                TeamProject = Project, CollectionUri = Collection, WebUrl = Untrusted },
            Runs = [Run(200, "Tests_EN", 1, Clock.AddDays(-10)), Run(201, "Tests_EN", 2, Clock.AddMinutes(-20)), Run(202, "Tests_FR", 1, Clock.AddMinutes(-19)),
                Run(203, "Tests_FR", 2, Clock.AddMinutes(-10))],
            Summary = summary, History = [summary], Failures = failures, FailedCount = 2, Status = AdoTestFailureStatus.Complete,
            RetrievedAt = Clock.AddMinutes(-1), CollectionUri = Collection,
        };
    }

    // Sixteen tests in English and French stages, two runs each, with ten builds of history. Errors
    // that differ in numbers, paths and GUIDs, exception types, an MSTest first line and a shared
    // helper frame make clusters; histories give new, recurring and unknown trends; bug 5101 covers
    // one failed result of SubmitOrder and not AddItem, which shares its Test Case; bug 5103 could
    // not be read; bug 5104 covers some results of one test and all of another.
    private static AdoBuildTestFailureSet LargeSet()
    {
        DateTimeOffset clock = Clock;
        (string Stage, int FirstRun)[] groups = [("Tests_EN", 300), ("Tests_FR", 302)];
        List<AdoTestRun> runs = [];
        foreach ((string stage, int first) in groups)
            foreach (int attempt in new[] { 1, 2 })
            {
                int id = first + attempt - 1;
                Dictionary<string, int> counts = new(StringComparer.Ordinal) { ["Passed"] = attempt == 1 ? 402 : 9, ["Failed"] = attempt == 1 ? 14 : 5 };
                if (attempt == 1) counts["NotExecuted"] = 2;
                runs.Add(new AdoTestRun
                {
                    Id = id, Name = "Synthetic " + stage, BuildId = 412, State = "Completed", IsAutomated = true,
                    StartedDate = clock.AddMinutes(-50 + 8 * (id - 300)), CompletedDate = clock.AddMinutes(-44 + 8 * (id - 300)),
                    StageName = stage, PhaseName = "UiTests", JobName = "__default", PipelineAttempt = attempt, TotalTests = attempt == 1 ? 418 : 14,
                    PassedTests = counts["Passed"], OutcomeCounts = counts, TeamProject = Project, CollectionUri = Collection,
                });
            }
        const string Work = @"C:\agent\_work\12\s";
        string Plain(string test, int line) =>
            "   at Synthetic.Web." + test + "() in " + Work + @"\tests\" + test.Split('.')[^2] + ".cs:line " + line.ToString(CultureInfo.InvariantCulture);
        string Trace(string kind, string test, int index) => kind switch
        {
            "timeout" => "   at OpenQA.Selenium.Support.UI.DefaultWait`1.Until[TResult](Func`2 condition)\n   at Synthetic.Web.Pages.CheckoutPage.Submit() in " + Work
                + @"\src\Pages\CheckoutPage.cs:line 88" + "\n" + Plain(test, 41)
                + "\n   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)",
            "import" => "   at System.IO.FileStream.ValidateFileHandle(SafeFileHandle fileHandle, String path)\n   at Synthetic.Web.Data.Importer.Load(String path) in " + Work
                + @"\src\Data\Importer.cs:line 23" + "\n" + Plain(test, 30),
            "api" => "   at Synthetic.Web.Api.ApiClient.SendAsync(HttpRequestMessage request) in " + Work + @"\src\Api\ApiClient.cs:line 57" + "\n" + Plain(test, 19),
            "payments" => "   at Synthetic.Web.Api.PaymentsClient.Refund(Guid payment) in " + Work + @"\src\Api\PaymentsClient.cs:line 112" + "\n" + Plain(test, 64),
            _ => Plain(test, 20 + index),
        };
        // Name, error, trace kind, history (P passed, F failed, K flaky, N not run, U unavailable),
        // attempts per group, Test Case and bugs.
        (string Name, string? Error, string Trace, string History, string[] Attempts, int TestCase, string? Bugs)[] specs =
        [
            ("Checkout.CheckoutTests.SubmitOrder", "System.TimeoutException: Timed out after 3000 ms waiting for #submit", "timeout", "PUFFFFFFFF", ["FF", "FF"], 9101, "b5101"),
            ("Checkout.CheckoutTests.ApplyCoupon", "System.TimeoutException: Timed out after 4500 ms waiting for #submit", "timeout", "PUPPPFFFFF", ["FF", "P"], 9102, null),
            ("Checkout.CheckoutTests.PayWithCard", "System.TimeoutException: Timed out after 30000 ms waiting for #submit", "timeout", "PUPPPPPPPK", ["P", "FP"], 9103, null),
            ("Checkout.CartTests.AddItem", "System.TimeoutException: Timed out after 3000 ms waiting for #submit", "timeout", "PUPPPPPPFF", ["FF", "FF"], 9101, null),
            ("Data.ImportTests.ImportOrders", "System.IO.FileNotFoundException: Could not find file '" + Work + @"\data\orders-1.json'.", "import", "PUPPPPPFFF", ["F", "F"], 9105, "b5102"),
            ("Data.ImportTests.ImportCustomers", @"System.IO.FileNotFoundException: Could not find file 'C:\agent\_work\7\s\data\customers.json'.", "import", "PUPPPPPFFF", ["FF", "P"], 9106, null),
            ("Data.ExportTests.ExportInvoices", "System.IO.FileNotFoundException: Could not find file '/home/agent/work/3/s/out/invoices.json'.", "import", "PUPPPPPPPF", ["P", "FF"], 0, null),
            ("Api.OrdersApiTests.GetOrder", "Test method Synthetic.Web.Api.OrdersApiTests.GetOrder threw exception: \nSynthetic.Web.Api.ApiException: Order 3f2504e0-4f89-11d3-9a0c-0305e82c3301 was not found (HTTP 404).",
                "api", "PUPPFFFFFF", ["FF", "FF"], 9108, "b5103"),
            ("Api.OrdersApiTests.CancelOrder", "Test method Synthetic.Web.Api.OrdersApiTests.CancelOrder threw exception: \nSynthetic.Web.Api.ApiException: Order 9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d was not found (HTTP 404).",
                "api", "PUPPPPFFFF", ["FF", "P"], 9109, null),
            ("Api.PaymentsApiTests.Refund", "System.NullReferenceException: Object reference not set to an instance of an object.", "payments", "PUPPPPPPPF", ["FF", "FF"], 9110, "b5106"),
            ("Api.PaymentsApiTests.Capture", "System.NullReferenceException: Object reference not set to an instance of an object.", "payments", "PUPPPPPPKK", ["FP", "P"], 9111, null),
            ("UI.HomeTests.ShowBanner", "Assert.Equal() Failure: Expected: \"Bienvenue\" Actual: \"Welcome\"", "plain", "PUPPPPPPNF", ["F", "F"], 9112, null),
            ("UI.HomeTests.ShowFooter", "Assert.True() Failure\nExpected: True\nActual:   False", "plain", "", ["P", "FF"], 9113, "b5105"),
            ("UI.SearchTests.SearchByVeryLongProductNameThatDoesNotFitInTheColumnAtAll", "Expected 12 results but found 9.", "plain", "PUPFFFFFFF", ["FF", "FF"], 9114, "b5104all"),
            ("UI.SearchTests.SearchByCategory", "Expected 4 results but found 0.", "plain", "PUPPPPPFFF", ["FF", "P"], 9115, "b5104part"),
            ("UI.ProfileTests.UpdateAvatar", null, "plain", "PUPPPPPPPF", ["F", "P"], 9116, null),
        ];
        AdoTestAttachment Attachment(int id, int run, int result, string name, long size, AdoTestAttachmentStatus status = AdoTestAttachmentStatus.NotRequested) => new()
        { Id = id, RunId = run, ResultId = result, FileName = name, Size = size, AttachmentType = "GeneralAttachment", Kind = AttachmentKinds.FromFileName(name), DownloadStatus = status };
        // The build was queued 60 minutes before the clock, so created tells each bug apart: after the
        // queue time, at it, a minute before it, long before it, and one bug with no date at all.
        AdoTestBug Read(int id, string title, string state, bool associated, bool linked, DateTimeOffset? created = null, string? assignee = null) => new()
        {
            Id = id, Title = title, State = state, WorkItemType = "Bug", TeamProject = Project, IsOpen = true, IsResolved = true,
            StateCategory = state switch { "New" => "Proposed", "Resolved" => "Resolved", _ => "InProgress" },
            CreatedDate = created, AssignedTo = assignee is null ? null : new AdoIdentityRef { DisplayName = assignee, UniqueName = "owner@example.test" },
            IsAssociatedWithResult = associated, IsLinkedToTestCase = linked, WebUrl = Untrusted,
        };
        List<AdoTestFailure> failures = [];
        int resultId = 1000;
        for (int index = 0; index < specs.Length; index++)
        {
            var spec = specs[index];
            string shortName = spec.Name.Split('.')[^1];
            List<AdoTestAttempt> attempts = [];
            int number = 0;
            bool failedAtEnd = false;
            for (int g = 0; g < groups.Length; g++)
            {
                string pattern = spec.Attempts[g];
                for (int a = 0; a < pattern.Length; a++)
                {
                    number++;
                    resultId++;
                    bool failed = pattern[a] == 'F';
                    int[] bugIds = !failed ? [] : spec.Bugs switch
                    {
                        "b5101" or "b5104part" when number == 1 => [spec.Bugs == "b5101" ? 5101 : 5104],
                        "b5103" => [5103], "b5106" => [5106], "b5104all" => [5104],
                        _ => [],
                    };
                    int run = 300 + 2 * g + a, id = 7000 + 10 * resultId;
                    AdoTestAttachment[] attachments = !failed ? []
                        : shortName == "SubmitOrder" && a == 0 ? [Attachment(id + 1, run, resultId, "Screenshot.png", 245760), g == 0
                            ? Attachment(id + 2, run, resultId, "console.log", 12288) : Attachment(id + 2, run, resultId, "network.json", 2048)]
                        : shortName == "GetOrder" && number == 1 ? [Attachment(id + 1, run, resultId, "response.json", 512),
                            Attachment(id + 2, run, resultId, "trace.zip", 2516582, AdoTestAttachmentStatus.TooLarge)]
                        : shortName is "Refund" or "AddItem" && number == 1 ? [Attachment(id + 1, run, resultId, "Screenshot.png", 198000)]
                        : [];
                    attempts.Add(new AdoTestAttempt
                    {
                        Number = number, Source = number == 1 ? AdoTestAttemptSource.Single : AdoTestAttemptSource.RunAttempt, RunId = run, ResultId = resultId,
                        Outcome = !failed ? "Passed" : shortName == "ShowBanner" && g == 1 ? "Timeout" : "Failed",
                        OutcomeClass = failed ? AdoTestOutcomeClass.Failure : AdoTestOutcomeClass.Pass,
                        ErrorMessage = failed ? spec.Error : null, StackTrace = failed && spec.Error is not null ? Trace(spec.Trace, spec.Name, index) : null,
                        StartedDate = clock.AddMinutes(-45 + 8 * g), CompletedDate = clock.AddMinutes(-45 + 8 * g).AddSeconds(14),
                        Duration = TimeSpan.FromMilliseconds(1200 + 977 * index + 311 * a), ComputerName = "SYNTHETIC-AGENT-0" + (3 + g).ToString(CultureInfo.InvariantCulture),
                        RunBy = new AdoIdentityRef { DisplayName = "Fictional Runner", UniqueName = "runner@example.test" },
                        FailureType = failed ? "Regression" : null, ResolutionState = failed ? "Unresolved" : null, FailingSinceBuildId = failed ? 405 : null,
                        AssociatedBugIds = bugIds, Attachments = attachments,
                        Comment = failed && shortName == "SubmitOrder"
                            ? "Synthetic comment: the submit button stays disabled until the payment iframe answers, which takes longer on the shared agents." : null,
                    });
                }
                if (pattern[^1] == 'F') failedAtEnd = true;
            }
            AdoTestBug[] bugs = spec.Bugs switch
            {
                "b5101" => [Read(5101, "Checkout times out on submit when the payment frame is slow", "Active", true, false,
                    clock.AddMinutes(-55), "Nadia Roy")],
                "b5102" => [Read(5102, "Import fails when the agent data folder moves", "Active", false, true, clock.AddDays(-9), "Luc Beaulieu")],
                // Bug 5103 could not be read.
                "b5103" => [new AdoTestBug { Id = 5103, IsAssociatedWithResult = true, WebUrl = Untrusted }],
                // Filed at the queue time itself: the marker takes the boundary.
                "b5104all" or "b5104part" => [Read(5104, "Search result count is off by the hidden items", "Active", true, false, clock.AddMinutes(-60))],
                // One minute before it: not marked.
                "b5105" => [Read(5105, "Footer missing on narrow screens", "Resolved", false, true, clock.AddMinutes(-61))],
                // Read and open with no creation date: no claim about its age, but its owner still shows.
                "b5106" => [Read(5106, "Refund throws when the payment has no provider", "New", true, false, assignee: "Priya Gagné")],
                _ => [],
            };
            failures.Add(new AdoTestFailure
            {
                Ordinal = index + 1, Classification = failedAtEnd ? AdoTestFailureClassification.Failed : AdoTestFailureClassification.Flaky,
                TestName = "Synthetic.Web." + spec.Name, ShortName = shortName, Storage = "Synthetic.Web.Tests.dll",
                Title = spec.TestCase > 0 ? "Synthetic case " + spec.TestCase.ToString(CultureInfo.InvariantCulture) : null,
                TestCase = spec.TestCase > 0 ? new AdoTestCaseLink { Id = spec.TestCase, Title = "Synthetic case " + spec.TestCase.ToString(CultureInfo.InvariantCulture),
                    State = "Ready", IsResolved = true, WebUrl = Untrusted } : null,
                Owner = new AdoIdentityRef { DisplayName = "Fictional Tester", UniqueName = "tester@example.test" }, Priority = 2,
                Attempts = attempts, Bugs = bugs, CollectionUri = Collection,
                History = [.. spec.History.Select((code, i) => new AdoTestHistoryEntry
                {
                    BuildId = 403 + i, BuildNumber = "20260916." + (3 + i).ToString(CultureInfo.InvariantCulture), IsCurrent = i == spec.History.Length - 1, WebUrl = Untrusted,
                    Outcome = code switch
                    {
                        'P' => AdoTestHistoryOutcome.Passed, 'F' => AdoTestHistoryOutcome.Failed, 'K' => AdoTestHistoryOutcome.Flaky,
                        'N' => AdoTestHistoryOutcome.NotRun, _ => AdoTestHistoryOutcome.Unavailable,
                    },
                })],
            });
        }
        AdoBuildTestSummary[] history = [.. Enumerable.Range(403, 10).Select(build => new AdoBuildTestSummary
        {
            BuildId = build, BuildNumber = "20260916." + (build - 400).ToString(CultureInfo.InvariantCulture), IsCurrent = build == 412, IsAvailable = build != 404,
            Passed = build == 404 ? 0 : 410 - (build - 403), Failed = build == 404 ? 0 : 2 + (build - 403), Flaky = build >= 410 ? 2 : 0, Other = build == 404 ? 0 : 2,
            SourceBranch = "refs/heads/main", FinishTime = clock.AddDays(build - 412), Result = "failed", WebUrl = Untrusted,
        })];
        return new AdoBuildTestFailureSet
        {
            Build = new AdoBuild { Id = 412, BuildNumber = "20260916.12", Definition = new AdoBuildDefinitionRef { Id = 31, Name = "Synthetic web tests" },
                SourceBranch = "refs/heads/main", SourceVersion = "89abcdef0123456789abcdef0123456789abcdef", RepositoryType = "TfsGit",
                RepositoryId = "22222222-3333-4444-5555-666666666666", Result = "failed", QueueTime = clock.AddMinutes(-60), FinishTime = clock.AddMinutes(-5),
                TeamProject = Project, CollectionUri = Collection, WebUrl = Untrusted },
            Runs = runs, Summary = history[^1], History = history, Failures = failures,
            FailedCount = failures.Count(static failure => failure.Classification == AdoTestFailureClassification.Failed),
            FlakyCount = failures.Count(static failure => failure.Classification == AdoTestFailureClassification.Flaky),
            Status = AdoTestFailureStatus.Complete, RetrievedAt = clock.AddMinutes(-1), CollectionUri = Collection,
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
        // Only what retrieval can produce: an open bug found on both sources, and in the partial report
        // bug 802, which could not be read. Elsewhere 802 is closed, so it is absent here although every
        // attempt still names it as associated; the flaky test has that closed bug only.
        Bugs = flaky ? []
            // Filed two minutes after the build went into the queue, and assigned; a name is remote text too.
            : [Bug(801, hostile ? Hostile : "Le total ignore la remise", hostile ? Hostile : "Active", "InProgress", true, linked: true,
                    created: Clock.AddMinutes(-18), assignee: hostile ? Hostile : "Nadia Roy"),
                .. unresolved ? [new AdoTestBug { Id = 802, IsAssociatedWithResult = true, WebUrl = Untrusted }] : Array.Empty<AdoTestBug>()],
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

    private static AdoTestBug Bug(int id, string title, string state, string category, bool open, bool associated = true, bool linked = false,
        DateTimeOffset? created = null, string? assignee = null) => new()
    {
        Id = id, Title = title, State = state, WorkItemType = "Bug", TeamProject = Project, StateCategory = category, IsOpen = open, IsResolved = true,
        CreatedDate = created, AssignedTo = assignee is null ? null : new AdoIdentityRef { DisplayName = assignee, UniqueName = "nroy@example.test" },
        IsAssociatedWithResult = associated, IsLinkedToTestCase = linked, WebUrl = Untrusted,
    };

    private static AdoTestHistoryEntry History(int build, AdoTestHistoryOutcome outcome) => new()
    { BuildId = build, BuildNumber = "20260916." + (build - 398).ToString(CultureInfo.InvariantCulture), Outcome = outcome, IsCurrent = build == 401, WebUrl = Untrusted };

    private static AdoBuildTestSummary Summary(int build, bool current, bool available, int failed, int flaky) => new()
    {
        BuildId = build, BuildNumber = "20260916." + (build - 398).ToString(CultureInfo.InvariantCulture), IsCurrent = current, IsAvailable = available,
        Passed = available ? 42 : 0, Failed = failed, Flaky = flaky, Other = available ? 2 : 0,
        SourceBranch = "refs/heads/main", FinishTime = Clock.AddDays(build - 401), Result = "failed", WebUrl = Untrusted,
    };
}
