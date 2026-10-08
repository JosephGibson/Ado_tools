using AdoToolkit.Core.Reporting.Errors;
using AdoToolkit.Core.Reporting.Highlighting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// How By error, the Overview and Open bugs group tests: one cluster per error that the classifier
// found in the failed attempts, and the key line with every URL, GUID, path, hexadecimal ID and
// number as a placeholder of its kind.
public sealed class ErrorClustersTests
{
    [Theory]
    [InlineData("Timed out after 3000 ms", "Timed out after 30000 ms")]
    [InlineData("Expected 1,234.5 but was 7", "Expected 2 but was 3.25")]
    [InlineData("Order 3f2504e0-4f89-11d3-9a0c-0305e82c3301 was not found", "Order 9B1DEB4D-3B7D-4BAD-9BDD-2B0D7B3DCB6D was not found")]
    [InlineData("GET https://shop.example.test/api/orders/1?x=2 failed", "GET http://api.example.test/v2/items failed")]
    [InlineData(@"Could not find file 'C:\agent\_work\7\s\data\customers.json'.", @"Could not find file 'D:\b\orders.json'.")]
    [InlineData(@"\\share.example.test\drop\a.log is locked", @"\\files.example.test\x\b.log is locked")]
    [InlineData("Missing /home/agent/work/3/s/out/invoices.json", "Missing /var/tmp/x.json")]
    [InlineData("Handle 0x7FFE1234 is invalid", "Handle 0x1 is invalid")]
    [InlineData("Commit a1b2c3d4e5f6 not found", "Commit 0123abcd not found")]
    public void LinesThatDifferOnlyInVariablePartsShareAKey(string first, string second)
    {
        Assert.Equal(ErrorClusters.Key(first), ErrorClusters.Key(second));
        Assert.Equal(ErrorClusters.Key(first), HtmlTestFailureRenderer.ErrorKey(second));
    }

    [Theory]
    // A path is not a number.
    [InlineData(@"Value C:\data\y.txt", "Value 12")]
    // A hexadecimal ID mixes digits and letters: a word of letters alone is text.
    [InlineData("Token deadbeefcafe rejected", "Token feedfacebead rejected")]
    // Fewer than eight hexadecimal digits are not an ID.
    [InlineData("Code a1b2c3 rejected", "Code d4e5f6 rejected")]
    [InlineData("Expected 4 results", "Expected 4 items")]
    public void OtherTextOrAnotherKindKeepsKeysApart(string first, string second) =>
        Assert.NotEqual(ErrorClusters.Key(first), ErrorClusters.Key(second));

    [Theory]
    // MSTest names the test on the first line and the exception on the next: the exception is the key line.
    [InlineData("Test method A.B threw exception: \nSynthetic.ApiException: Order 1 was not found.", "Synthetic.ApiException: Order 1 was not found.")]
    [InlineData("Test method A.B threw exception:\r\n\r\n   System.TimeoutException: late", "System.TimeoutException: late")]
    [InlineData("Test method A.B threw exception:\n ---> System.IO.IOException: gone", "---> System.IO.IOException: gone")]
    // A first line that ends with a colon stays the key line when no exception header follows it.
    [InlineData("Assert failed:\nExpected: 1", "Assert failed:")]
    [InlineData("Only colon:", "Only colon:")]
    [InlineData("Plain line\nSystem.Exception: x", "Plain line")]
    // Blank lines are skipped and control characters removed.
    [InlineData("  \n\u0007Bell\u001B line  ", "Bell line")]
    public void TheKeyLineIsTheFirstLineOrTheExceptionAfterMsTestsFirstLine(string message, string expected) =>
        Assert.Equal(expected, ErrorClusters.KeyLine(message));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n \r\n")]
    public void NoTextHasNoKeyLine(string? message) => Assert.Null(ErrorClusters.KeyLine(message));

