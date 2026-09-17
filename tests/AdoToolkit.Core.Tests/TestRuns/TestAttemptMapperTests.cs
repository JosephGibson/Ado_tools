using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-1")]
public sealed class TestAttemptMapperTests
{
    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(1e20)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidOptionalDurationsDoNotDiscardTheResultOrItsSubResults(double milliseconds)
    {
        TestSubResultDto sub = new() { Id = 2, Outcome = "Failed", DurationInMs = milliseconds };
        TestResultDto result = new()
        {
            Id = 1, Outcome = "Failed", DurationInMs = milliseconds,
            ErrorMessage = "Synthetic failure", SubResults = [sub],
        };
        AdoTestAttempt attempt = TestAttemptMapper.FromResult(result, 201, 1, AdoTestAttemptSource.Single,
            null, TestContext.Current.CancellationToken);
        Assert.Equal("Synthetic failure", attempt.ErrorMessage);
        Assert.Null(attempt.Duration);
        Assert.Null(Assert.Single(attempt.SubResults).Duration);
        Assert.Null(TestAttemptMapper.FromSubResult(sub, result, 201, 1, null, TestContext.Current.CancellationToken).Duration);
    }
}
