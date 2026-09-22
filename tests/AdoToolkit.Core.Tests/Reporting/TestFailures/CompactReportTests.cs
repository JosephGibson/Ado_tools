using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The compact report: flaky exclusion, pipeline groups, collapsed details, search coverage and size.
public sealed class CompactReportTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] AttachmentNames = ["screenshot.png", "page.html", "context.json", "console.txt"];

    [Fact]
    public void FlakyTestsAreLeftOutByDefaultAndIncludedOnRequest()
    {
        AdoBuildTestFailureSet set = TestFailureReportFixture.Set("flaky");
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(set, Options(includeFlaky: false));
        Assert.Equal(["SubmitOrder"], model.Failures.Select(f => f.ShortName));
        Assert.True(model.FlakyExcluded);
        Assert.Equal(1, model.FlakyCount);
        string html = TestFailureReportFixture.Render(model);
        Assert.DoesNotContain("RetryPayment", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"count-note\">not shown</span>", html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(html), model);

        TestFailureReportModel included = TestFailureReportModelBuilder.Build(set, Options(includeFlaky: true));
        Assert.Equal(["SubmitOrder", "RetryPayment"], included.Failures.Select(f => f.ShortName));
        Assert.False(included.FlakyExcluded);
        html = TestFailureReportFixture.Render(included);
        Assert.Contains("data-classification=\"flaky\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"count-note\"", html, StringComparison.Ordinal);
    }

    // Stage, job (a REST phase) and job instance each separate English from French attempts.
    [Theory]
    [InlineData("stage", "Tests_EN", "Tests_FR")]
    [InlineData("phase", "UiTests_EN", "UiTests_FR")]
    [InlineData("job", "en", "fr")]
    public void AttemptsFromDifferentPipelineNamesRenderAsSeparateGroupsWithTheirStatusInTheOverview(string level, string english, string french)
    {
        TestFailureReportModel model = Model(Named(run => run.StageName == "Tests_EN" ? english : french, level));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains("<th scope=\"col\">" + english + "</th><th scope=\"col\">" + french + "</th>", html, StringComparison.Ordinal);
        // SubmitOrder failed, then passed in English, and failed twice in French.
        string row = Section(html, "<tr data-index-for=\"f-1\">", "</tr>");
        Assert.Matches("status-flaky\">.*1/2</span>.*status-failed\">.*2/2</span>", row);
        string card = Section(html, "<article class=\"card failure-card\" id=\"f-1\"", "</article>");
        Assert.Contains("data-failing=\"1\"", card, StringComparison.Ordinal);
        string first = Section(card, "<details class=\"attempt-group\" id=\"f-1-g1\">", "<details class=\"attempt-group\" id=\"f-1-g2\">");
        string second = card[card.IndexOf("<details class=\"attempt-group\" id=\"f-1-g2\">", StringComparison.Ordinal)..];
        Assert.Contains("<span class=\"group-label\">" + english + "</span>", first, StringComparison.Ordinal);
        Assert.Contains("<span class=\"group-label\">" + french + "</span>", second, StringComparison.Ordinal);
        Assert.Equal(["f-1-a1", "f-1-a2"], AttemptIds(first));
        Assert.Equal(["f-1-a3", "f-1-a4"], AttemptIds(second));
        // ShowBanner never ran a third group, and its French group did not fail.
        Assert.Matches("status-failed\">.*1/1</span>.*status-passed\">.*0/1</span>", Section(html, "<tr data-index-for=\"f-2\">", "</tr>"));
    }

    // No distinct pipeline OR run names keeps one list.
    [Theory]
    [InlineData(null)]
    [InlineData("Tests")]
    public void RunsWithoutDistinctPipelineNamesRenderOneUngroupedList(string? name)
    {
        AdoBuildTestFailureSet set = Named(_ => name, "stage");
        TestFailureReportModel model = Model(With(set,
            [.. set.Runs.Select(run => Copy(run, run.StageName, run.PhaseName, run.JobName, "Same run"))], set.Failures));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.DoesNotContain("class=\"attempt-group\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" data-failing", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Attempts</th>", html, StringComparison.Ordinal);
        Assert.Equal(["f-1-a1", "f-1-a2", "f-1-a3", "f-1-a4"], AttemptIds(Section(html, "<article class=\"card failure-card\" id=\"f-1\"", "</article>")));
        Assert.Contains("status-failed\">", Section(html, "<tr data-index-for=\"f-1\">", "</tr>"), StringComparison.Ordinal);
    }

    [Fact]
    public void NoAttemptGroupOrPreviewStartsOpen()
    {
        foreach (string variant in new[] { "failed", "flaky", "partial", "hostile", "grouped" })
        {
            string html = TestFailureReportFixture.Render(variant, "fr-CA");
            Assert.Contains("<details class=\"attempt\"", html, StringComparison.Ordinal);
            Assert.DoesNotMatch("<details[^>]*\\sopen[\\s>=]", html);
        }
    }

    // The script searches each card's whole text, collapsed parts included, so everything a reader
    // may search for must be inside the card: a Test Case ID with or without '#', and text found
    // only in a stack trace, an error message, an attachment, a pipeline name or a run field.
    [Fact]
    public void EachCardHoldsItsSearchableRunDetails()
    {
        using TestDirectory directory = new();
        AdoBuildTestFailureSet set = Named(run => run.Id == 203 ? "stage-only-marker" : run.StageName, "stage");
        AdoTestRun[] runs = [.. set.Runs.Select(run => run.Id != 203 ? run : Copy(run, run.StageName, run.PhaseName, run.JobName, "run-only-marker"))];
        AdoTestFailure submit = set.Failures[0];
        AdoTestAttempt third = submit.Attempts[2];
        AdoTestAttempt marked = new()
        {
            Number = third.Number, Source = third.Source, RunId = third.RunId, ResultId = third.ResultId, Outcome = third.Outcome, OutcomeClass = third.OutcomeClass,
            ErrorMessage = "Assert failed: message-only-marker", StackTrace = "   at Synthetic.Stack.stack-only-marker()", ComputerName = "machine-only-marker",
            Attachments = [.. third.Attachments.Select(a => a.Id == 63 ? a.WithDownload(AdoTestAttachmentStatus.Downloaded, "files/r202-13-a63.txt") : a)],
        };
        string folder = Directory.CreateDirectory(Path.Combine(directory.Root, "files")).FullName;
        File.WriteAllText(Path.Combine(folder, "r202-13-a63.txt"), "agent output: attachment-only-marker");
        AdoBuildTestFailureSet searchable = With(set, runs, [submit.WithAttempts([submit.Attempts[0], submit.Attempts[1], marked, submit.Attempts[3]]), set.Failures[1]]);
        TestFailureReportModel model = Model(searchable);
        model = TestFailureReportModelBuilder.WithAttachments(model, model.Failures, [], new TestFailureLocalAttachments
        {
            FolderName = "files", SourceFolder = folder, MaximumInlineJsonBytes = 262144,
            Files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["r202-13-a63.txt"] = new FileInfo(Path.Combine(folder, "r202-13-a63.txt")).Length },
        });
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model, folder);
        string first = Section(html, "<article class=\"card failure-card\" id=\"f-1\"", "</article>");
        string second = Section(html, "<article class=\"card failure-card\" id=\"f-2\"", "</article>");
        string firstText = TestFailureMarkup.Text(first), secondText = TestFailureMarkup.Text(second);
        foreach (string marker in new[] { "stack-only-marker", "message-only-marker", "attachment-only-marker", "stage-only-marker", "run-only-marker", "machine-only-marker", "#901" })
        {
            Assert.Contains(marker, firstText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(marker, secondText, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("data-test-case=\"901\"", first, StringComparison.Ordinal);
        Assert.DoesNotContain("901", secondText, StringComparison.Ordinal);
        Assert.Contains("data-test-case=\"903\"", second, StringComparison.Ordinal);
    }

    // 100 failures with 7 English and 7 French attempts, a 40-frame stack trace and four attachments
    // per attempt. The 0.2.0 renderer wrote about 46 MB for this input.
    [Fact]
    public void AHundredFailuresWithFourteenAttemptsStayWithinTheSizeBudget()
    {
        AdoBuildTestFailureSet grouped = TestFailureReportFixture.Set("grouped");
        DateTimeOffset clock = TestFailureReportFixture.Clock;
        string frames = string.Join('\n', Enumerable.Range(1, 40).Select(i => "   at Synthetic.Ui.Framework.Layer" + i.ToString(CultureInfo.InvariantCulture)
            + ".Helper.InvokeStep(String name, Int32 timeout) in C:\\agent\\_work\\1\\s\\src\\Layer" + i.ToString(CultureInfo.InvariantCulture) + "\\Helper.cs:line 42"));
        AdoTestRun[] runs = [.. Enumerable.Range(1, 14).Select(i => new AdoTestRun
        {
            Id = 1000 + i, Name = "Synthetic run", BuildId = 401, State = "Completed", StartedDate = clock.AddMinutes(-60 + i), StageName = i <= 7 ? "Tests_EN" : "Tests_FR",
            PipelineAttempt = 1 + ((i - 1) % 7), TeamProject = TestFailureReportFixture.Project, CollectionUri = TestFailureReportFixture.Collection,
        })];
        AdoTestFailure[] failures = [.. Enumerable.Range(1, 100).Select(f => new AdoTestFailure
        {
            Ordinal = f, Classification = AdoTestFailureClassification.Failed, ShortName = "Scenario" + f.ToString(CultureInfo.InvariantCulture),
            TestName = "Synthetic.Ui.CheckoutTests.Scenario" + f.ToString(CultureInfo.InvariantCulture), Storage = "Synthetic.Ui.dll", CollectionUri = TestFailureReportFixture.Collection,
            TestCase = new AdoTestCaseLink { Id = 12000 + f, Title = "Checkout scenario", State = "Ready", IsResolved = true, WebUrl = TestFailureReportFixture.Untrusted },
            Attempts = [.. Enumerable.Range(1, 14).Select(n => new AdoTestAttempt
            {
                Number = n, Source = n == 1 ? AdoTestAttemptSource.Single : AdoTestAttemptSource.RunAttempt, RunId = 1000 + n, ResultId = 100000 + f * 20 + n,
                Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, StartedDate = clock.AddMinutes(-50), CompletedDate = clock.AddMinutes(-49),
                Duration = TimeSpan.FromSeconds(31.2), ComputerName = "SYNTHETIC-AGENT-07", FailureType = "Regression", ResolutionState = "Unresolved", FailingSinceBuildId = 399,
                ErrorMessage = "OpenQA.Selenium.WebDriverTimeoutException : Timed out after 30 seconds waiting for element '#submit-" + f.ToString(CultureInfo.InvariantCulture) + "' to be clickable.",
                StackTrace = "OpenQA.Selenium.WebDriverTimeoutException: Timed out after 30 seconds\n" + frames,
                Attachments = [.. AttachmentNames.Select((name, k) => new AdoTestAttachment
                {
                    Id = n * 10 + k + 1, RunId = 1000 + n, ResultId = 100000 + f * 20 + n, FileName = name, Size = 20480, AttachmentType = "GeneralAttachment",
                    Kind = AttachmentKinds.FromFileName(name),
                })],
            })],
        })];
        TestFailureReportModel model = Model(With(grouped, runs, failures));
        string html = TestFailureReportFixture.Render(model);
        long bytes = Encoding.UTF8.GetByteCount(html);
        Assert.True(bytes < 7_000_000, "The report is " + bytes.ToString("N0", English) + " bytes.");
        Assert.DoesNotMatch("<details[^>]*\\sopen[\\s>=]", html);
    }

    private static TestFailureReportOptions Options(bool includeFlaky = true) => new()
    { Culture = "en-US", SessionCulture = English, GeneratedAt = TestFailureReportFixture.Clock, ToolkitVersion = "5.3.0-test", IncludeFlaky = includeFlaky };

    private static TestFailureReportModel Model(AdoBuildTestFailureSet set) => TestFailureReportModelBuilder.Build(set, Options());

    // The grouped fixture with its runs renamed at one pipeline level; the other levels share one name.
    private static AdoBuildTestFailureSet Named(Func<AdoTestRun, string?> name, string level)
    {
        AdoBuildTestFailureSet set = TestFailureReportFixture.Set("grouped");
        AdoTestRun[] runs = [.. set.Runs.Select(run => level switch
        {
            "stage" => Copy(run, name(run), run.PhaseName, run.JobName),
            "phase" => Copy(run, "Tests", name(run), run.JobName),
            _ => Copy(run, "Tests", run.PhaseName, name(run)),
        })];
        return With(set, runs, set.Failures);
    }

    private static AdoTestRun Copy(AdoTestRun run, string? stage, string? phase, string? job, string? name = null) => new()
    {
        Id = run.Id, Name = name ?? run.Name, BuildId = run.BuildId, State = run.State, StartedDate = run.StartedDate, PipelineAttempt = run.PipelineAttempt,
        StageName = stage, PhaseName = phase, JobName = job, TeamProject = run.TeamProject, CollectionUri = run.CollectionUri,
    };

    private static AdoBuildTestFailureSet With(AdoBuildTestFailureSet set, IReadOnlyList<AdoTestRun> runs, IReadOnlyList<AdoTestFailure> failures) => new()
    {
        Build = set.Build, Runs = runs, Summary = set.Summary, History = set.History, Failures = failures, FailedCount = failures.Count,
        Status = set.Status, RetrievedAt = set.RetrievedAt, CollectionUri = set.CollectionUri,
    };

    private static string[] AttemptIds(string html) => [.. Regex.Matches(html, "<details class=\"attempt\" id=\"([^\"]+)\"").Select(m => m.Groups[1].Value)];

    private static string Section(string html, string start, string end)
    {
        int index = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(index >= 0, start);
        return html[index..html.IndexOf(end, index + start.Length, StringComparison.Ordinal)];
    }
}
