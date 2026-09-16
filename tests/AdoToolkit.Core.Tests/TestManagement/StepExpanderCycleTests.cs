using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-3")]
public sealed class StepExpanderCycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepetitionAndDiamondExpandEveryOccurrenceAndFetchOnce(bool diamond)
    {
        using var handler = ExpansionFixture.Handler(
            ExpansionFixture.Item(1, ExpansionFixture.Xml(diamond ? "13-diamond.xml" : "12-repeated.xml")),
            ExpansionFixture.Item(2, diamond ? "<steps><compref ref=\"4\"/></steps>" : ExpansionFixture.Xml("01-direct.xml")),
            ExpansionFixture.Item(3, "<steps><compref ref=\"4\"/></steps>"),
            ExpansionFixture.Item(4, ExpansionFixture.Xml("01-direct.xml")));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.StepCount);
        Assert.Equal(2, result.SharedSteps.Single(info => info.Id == (diamond ? 4 : 2)).ReferenceCount);
        Assert.All(result.Steps.Where(row => row.Kind == AdoTestStepKind.SharedStep), row => Assert.True(row.IsExpanded));
        Assert.Equal(diamond ? 3 : 2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SelfDirectAndIndirectCyclesCarryRootToOffendingId(int length)
    {
        using var handler = ExpansionFixture.Handler(
            ExpansionFixture.Item(1, ExpansionFixture.Xml(length == 1 ? "16-self-cycle.xml" : "10-shared.xml")),
            ExpansionFixture.Item(2, ExpansionFixture.Xml(length == 2 ? "14-direct-cycle.xml" : "15-indirect-cycle.xml")),
            ExpansionFixture.Item(3, ExpansionFixture.Xml("14-direct-cycle.xml")));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCodes.CircularSharedStepReference, diagnostic.Code);
        Assert.Equal(Enumerable.Range(1, length).Append(1), diagnostic.ReferenceChain);
        Assert.Equal(length, result.Steps.Count);
        Assert.False(result.Steps[^1].IsExpanded);
        Assert.Equal(length, handler.Requests.Count);
    }
}
