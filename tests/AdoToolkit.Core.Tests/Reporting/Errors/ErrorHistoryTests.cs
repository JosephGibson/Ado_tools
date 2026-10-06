using AdoToolkit.Core.Reporting.Errors;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.Errors;

// A test's primary error against the error of its previous failed build (D-8): the starts of the
// messages that build listed are read as the test's own attempts are. The comparison says nothing
// when it cannot be sure: a key that needs the test's own frame, a message cut before its end, a
// start that was dropped, or a previous error only seen where the test did not fail this time.
public sealed class ErrorHistoryTests
{
    private static readonly Uri Collection = new("https://ado.example.test/Collection/");
    private static readonly PipelineGrouping Grouping = PipelineGrouping.Create([Run(301, "Tests_EN"), Run(302, "Tests_FR")]);
    private static readonly string English = Grouping.KeyOf(301), French = Grouping.KeyOf(302);

    [Theory]
    // Values differ, the error is the same.
    [InlineData("Expected 4 items but found 1", "Same")]
    [InlineData("Cart is empty", "Other")]
    // An English template keeps its identity: another expected value is another error.
    [InlineData("Assert.Equal() Failure: Values differ\nExpected: \"Welcome\"\nActual:   \"Home\"", "Same", "Assert.Equal() Failure: Values differ\nExpected: \"Welcome\"\nActual:   \"Cart\"")]
    [InlineData("Assert.Equal() Failure: Values differ\nExpected: \"Basket\"\nActual:   \"Cart\"", "Other", "Assert.Equal() Failure: Values differ\nExpected: \"Welcome\"\nActual:   \"Cart\"")]
    public void ThePreviousErrorIsTheSameOrAnother(string previous, string verdict, string current = "Expected 4 items but found 0")
    {
        ErrorComparison comparison = Compare(Failure(current, Entry(400, previous)))!;
        Assert.Equal((400, "20260915.2", verdict == "Same"), (comparison.BuildId, comparison.BuildNumber, comparison.Same));
    }

    [Fact]
    public void AnUncertainComparisonSaysNothing()
    {
        // The French stage's start, while this build failed in English only.
        Assert.Null(Compare(Failure("Expected 4 items but found 0", Entry(400, "Le panier est vide", key: French))));
        // A start cut by the server: what it lost might have matched.
        Assert.Null(Compare(Failure("Expected 4 items but found 0", Entry(400, "Cart is empty " + new string('x', ErrorMessageStart.ListingCut)))));
        // More than five starts were listed: one that was dropped might have matched.
        Assert.Null(Compare(Failure("Expected 4 items but found 0", Entry(400, "Cart is empty", dropped: true))));
        // A located error keys on the test's own frame, which a listing does not carry.
        Assert.Null(Compare(Failure("Assert.IsTrue failed.", Entry(400, "Assert.IsTrue failed."))));
        // No earlier failed build listed a message.
        Assert.Null(Compare(Failure("Expected 4 items but found 0")));
    }

    // A match is a match: a cut or dropped start, or one from the other stage, can still be the same.
    [Fact]
    public void AMatchNeedsNoCertaintyAboutTheRest()
    {
        Assert.True(Compare(Failure("Expected 4 items but found 0", Entry(400, "Expected 4 items but found 2", dropped: true)))!.Same);
        Assert.True(Compare(Failure("Expected 4 items but found 0", Entry(400, "Expected 4 items but found 2", key: French)))!.Same);
    }

    // The previous failed build is the latest earlier build where the test failed or was flaky. When
    // its listing carried no message, nothing is said: an older build would not be the previous one.
    [Fact]
    public void ThePreviousFailedBuildIsTheLatestOne()
    {
        Assert.Equal((398, false), (Compare(Failure("Expected 4 items but found 0", Entry(398, "Cart is empty"), Entry(399, null, AdoTestHistoryOutcome.Passed)))!.BuildId,
            Compare(Failure("Expected 4 items but found 0", Entry(398, "Cart is empty"), Entry(399, null, AdoTestHistoryOutcome.Passed)))!.Same));
        Assert.Null(Compare(Failure("Expected 4 items but found 0", Entry(398, "Cart is empty"), Entry(400, null))));
        Assert.Equal(398, Compare(Failure("Expected 4 items but found 0", Entry(398, "Expected 4 items but found 9", AdoTestHistoryOutcome.Flaky)))!.BuildId);
    }

    private static ErrorComparison? Compare(AdoTestFailure failure) =>
        Assert.Single(ErrorClassifier.Classify([failure], Grouping, BuiltInErrorRules.All).Comparisons);

    // A test that failed in the English stage, with its frames, and its history ending in the current build.
    private static AdoTestFailure Failure(string message, params AdoTestHistoryEntry[] earlier) => new()
    {
        Ordinal = 1, Classification = AdoTestFailureClassification.Failed, ShortName = "Totals", TestName = "Synthetic.Orders.OrderTests.Totals", CollectionUri = Collection,
        Attempts = [new AdoTestAttempt
        {
            Number = 1, RunId = 301, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = message,
            StackTrace = @"   at Synthetic.Orders.OrderTests.Totals() in C:\agent\_work\1\s\OrderTests.cs:line 42",
        }],
        History = [.. earlier, new AdoTestHistoryEntry
        {
            BuildId = 401, BuildNumber = "20260916.1", Outcome = AdoTestHistoryOutcome.Failed, IsCurrent = true, WebUrl = new Uri(Collection, "_build/results?buildId=401"),
        }],
    };

    private static AdoTestHistoryEntry Entry(int build, string? start, AdoTestHistoryOutcome outcome = AdoTestHistoryOutcome.Failed, string? key = null, bool dropped = false) => new()
    {
        BuildId = build, BuildNumber = "20260915." + (build - 398).ToString(CultureInfo.InvariantCulture), Outcome = outcome,
        WebUrl = new Uri(Collection, "_build/results?buildId=" + build.ToString(CultureInfo.InvariantCulture)),
        ErrorMessages = start is null ? [] : [start], ErrorMessageKeys = start is null ? [] : [key ?? English], ErrorMessagesDropped = dropped,
    };

    private static AdoTestRun Run(int id, string stage) => new()
    {
        Id = id, Name = "Synthetic " + stage, BuildId = 401, State = "Completed", StageName = stage, PhaseName = "UiTests", JobName = "__default",
        TeamProject = "Synthetic Web", CollectionUri = Collection,
    };
}
