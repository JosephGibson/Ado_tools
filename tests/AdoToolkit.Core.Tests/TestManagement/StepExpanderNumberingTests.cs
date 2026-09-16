using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-2")]
public sealed class StepExpanderNumberingTests
{
    private static readonly string[] Values1 = ["1", "2", "3", "3.1", "3.2", "3.3", "3.3.1", "4", "5"];
    private static readonly int[] Values2 = [2, 3];
    [Fact]
    public async Task NestedNumbersAndUnresolvedGroupKeepTheirPositionsAndProvenance()
    {
        using var handler = ExpansionFixture.Handler(
            ExpansionFixture.Item(1, ExpansionFixture.Xml("28-numbering.xml")),
            ExpansionFixture.Item(2, ExpansionFixture.Xml("28-child.xml")),
            ExpansionFixture.Item(3, ExpansionFixture.Xml("01-direct.xml")));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal(Values1, result.Steps.Select(row => row.Number));
        Assert.Equal(Enumerable.Range(1, 9), result.Steps.Select(row => row.Sequence));
        AdoTestStep nested = result.Steps[6];
        Assert.Equal(3, nested.SourceWorkItemId);
        Assert.Equal(3, nested.SourceRev);
        Assert.Equal(Values2, nested.SharedStepPath.Select(info => info.Id));
        Assert.Equal(2, nested.Depth);
        Assert.Equal(DiagnosticCodes.UnresolvedSharedStep, result.Steps[7].DiagnosticCode);
        Assert.False(result.Steps[7].IsExpanded);
        Assert.Equal(6, result.StepCount);
        Assert.Equal(AdoTestCaseStatus.Partial, result.Status);
        Assert.All(result.SharedSteps, info => Assert.Equal(1, info.ReferenceCount));
        AdoSharedStepInfo missing = result.SharedSteps.Single(info => info.Id == 99);
        Assert.Null(missing.Title);
        Assert.Null(missing.Rev);
        Assert.Null(missing.WebUrl);
        Assert.Null(result.Suite);
        Assert.Null(result.Priority);
        Assert.Null(result.AreaPath);
        Assert.Null(result.IterationPath);
        Assert.Null(result.AssignedTo);
        Assert.Null(result.ChangedBy);
        Assert.Null(result.AutomationStatus);
        Assert.Equal(ExpansionFixture.Connection.CollectionUri, result.CollectionUri);
        Assert.EndsWith("/%C3%89quipe%20%2F%20Web/_workitems/edit/1", result.WebUrl.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.Zero, result.RetrievedAt.Offset);
    }
}