    [Theory]
    // The innermost exception, after --->, on the same line or a later one.
    [InlineData("System.InvalidOperationException: outer ---> System.TimeoutException: inner", null, "System.TimeoutException")]
    [InlineData("System.AggregateException: x\n ---> System.IO.IOException: y", null, "System.IO.IOException")]
    [InlineData("AssertionError: expected 2", null, "AssertionError")]
    // A message without one leaves the trace to name it.
    [InlineData("Expected 4 results", "System.NullReferenceException: Object reference\n   at Synthetic.A.B()", "System.NullReferenceException")]
    // Only at the start of a line or after --->.
    [InlineData("Look at Synthetic.BarException: in the middle", null, null)]
    [InlineData(null, null, null)]
    public void TheExceptionTypeIsTheInnermostOfTheMessageThenOfTheTrace(string? message, string? trace, string? expected) =>
        Assert.Equal(expected, ErrorClusters.ExceptionType(Attempt(message, trace)));

    [Theory]
    [InlineData("System.IO.FileNotFoundException", "FileNotFoundException")]
    [InlineData("AssertionError", "AssertionError")]
    [InlineData("Trailing.", "Trailing.")]
    public void ATypeShowsWithoutItsNamespace(string type, string expected) => Assert.Equal(expected, ErrorClusters.ShortType(type));

    [Theory]
    [InlineData("Synthetic.Web.Pages.CheckoutPage.Submit", "CheckoutPage.Submit()")]
    [InlineData("Synthetic.Web.Pages.CheckoutPage.<SubmitAsync>d__4.MoveNext", "CheckoutPage.SubmitAsync()")]
    [InlineData("Main", "Main()")]
    public void AFrameShowsItsTypeAndMethod(string method, string expected) => Assert.Equal(expected, ErrorClusters.ShortFrame(method));

    // The first frame of the test's own root namespace, among the first lines of the trace only.
    [Fact]
    public void TheFirstFrameOfANamespaceIsReadFromTheTopOfTheTrace()
    {
        const string Trace = "   at OpenQA.Selenium.Support.UI.DefaultWait`1.Until[TResult](Func`2 condition)\n"
            + "   at Synthetic.Web.Pages.CheckoutPage.Submit() in C:\\agent\\_work\\12\\s\\src\\Pages\\CheckoutPage.cs:line 88\n   at Synthetic.Web.Tests.Submit()";
        Assert.Equal("Synthetic.Web.Pages.CheckoutPage.Submit", StackTraceLexer.FirstFrame(Trace, "Synthetic."));
        Assert.Null(StackTraceLexer.FirstFrame(Trace, "Synthetic.", maximumLines: 1));
        Assert.Null(StackTraceLexer.FirstFrame(Trace, "Other."));
    }

