using System.Net.Http;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-2")]
public sealed class TestCapabilityDetectorTests
{
    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(200)]
    public async Task FailedLookupSurfacesTranslatedErrorAndIsNeverCached(int status)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("test-category.json"));
        handler.Enqueue(FakeHttpMessageHandler.Response("{}", status));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("test-category.json"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("shared-category.json"));
        using HttpClient client = new(handler);
        TestCapabilityDetector detector = new(client, ExpansionFixture.Connection, new());
        Exception? error = await Record.ExceptionAsync(() => detector.IsStepContainerAsync(false, "Équipe / Web", "Cas personnalisé",
            CultureInfo.CurrentCulture, TestContext.Current.CancellationToken));
        Assert.IsAssignableFrom<AdoException>(error);
        if (status == 403) Assert.IsType<AdoAuthorizationException>(error);
        else if (status == 404) Assert.IsType<AdoNotFoundException>(error);
        else Assert.IsType<AdoResponseFormatException>(error);
        Assert.True(await detector.IsStepContainerAsync(false, "Équipe / Web", "Cas personnalisé",
            CultureInfo.CurrentCulture, TestContext.Current.CancellationToken));
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task PresentNullStepsOnArbitraryTypeBypassCategoriesAndYieldEmptySteps()
    {
        using var handler = ExpansionFixture.Handler(ExpansionFixture.Item(1, null, "Custom type"));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal(DiagnosticCodes.EmptySteps, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(AdoTestCaseStatus.Complete, result.Status);
        Assert.Single(handler.Requests);
    }
}
