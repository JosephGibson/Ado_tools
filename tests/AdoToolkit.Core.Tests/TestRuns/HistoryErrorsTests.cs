using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// What history keeps of a failed result's error message (D-8): the start that the classifier reads,
// as the listing sent it, and for each test at most five distinct starts of a build.
[Trait("Culture", "Invariant")]
public sealed class HistoryErrorsTests
{
    [Fact]
    public void TheStartIsTheFirstTwentyLinesAsSent()
    {
        string lines = string.Join("\r\n", Enumerable.Range(1, 25).Select(static line => "Line " + line.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(string.Join("\r\n", Enumerable.Range(1, 20).Select(static line => "Line " + line.ToString(CultureInfo.InvariantCulture))),
            ErrorMessageStart.Of(lines));
        // Blank lines count, as the classifier reads them; a short message is kept whole.
        Assert.Equal("Assert failed:\n\nExpected: 1\r\n", ErrorMessageStart.Of("Assert failed:\n\nExpected: 1\r\n"));
        Assert.Null(ErrorMessageStart.Of(null));
        Assert.Null(ErrorMessageStart.Of(""));
    }

    // A start never holds more than MaximumCharacters, and a cut never splits a surrogate pair.
    [Fact]
    public void ALongLineIsCutWithoutSplittingAPair()
    {
        string line = new string('x', ErrorMessageStart.MaximumCharacters - 1) + "😀 and more";
        string start = ErrorMessageStart.Of(line)!;
        Assert.Equal(ErrorMessageStart.MaximumCharacters - 1, start.Length);
        Assert.Equal(new string('x', ErrorMessageStart.MaximumCharacters - 1), start);
    }

    // The distinct starts of a test's failed records, five at most in record order, each with the
    // pipeline key of its first record, and whether some were dropped; a passed record's message is
    // not kept.
    [Fact]
    public void ABuildKeepsFiveDistinctStartsPerTest()
    {
        static TestResultRecord Record(int id, string outcome, string? message, string key) => new()
        { RunId = 1, ResultId = id, RunOrder = 1, PipelineKey = key, Outcome = outcome, ErrorMessage = message };
        TestIdentity identity = TestIdentity.ForAutomated("Synthetic.dll", "Synthetic.Tests.Totals");
        TestIdentityGroup many = new()
        {
            Identity = identity,
            Records = [.. Enumerable.Range(1, 7).Select(static id => Record(id, "Failed", "Error " + "ABCDEFG"[id - 1], id % 2 == 0 ? "Tests_FR" : "Tests_EN")),
                Record(8, "Failed", "Error A", "Tests_EN"), Record(9, "Passed", "Passed with a warning", "Tests_EN")],
        };
        TestIdentity quiet = TestIdentity.ForAutomated("Synthetic.dll", "Synthetic.Tests.Quiet");
        TestIdentityGroup silent = new() { Identity = quiet, Records = [Record(10, "Failed", null, "Tests_EN")] };
        HistoryBuildData data = HistoryBuildData.FromGroups([many, silent], TestContext.Current.CancellationToken);
        HistoryErrors errors = data.Errors[identity];
        Assert.Equal(["Error A", "Error B", "Error C", "Error D", "Error E"], errors.Starts);
        Assert.True(errors.Dropped);
        Assert.Equal(["Tests_EN", "Tests_FR", "Tests_EN", "Tests_FR", "Tests_EN"], errors.Keys);
        Assert.False(data.Errors.ContainsKey(quiet));
    }

    // A build keeps at most MaximumCharacters of distinct starts, so a large outage stays small in
    // memory: the tests read after that keep none, and a test that lost some says so. Identical
    // starts of several tests share one string and count once.
    [Fact]
    public void ABuildKeepsABoundedAmountOfText()
    {
        static TestIdentityGroup Group(int test, params string[] messages) => new()
        {
            Identity = TestIdentity.ForAutomated("Synthetic.dll", "Synthetic.Tests.T" + test.ToString(CultureInfo.InvariantCulture)),
            Records = [.. messages.Select((message, index) => new TestResultRecord
                { RunId = 1, ResultId = 10 * test + index, RunOrder = 1, Outcome = "Failed", ErrorMessage = message })],
        };
        static string Start(int test) => new string('x', ErrorMessageStart.MaximumCharacters - 6) + test.ToString("D6", CultureInfo.InvariantCulture);
        int fit = HistoryErrors.MaximumCharacters / ErrorMessageStart.MaximumCharacters;
        TestIdentityGroup[] groups = [.. Enumerable.Range(0, fit - 1).Select(test => Group(test, Start(test))),
            Group(fit - 1, Start(fit - 1), Start(1_000_000)), .. Enumerable.Range(fit, 5).Select(test => Group(test, Start(test)))];
        HistoryBuildData data = HistoryBuildData.FromGroups(groups, TestContext.Current.CancellationToken);
        Assert.Equal(fit, data.Errors.Count);
        Assert.True(data.Errors[groups[fit - 1].Identity].Dropped);
        Assert.False(data.Errors[groups[0].Identity].Dropped);
        Assert.False(data.Errors.ContainsKey(groups[^1].Identity));

        // Two strings of one text, as two listed results would bring them.
        HistoryBuildData shared = HistoryBuildData.FromGroups([Group(1, new string("Cart is empty".AsSpan())), Group(2, new string("Cart is empty".AsSpan()))],
            TestContext.Current.CancellationToken);
        Assert.Same(shared.Errors[groups[1].Identity].Starts[0], shared.Errors[groups[2].Identity].Starts[0]);
    }
}
