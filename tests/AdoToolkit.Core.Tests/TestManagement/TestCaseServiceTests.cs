using System.Text.Json.Nodes;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-2")]
public sealed class TestCaseServiceTests
{
    private static readonly string[] Values1 = ["1", "2"];
    private static readonly string[] Values2 = ["user", "password"];
    [Theory]
    [InlineData("04-unknown-type.xml", DiagnosticCodes.UnknownStepType)]
    [InlineData("07-nested.xml", DiagnosticCodes.NestedEncodingDecoded)]
    [InlineData("17-missing-ref.xml", DiagnosticCodes.InvalidSharedStepReference)]
    [InlineData("18-invalid-ref.xml", DiagnosticCodes.InvalidSharedStepReference)]
    [InlineData("19-compref-children.xml", DiagnosticCodes.UnexpectedComprefChildren)]
    [InlineData("23-malformed-root.xml.txt", DiagnosticCodes.MalformedStepsXml)]
    [InlineData("27-dtd.xml.txt", DiagnosticCodes.MalformedStepsXml)]
    public async Task ParserDiagnosticsSurviveEndToEnd(string fixture, string code)
    {
        using var handler = ExpansionFixture.Handler(ExpansionFixture.Item(1, ExpansionFixture.Xml(fixture)));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Single(result.Diagnostics, d => d.Code == code);
        Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticMessageRenderer.Render(d.Code, d.Arguments, CultureInfo.CurrentCulture), d.Message));
        if (code == DiagnosticCodes.InvalidSharedStepReference)
        {
            Assert.Null(result.Steps[0].SharedStep);
            Assert.Equal(code, result.Steps[0].DiagnosticCode);
            Assert.Equal("1", result.Diagnostics.Single(d => d.Code == code).StepNumber);
        }
    }

    [Fact]
    public async Task ResolvedItemWithoutStepsHasWarningGroupAndNoChildren()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(new JsonObject { ["value"] = new JsonArray(ExpansionFixture.Item(1, ExpansionFixture.Xml("10-shared.xml"))) }.ToJsonString()));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("shared-no-steps.json"));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        AdoTestStep group = Assert.Single(result.Steps);
        Assert.False(group.IsExpanded);
        Assert.Equal(DiagnosticCodes.SharedStepHasNoSteps, group.DiagnosticCode);
        Assert.Equal(AdoDiagnosticSeverity.Warning, Assert.Single(result.Diagnostics).Severity);
        Assert.Equal(AdoTestCaseStatus.Complete, result.Status);
        Assert.Equal(4, group.SharedStep!.Rev);
        Assert.Equal(0, result.StepCount);
    }

    // System.TeamProject is server data. On the Test Case a blank or dot-segment value is a format
    // error; on a Shared Step it only drops that step's project and link. Neither may escape as an
    // ArgumentException or build a link outside the collection.
    [Theory]
    [InlineData("   ")]
    [InlineData("..")]
    public async Task UnusableTeamProjectIsAFormatErrorForTheCaseAndDropsASharedStepLink(string project)
    {
        JsonObject root = ExpansionFixture.Item(1, "<steps><compref ref=\"2\"/></steps>");
        root["fields"]!["System.TeamProject"] = project;
        using (FakeHttpMessageHandler handler = ExpansionFixture.Handler(root, ExpansionFixture.Item(2, ExpansionFixture.Xml("01-direct.xml"))))
        {
            AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => ExpansionFixture.Retrieve(handler));
            Assert.Equal("WorkItemsBatch", error.Operation);
        }
        JsonObject shared = ExpansionFixture.Item(2, ExpansionFixture.Xml("01-direct.xml"));
        shared["fields"]!["System.TeamProject"] = project;
        using FakeHttpMessageHandler sharedHandler = ExpansionFixture.Handler(ExpansionFixture.Item(1, "<steps><compref ref=\"2\"/></steps>"), shared);
        AdoTestCase result = await ExpansionFixture.Retrieve(sharedHandler);
        AdoSharedStepInfo info = Assert.Single(result.SharedSteps);
        Assert.Equal(2, info.Id);
        Assert.Null(info.TeamProject);
        Assert.Null(info.WebUrl);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/_workitems/edit/1", result.WebUrl.AbsoluteUri);
    }

    [Fact]
    public async Task MalformedSharedDocumentPreservesGroupAndFollowingRootStep()
    {
        using var handler = ExpansionFixture.Handler(ExpansionFixture.Item(1, "<steps><compref ref=\"2\"/><step type=\"ActionStep\"/></steps>"),
            ExpansionFixture.Item(2, ExpansionFixture.Xml("23-malformed-shared.xml.txt")));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal(Values1, result.Steps.Select(row => row.Number));
        Assert.Equal(2, Assert.Single(result.Diagnostics).WorkItemId);
        Assert.Equal(DiagnosticCodes.MalformedStepsXml, result.Diagnostics[0].Code);
        Assert.Equal(AdoTestCaseStatus.Partial, result.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalAndSharedParameterRowsRetainNamesValuesAndTokens(bool shared)
    {
        JsonObject root = ExpansionFixture.Item(1, "<steps><step type=\"ActionStep\"><parameterizedString>@user</parameterizedString></step></steps>");
        root["fields"]!["Microsoft.VSTS.TCM.Parameters"] = ParserFixture.Read("Parameters/01-names.xml");
        root["fields"]!["Microsoft.VSTS.TCM.LocalDataSource"] = shared ? "{\"user\":2,\"password\":2}" : ParserFixture.Read("Parameters/01-local.xml");
        JsonObject set = ExpansionFixture.Item(2, null);
        set["fields"]!["Microsoft.VSTS.TCM.Parameters"] = ParserFixture.Read("Parameters/02-shared-set.xml");
        using var handler = ExpansionFixture.Handler(root, set);
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal("@user", Assert.Single(result.Steps).Action);
        Assert.Equal(shared ? AdoParameterSource.Shared : AdoParameterSource.Local, result.Parameters.Source);
        Assert.Equal(Values2, result.Parameters.Names);
        Assert.NotEmpty(result.Parameters.Rows);
        Assert.Empty(result.Diagnostics);
        if (shared)
        {
            var info = Assert.Single(result.Parameters.SharedParameterSets);
            Assert.Equal(2, info.Id);
            Assert.Equal(3, info.Rev);
            Assert.NotNull(info.WebUrl);
            Assert.Equal(2, handler.Requests.Count);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrMalformedSharedParameterSetIsWarningAndStepsRemain(bool malformed)
    {
        JsonObject root = ExpansionFixture.Item(1, ExpansionFixture.Xml("01-direct.xml"));
        root["fields"]!["Microsoft.VSTS.TCM.LocalDataSource"] = ParserFixture.Read("Parameters/05-unresolved.json");
        JsonObject set = ExpansionFixture.Item(999, null);
        set["fields"]!["Microsoft.VSTS.TCM.Parameters"] = "broken";
        using var handler = ExpansionFixture.Handler(malformed ? new[] { root, set } : new[] { root });
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal(malformed ? DiagnosticCodes.MalformedParameterData : DiagnosticCodes.UnresolvedSharedParameter, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(AdoTestCaseStatus.Complete, result.Status);
        Assert.Single(result.Steps);
        Assert.Empty(result.Parameters.Rows);
        Assert.Equal(999, Assert.Single(result.Parameters.SharedParameterSets).Id);
    }

    [Fact]
    public async Task UnknownElementAndMalformedLocalDataRemainWarnings()
    {
        JsonObject root = ExpansionFixture.Item(1, "<steps><unknown/><step type=\"ActionStep\"/></steps>");
        root["fields"]!["Microsoft.VSTS.TCM.LocalDataSource"] = "{broken";
        using var handler = ExpansionFixture.Handler(root);
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal(new[] { DiagnosticCodes.UnknownStepElement, DiagnosticCodes.MalformedParameterData }, result.Diagnostics.Select(d => d.Code));
        Assert.Equal(AdoTestCaseStatus.Complete, result.Status);
    }
}
