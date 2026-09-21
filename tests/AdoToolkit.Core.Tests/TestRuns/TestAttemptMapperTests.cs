using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.Http;
using System.Text.Json;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-1")]
public sealed class TestAttemptMapperTests
{
    [Fact]
    public void RerunCustomFieldsOverrideParentFieldsWithoutLosingFallbackMetadata()
    {
        const string body = """
            {"id":"10","customFields":[{"fieldName":"AttemptId","value":1},{"fieldName":"IsTestResultFlaky","value":true}],
             "subResults":[{"id":"20","customFields":[{"fieldName":"AttemptId","value":2},{"fieldName":"empty","value":null},{"fieldName":"missing"}]},
                           {"id":21}]}
            """;
        TestResultDto parent = JsonSerializer.Deserialize(body, AdoJsonContext.Default.TestResultDto)!;
        AdoTestAttempt child = TestAttemptMapper.FromSubResult(parent.SubResults![0], parent, 1, 1, null, TestContext.Current.CancellationToken);
        Assert.Equal(2L, child.CustomFields["AttemptId"]);
        Assert.Equal(true, child.CustomFields["IsTestResultFlaky"]);
        Assert.Null(child.CustomFields["empty"]);
        Assert.False(child.CustomFields.ContainsKey("missing"));
        AdoTestAttempt fallback = TestAttemptMapper.FromSubResult(parent.SubResults[1], parent, 1, 2, null, TestContext.Current.CancellationToken);
        Assert.Equal(1L, fallback.CustomFields["AttemptId"]);
    }

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
