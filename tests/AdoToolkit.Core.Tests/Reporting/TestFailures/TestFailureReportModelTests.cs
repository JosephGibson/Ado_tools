using AdoToolkit.Core.Reporting.Errors;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

public sealed class TestFailureReportModelTests
{
    private static readonly int[] ExpectedResults = [2, 3, 6, 5, 11];
    private static readonly int[] ExpectedOrdinals = [1, 2, 3, 4, 5];
    [Theory]
    [InlineData("fr-CA", "en-US", "en-US", "fr-CA", false)]
    [InlineData(null, "fr-CA", "en-US", "fr-CA", false)]
    [InlineData(null, null, "fr-CA", "fr-CA", false)]
    [InlineData("de-DE", "fr-CA", "en-US", "en", true)]
    public void ReportCultureOrderAndWarningAreExplicit(string? chosen, string? configured, string session, string expected, bool warned)
    {
        var model = TestFailureReportModelBuilder.Build(TestFailureReportFixture.Set("partial"), new TestFailureReportOptions
        { Culture = chosen, ConfiguredCulture = configured, SessionCulture = CultureInfo.GetCultureInfo(session), GeneratedAt = TestFailureReportFixture.Clock, ToolkitVersion = "5.3-test" });
        Assert.Equal(expected, model.Culture.Name);
        Assert.Equal(warned, model.Warnings.Count == 1);
        Assert.Equal(AdoTestFailureStatus.Partial, model.Status);
        Assert.Equal(DiagnosticMessageRenderer.Render(model.Diagnostics[0].Code, model.Diagnostics[0].Arguments, model.Culture), model.Diagnostics[0].Message);
        if (expected == "fr-CA") Assert.NotEqual(TestFailureReportFixture.Set("partial").Diagnostics[0].Message, model.Diagnostics[0].Message);
    }

