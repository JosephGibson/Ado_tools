using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-4")]
public sealed class StepExpanderLimitTests
{
    private static readonly int[] Values1 = [1, 2, 3, 4];
    [Fact]
    public async Task DepthOverflowRetainsGroupAndChecksDepthBeforeMissingCache()
    {
        using var handler = ExpansionFixture.Handler(
            ExpansionFixture.Item(1, ExpansionFixture.Xml("10-shared.xml")),
            ExpansionFixture.Item(2, ExpansionFixture.Xml("11-nested.xml")),
            ExpansionFixture.Item(3, ExpansionFixture.Xml("24-depth.xml")));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler, depth: 2);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal(DiagnosticCodes.MaximumDepthExceeded, result.Steps[^1].DiagnosticCode);
        Assert.Equal(Values1, Assert.Single(result.Diagnostics).ReferenceChain);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GeneratedExponentialFanOutStopsGloballyAfterExactlyOneTruncation()
    {
        var items = Enumerable.Range(1, 11).Select(id => ExpansionFixture.Item(id, id == 11 ? ExpansionFixture.Xml("01-direct.xml")
            : FormattableString.Invariant($"<steps><compref ref=\"{id + 1}\"/><compref ref=\"{id + 1}\"/></steps>"))).ToArray();
        using var handler = ExpansionFixture.Handler(items);
        AdoTestCase result = await ExpansionFixture.Retrieve(handler, rows: 30);
        // §10.5.2 checks after recursion: rows 31–33 are groups, row 34 is their leaf, then the marker.
        Assert.Equal(35, result.Steps.Count);
        Assert.Equal(AdoTestStepKind.Truncated, result.Steps[^1].Kind);
        Assert.Single(result.Steps, row => row.Kind == AdoTestStepKind.Truncated);
        Assert.Single(result.Diagnostics, d => d.Code == DiagnosticCodes.ExpansionLimitExceeded);
        Assert.Equal(Enumerable.Range(1, 35), result.Steps.Select(row => row.Sequence));
        foreach (var info in result.SharedSteps)
            Assert.Equal(result.Steps.Count(row => row.SharedStep?.Id == info.Id), info.ReferenceCount);
    }

    [Fact]
    public async Task ResolutionBudgetIsSharedAcrossRoundsAndMissingIdsConsumeBudget()
    {
        using var handler = ExpansionFixture.Handler(ExpansionFixture.Item(1, "<steps><compref ref=\"99\"/><compref ref=\"2\"/></steps>"),
            ExpansionFixture.Item(2, ExpansionFixture.Xml("01-direct.xml")));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler, documents: 1);
        Assert.Equal(new[] { DiagnosticCodes.UnresolvedSharedStep, DiagnosticCodes.ResolutionLimitExceeded }, result.Steps.Select(row => row.DiagnosticCode));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ExactLimitDoesNotTruncateButNextRowIsRetainedBeforeMarker()
    {
        using var exact = ExpansionFixture.Handler(ExpansionFixture.Item(1, ExpansionFixture.Xml("02-multiple.xml")));
        Assert.DoesNotContain((await ExpansionFixture.Retrieve(exact, rows: 2)).Steps, row => row.Kind == AdoTestStepKind.Truncated);
        using var over = ExpansionFixture.Handler(ExpansionFixture.Item(1, ExpansionFixture.Xml("02-multiple.xml")));
        Assert.Equal(new[] { AdoTestStepKind.Action, AdoTestStepKind.Validate, AdoTestStepKind.Truncated },
            (await ExpansionFixture.Retrieve(over, rows: 1)).Steps.Select(row => row.Kind));
    }
}