    // The large fixture: each test failed with one error, so clusters of 4, 3 and three of 2, then
    // single tests, and last the test without a message. Ties follow the first test's ordinal.
    [Fact]
    public void ClustersComeLargestFirstWithWhatTheirTestsShare()
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large");
        IReadOnlyList<ErrorClusters.Cluster> clusters = ErrorClusters.Of(model);
        Assert.Equal([4, 3, 2, 2, 2, 1, 1, 1], clusters.Select(static cluster => cluster.Members.Count));
        Assert.Equal([4, 3, 2, 2, 2, 1, 1, 1], clusters.Select(static cluster => cluster.PrimaryCount));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], clusters.Select(static cluster => cluster.Number));
        Assert.Equal([1, 3, 13], clusters.Skip(2).Take(3).Select(static cluster => cluster.Representative.Failure.Ordinal));
        Assert.Null(clusters[^1].Error);
        Assert.Equal("UpdateAvatar", Assert.Single(clusters[^1].Members).Failure.ShortName);
        Assert.All(clusters.SkipLast(1), static cluster => Assert.NotNull(cluster.Error));
        Assert.All(clusters, static cluster => Assert.False(cluster.IsGeneric));
        Assert.All(clusters.SelectMany(static cluster => cluster.Members), static member => Assert.True(member.IsPrimary));

        // Timeouts after 3000, 4500 and 30000 ms: one slot differs, its values in test order.
        ErrorClusters.Cluster timeouts = clusters[0];
        Assert.Equal("System.TimeoutException", timeouts.ExceptionType);
        int slot = Assert.Single(timeouts.Varying);
        Assert.Equal(["3000", "4500", "30000"], timeouts.Values(slot));
        Assert.Equal(["3000"], timeouts.ValuesOf(timeouts.Representative));
        Assert.Equal(("Synthetic.Web.Pages.CheckoutPage.Submit", 4), (timeouts.Frame, timeouts.FrameCount));
        // MSTest's first line names the test: the exception below it makes the key, so two tests group.
        ErrorClusters.Cluster api = clusters[2];
        Assert.Equal("Synthetic.Web.Api.ApiException", api.ExceptionType);
        Assert.Equal(["CancelOrder", "GetOrder"], api.Members.Select(static member => member.Failure.ShortName));
        Assert.Equal("Synthetic.Web.Api.ApiException", api.Representative.PrefixType);
        // Two values differ; the tests share no frame of their own namespace, so none is named.
        ErrorClusters.Cluster search = clusters[4];
        Assert.Null(search.ExceptionType);
        Assert.Equal(2, search.Varying.Count);
        Assert.Equal((null, 0), (search.Frame, search.FrameCount));
    }

    // Every error that a shown test had is a cluster. A test sits under its primary error and comes
    // back, as another error's member, under each of its other errors. Specific errors come first, by
    // the tests whose primary error they are, then by all their tests; then the generic errors; then
    // the tests without a message.
    [Fact]
    public void EveryErrorIsAClusterAndATestComesBackUnderItsOtherErrors()
    {
        IReadOnlyList<ErrorClusters.Cluster> clusters = ErrorClusters.Of(TestFailureReportFixture.Model("bilingual"));
        Assert.Equal(["CheckTitle", "AddToCart", "ConfirmOrder", "OpenHome", "ShowBanner", "SubmitOrder", "LoadReport", "ConfirmOrder", "SubmitOrder", "UpdateAvatar"],
            clusters.Select(static cluster => cluster.Representative.Failure.ShortName));
        Assert.Equal([1, 1, 1, 1, 1, 1, 1, 0, 0, 1], clusters.Select(static cluster => cluster.PrimaryCount));
        Assert.Equal([2, 1, 1, 1, 1, 1, 1, 1, 1, 1], clusters.Select(static cluster => cluster.Members.Count));
        Assert.Equal([false, false, false, false, false, true, true, true, true, false], clusters.Select(static cluster => cluster.IsGeneric));
        Assert.Equal(["WebDriverSession", null, "ConnectionRefused", "ServerUnavailable"], clusters.Skip(5).Take(4).Select(static cluster => cluster.Error!.Rule!.BuiltInId));
        Assert.Equal("Test data reset", clusters[6].Error!.Rule!.Name);
        Assert.Null(clusters[^1].Error);

        // CheckTitle failed with the English and the French form of one assertion at one place: one
        // error, which AddToCart had once. Its primary members come first.
        ErrorClusters.Cluster welcome = clusters[0];
        Assert.Equal(2, welcome.Error!.Forms.Count);
        Assert.Equal([("CheckTitle", true), ("AddToCart", false)], welcome.Members.Select(static member => (member.Failure.ShortName, member.IsPrimary)));
        // Each member shows its latest attempt with the error: the forms share a layout, so their
        // expected and actual values line up and differ.
        Assert.Equal([4, 1], welcome.Members.Select(static member => member.Entry!.Latest.AttemptNumber));
        Assert.Equal(2, welcome.Varying.Count);
        Assert.Equal(["Bienvenue", "Welcome"], welcome.Values(welcome.Varying.Min()));
        Assert.Equal(["Welcome", "Error"], welcome.ValuesOf(welcome.Members[1]));
        Assert.Equal(("Synthetic.Shop.Pages.HomePage.CheckTitle", 2), (welcome.Frame, welcome.FrameCount));

        // OpenHome's two wordings are its own texts, which pairing joined; one test shows one of them,
        // so nothing differs.
        ErrorClusters.Cluster home = clusters[3];
        Assert.Equal(2, home.Error!.Forms.Count);
        Assert.Equal("OpenHome", Assert.Single(home.Members).Failure.ShortName);
        Assert.Empty(home.Varying);
    }

    // Members of two layouts, such as the two wordings that a rule joins, mark nothing.
    [Fact]
    public void MembersOfTwoLayoutsShowNoValuesThatDiffer()
    {
        AdoTestFailure Failure(int ordinal, string message) => new()
        {
            Ordinal = ordinal, Classification = AdoTestFailureClassification.Failed, ShortName = "T" + ordinal.ToString(CultureInfo.InvariantCulture),
            TestName = "Synthetic.Layout.T" + ordinal.ToString(CultureInfo.InvariantCulture), CollectionUri = TestFailureReportFixture.Collection,
            Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = ordinal, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = message }],
        };
        AdoTestFailure[] failures = [Failure(1, "Home page did not load within 30 seconds"), Failure(2, "La page d'accueil ne s'est pas chargée en moins de 45 secondes")];
        ErrorClassification errors = ErrorClassifier.Classify(failures, PipelineGrouping.None,
            BuiltInErrorRules.AfterConfigured([new ErrorRuleOptions { Name = "Home", Patterns = ["Home page did not load*", "La page d'accueil ne s'est pas chargée*"] }]));
        ErrorClusters.Cluster home = Assert.Single(ErrorClusters.Of(failures, errors));
        Assert.Equal(2, home.Members.Count);
        Assert.False(home.SingleLayout);
        Assert.Empty(home.Varying);
    }

    // The slots that vary come from every member: a first member whose timeout kept no call log hides
    // none of the others' elements, and its own Values are the values it has.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASlotThatTheFirstMemberLacksStillVaries(bool bareFirst)
    {
        const string Bare = "System.TimeoutException : Timeout 45000ms exceeded.";
        static string Logged(string locator) => "System.TimeoutException : Timeout 30000ms exceeded.\r\nCall log:\r\n  - waiting for " + locator;
        string[] messages = bareFirst ? [Bare, Logged("Locator(\"#place-order\")"), Logged("Locator(\"#coupon\")")]
            : [Logged("Locator(\"#place-order\")"), Bare, Logged("Locator(\"#coupon\")")];
        AdoTestFailure[] failures = [.. messages.Select(static (message, index) => TestWith(index + 1, message))];
        ErrorClusters.Cluster timeouts = Assert.Single(ErrorClusters.Of(failures, ErrorClassifier.Classify(failures, PipelineGrouping.None, BuiltInErrorRules.All)));
        Assert.Equal("PlaywrightTimeout", timeouts.Error!.Rule!.BuiltInId);
        Assert.True(timeouts.SingleLayout);
        Assert.Equal([3, 6], timeouts.Varying.Order());
        Assert.Equal(["Locator(\"#place-order\")", "Locator(\"#coupon\")"], timeouts.Values(6));
        ErrorClusters.Member bare = timeouts.Members.Single(static member => member.Parts.All(static part => part.Slot != 6));
        Assert.Equal(["45000"], timeouts.ValuesOf(bare));
        Assert.Equal(["30000", "Locator(\"#coupon\")"], timeouts.ValuesOf(timeouts.Members[2]));
    }

    // A test's other errors, the most frequent first, each with its cluster.
    [Fact]
    public void AMemberKnowsItsOtherErrorsAndWhereTheyAre()
    {
        IReadOnlyList<ErrorClusters.Cluster> clusters = ErrorClusters.Of(TestFailureReportFixture.Model("bilingual"));
        ErrorClusters.Member cart = clusters[1].Members[0];
        Assert.True(cart.IsPrimary);
        Assert.Equal((3, 4), (cart.Entry!.Count, cart.Profile.FailedAttempts));
        Assert.Same(clusters[0], Assert.Single(ErrorClusters.ClustersOf(clusters, cart.Profile.Others)));
        // The muted member under the error it had once points back to its primary error.
        ErrorClusters.Member muted = clusters[0].Members[1];
        Assert.Same(clusters[1], ErrorClusters.ClustersOf(clusters, [muted.Profile.Primary!])[0]);
    }

    private static AdoTestAttempt Attempt(string? message, string? trace) => new()
    {
        Number = 1, RunId = 201, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = message, StackTrace = trace,
    };

    private static AdoTestFailure TestWith(int ordinal, string message) => new()
    {
        Ordinal = ordinal, Classification = AdoTestFailureClassification.Failed, ShortName = "T" + ordinal.ToString(CultureInfo.InvariantCulture),
        TestName = "Synthetic.Shop.T" + ordinal.ToString(CultureInfo.InvariantCulture), CollectionUri = TestFailureReportFixture.Collection,
        Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = ordinal, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = message }],
    };
}
