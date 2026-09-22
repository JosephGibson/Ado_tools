using System.Net.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-8")]
public sealed class TestCaseLinkResolutionTests
{
    // Test results fixture 9: valid, missing from the batch, and a non-integer reference.
    [Fact]
    public async Task ValidMissingAndInvalidReferencesEachGetTheirDocumentedOutcome()
    {
        TestRunFixture fixture = Links();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 1 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        AdoTestFailure valid = Assert.Single(set.Failures, failure => failure.ShortName == "Valid");
        Assert.Equal(1010, valid.TestCase!.Id);
        Assert.True(valid.TestCase.IsResolved);
        Assert.Equal("Vérifier le panier", valid.TestCase.Title);
        Assert.Equal("Design", valid.TestCase.State);
        AdoTestFailure missing = Assert.Single(set.Failures, failure => failure.ShortName == "Missing");
        Assert.Equal(1011, missing.TestCase!.Id);
        Assert.False(missing.TestCase.IsResolved);
        Assert.Null(missing.TestCase.Title);
        Assert.Null(missing.TestCase.Rev);
        // The ID link is kept even though the work item could not be read.
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/1011",
            missing.TestCase.WebUrl.AbsoluteUri);
        // A reference that is not a positive integer produces no link at all.
        AdoTestFailure invalid = Assert.Single(set.Failures, failure => failure.ShortName == "Invalid");
        Assert.Null(invalid.TestCase);
        Assert.Equal([DiagnosticCodes.InvalidTestCaseReference, DiagnosticCodes.UnresolvedTestCase],
            set.Diagnostics.Select(static item => item.Code).Order(StringComparer.Ordinal));
        AdoDiagnostic reference = Assert.Single(set.Diagnostics, item => item.Code == DiagnosticCodes.InvalidTestCaseReference);
        Assert.Equal(AdoDiagnosticSeverity.Warning, reference.Severity);
        Assert.Equal(["103", "201"], reference.Arguments);
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
    }

    [Fact]
    public async Task OnlyTheDistinctValidIdsTravelInOneBatchRequestWithRelationsExpanded()
    {
        TestRunFixture fixture = Links();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 1 },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        RequestSnapshot batch = Assert.Single(handler.Requests, request =>
            request.Uri.AbsolutePath.EndsWith("/_apis/wit/workitemsbatch", StringComparison.Ordinal));
        // Relations feed the bug lookup. The API cannot combine them with a field list, so every
        // field comes back and only the title and state are read.
        Assert.Equal("{\"ids\":[1010,1011],\"errorPolicy\":\"omit\",\"$expand\":\"relations\"}", batch.Body);
    }

    [Theory]
    [InlineData("1010", true, 1010)]
    [InlineData("TC-1012", false, 0)]
    [InlineData("0", false, 0)]
    [InlineData("-5", false, 0)]
    [InlineData(" 12 ", false, 0)]
    [InlineData("12.0", false, 0)]
    [InlineData("1e3", false, 0)]
    [InlineData("2147483648", false, 0)]
    public void ReferenceParsingAcceptsOnlyPlainPositiveIntegers(string value, bool expected, int id)
    {
        Assert.Equal(expected, TestCaseLinkResolver.TryParseReference(value, out int parsed));
        Assert.Equal(id, expected ? parsed : 0);
    }

    private static TestRunFixture Links() => new TestRunFixture()
        .Route("runs-two.json", "/test/runs", "%24skip=0&")
        .Route("results-testcases.json", "/Runs/201/results", "%24skip=0&")
        .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
        .RouteBody(Detail(101, "1010"), "/Runs/201/results/101?")
        .RouteBody(Detail(102, "1011"), "/Runs/201/results/102?")
        .RouteBody(Detail(103, "TC-1012"), "/Runs/201/results/103?")
        .Route("attachments-empty.json", "/attachments")
        .Route("workitems-testcases.json", "workitemsbatch");

    private static string Detail(int id, string testCase) =>
        "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"outcome\":\"Failed\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\",\"automatedTestName\":\"Contoso.Web.Tests.LinkTests."
        + id switch { 101 => "Valid", 102 => "Missing", _ => "Invalid" }
        + "\",\"testCase\":{\"id\":\"" + testCase + "\"},\"errorMessage\":\"Link check failed.\"}";
}