    [Fact]
    public void SortingUsesClassificationThenOrdinalStorageNameAndResultIdAndAssignsAnchors()
    {
        var source = TestFailureReportFixture.Set("flaky");
        AdoTestFailure Failure(string storage, string name, int result) => new()
        { Ordinal = 999, ShortName = name, TestName = name, Storage = storage, CollectionUri = source.CollectionUri,
            Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = result, Outcome = "Failed" }] };
        var set = new AdoBuildTestFailureSet { Build = source.Build, Summary = source.Summary, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
            Failures = [source.Failures[1], Failure("b", "A", 5), Failure("a", "Z", 6), Failure("a", "A", 3), Failure("a", "A", 2)] };
        var model = TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options("en-US"));
        Assert.Equal(ExpectedResults, model.Failures.Select(f => f.Attempts[0].ResultId));
        Assert.Equal(ExpectedOrdinals, model.Failures.Select(f => f.Ordinal));
        Assert.Equal(999, set.Failures[1].Ordinal);
    }

    // Every failure of the set is classified, flaky tests included, in report order: leaving flaky
    // tests out changes which rows show, never which errors exist.
    [Fact]
    public void EveryFailureIsClassifiedWhetherOrNotItIsShown()
    {
        AdoBuildTestFailureSet set = TestFailureReportFixture.Set("flaky");
        TestFailureReportModel shown = TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options("en-US"));
        TestFailureReportModel hidden = TestFailureReportModelBuilder.Build(set, new TestFailureReportOptions
        { Culture = "en-US", SessionCulture = CultureInfo.GetCultureInfo("en-US"), GeneratedAt = TestFailureReportFixture.Clock, ToolkitVersion = "5.3-test" });
        Assert.Equal(2, shown.Failures.Count);
        Assert.Single(hidden.Failures);
        Assert.Equal(2, hidden.Errors.Profiles.Count);
        Assert.NotEmpty(hidden.Errors.Classes);
        Assert.Equal(shown.Errors.Classes.Select(static error => error.Forms[0].Key), hidden.Errors.Classes.Select(static error => error.Forms[0].Key));
        Assert.Equal(shown.Errors.Profiles.Select(static profile => profile.Primary?.Class.Id), hidden.Errors.Profiles.Select(static profile => profile.Primary?.Class.Id));
    }

    // Configured rules name the errors they match, and a generic one sets them apart; the copy that an
    // export makes after its downloads keeps the classification.
    [Fact]
    public void ConfiguredRulesReachTheClassificationAndSurviveTheDownloads()
    {
        TestFailureReportOptions options = new()
        {
            Culture = "en-US", SessionCulture = CultureInfo.GetCultureInfo("en-US"), GeneratedAt = TestFailureReportFixture.Clock, ToolkitVersion = "5.3-test",
            ErrorRules = [new ErrorRuleOptions { Name = "Submit timeout", Patterns = ["Timed out waiting for *"], Generic = false },
                new ErrorRuleOptions { Name = "Banner", Patterns = ["Banner not shown"] }],
        };
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(TestFailureReportFixture.Set("grouped"), options);
        ErrorProfile submit = model.Errors.Profiles[0], banner = model.Errors.Profiles[1];
        Assert.Null(submit.Primary!.Class.Rule);
        Assert.Equal(2, submit.Primary.Count);
        ErrorProfileEntry timeout = Assert.Single(submit.Others);
        Assert.Equal("Submit timeout", timeout.Class.Rule!.Name);
        Assert.False(timeout.Class.IsGeneric);
        Assert.Equal("Banner", banner.Primary!.Class.Rule!.Name);
        Assert.True(banner.Primary.Class.IsGeneric);
        Assert.Same(model.Errors, TestFailureReportModelBuilder.WithAttachments(model, model.Failures, [], null).Errors);
        Assert.All(TestFailureReportFixture.Model("grouped").Errors.Classes, static error => Assert.Null(error.Rule));
    }

    [Theory]
    [InlineData("Git", "11111111-2222-3333-4444-555555555555", "0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData("TfsGit", "11111111-2222-3333-4444-555555555555", "ABCDEF0123456789ABCDEF0123456789ABCDEF01", true)]
    [InlineData("Tfvc", "11111111-2222-3333-4444-555555555555", "0123456789abcdef0123456789abcdef01234567", false)]
    [InlineData("Git", "../repository", "0123456789abcdef0123456789abcdef01234567", false)]
    [InlineData("Git", "11111111-2222-3333-4444-555555555555", "0123456", false)]
    [InlineData("Git", "11111111-2222-3333-4444-555555555555", "../3456789abcdef0123456789abcdef01234567", false)]
    public void CommitRouteRequiresGitGuidAndFullHexSha(string type, string repository, string sha, bool valid)
    {
        Uri? uri = AdoWebLinks.Commit(TestFailureReportFixture.Collection, TestFailureReportFixture.Project, type, repository, sha);
        Assert.Equal(valid, uri is not null);
        if (valid) Assert.Equal(TestFailureReportFixture.Collection.AbsoluteUri + Uri.EscapeDataString(TestFailureReportFixture.Project)
            + "/_git/" + repository + "/commit/" + sha, uri!.AbsoluteUri);
    }

    [Fact]
    public void AllWebRoutesUseEncodedProjectNumericIdsAndConnectionOrigin()
    {
        var model = TestFailureReportFixture.Model();
        string prefix = TestFailureReportFixture.Collection.AbsoluteUri + Uri.EscapeDataString(TestFailureReportFixture.Project);
        Assert.Equal(prefix + "/_build/results?buildId=401", model.BuildUrl.AbsoluteUri);
        Assert.Equal(prefix + "/_build?definitionId=12", model.DefinitionUrl.AbsoluteUri);
        Assert.Equal(prefix + "/_build/results?buildId=401&view=ms.vss-test-web.build-test-results-tab", model.ResultsUrl.AbsoluteUri);
        Assert.Equal(prefix + "/_testManagement/runs?_a=runCharts&runId=201", AdoWebLinks.TestRun(TestFailureReportFixture.Collection, TestFailureReportFixture.Project, 201).AbsoluteUri);
        Assert.Equal(prefix + "/_build/results?buildId=401&view=ms.vss-test-web.build-test-results-tab&runId=201&resultId=11",
            AdoWebLinks.BuildTestResult(TestFailureReportFixture.Collection, TestFailureReportFixture.Project, 401, 201, 11).AbsoluteUri);
        string html = TestFailureReportFixture.Render(model);
        Assert.Contains("/_workitems/edit/801", html, StringComparison.Ordinal);
        // Every attempt names bug 802 as associated, but it is closed and so not among the test's bugs.
        Assert.DoesNotContain("/_workitems/edit/802", html, StringComparison.Ordinal);
        Assert.Contains("/_build/results?buildId=400", html, StringComparison.Ordinal);
    }
}
