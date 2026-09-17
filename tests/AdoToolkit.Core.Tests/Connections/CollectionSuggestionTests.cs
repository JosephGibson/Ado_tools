using System.Net.Http;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.Connections;

// A project URL supplied as the collection URL: the parent is checked once for that project.
public sealed class CollectionSuggestionTests
{
    private const string Projects = """{"value":[{"id":"11111111-1111-1111-1111-111111111111","name":"Équipe Web","state":"wellFormed"}]}""";

    [Fact]
    public async Task ProjectSegmentFoundInParentIsSuggested()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Projects));
        handler.Enqueue(FakeHttpMessageHandler.Response("""{"count":0,"value":[]}"""));
        using HttpClient client = new(handler);
        CollectionUrlSuggestion? suggestion = await ProjectService.SuggestCollectionAsync(client,
            Connection("https://ado.example.test/tfs/Collection/%C3%A9quipe%20web"), CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        Assert.NotNull(suggestion);
        Assert.Equal("https://ado.example.test/tfs/Collection", suggestion.CollectionUri.AbsoluteUri);
        // The server's spelling of the project wins over the typed case.
        Assert.Equal("Équipe Web", suggestion.Project);
        Assert.StartsWith("https://ado.example.test/tfs/Collection/_apis/projects?", handler.Requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownProjectSegmentIsNotSuggested()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Projects));
        handler.Enqueue(FakeHttpMessageHandler.Response("""{"count":0,"value":[]}"""));
        using HttpClient client = new(handler);
        Assert.Null(await ProjectService.SuggestCollectionAsync(client, Connection("https://ado.example.test/Collection/Mobile"),
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailingParentCheckIsNotSuggested()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("not-found.json", 404));
        using HttpClient client = new(handler);
        Assert.Null(await ProjectService.SuggestCollectionAsync(client, Connection("https://ado.example.test/Collection/Web"),
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("https://ado.example.test/Collection")]
    [InlineData("https://ado.example.test")]
    public async Task SingleSegmentUrlsSendNoRequest(string url)
    {
        using FakeHttpMessageHandler handler = new();
        using HttpClient client = new(handler);
        Assert.Null(await ProjectService.SuggestCollectionAsync(client, Connection(url), CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    private static AdoConnection Connection(string url) => new() { CollectionUri = new Uri(url) };
}
