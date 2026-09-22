using System.Net;
using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

public sealed class HttpPipelineContractTests
{
    [Fact]
    [Trait("Acceptance", "S0-3")]
    public void HandlerUsesDefaultWindowsAuthenticationAndConservativeTransport()
    {
        using SocketsHttpHandler handler = AdoHttpHandlerFactory.CreateHandler();
        Assert.Same(CredentialCache.DefaultCredentials, handler.Credentials);
        Assert.True(handler.UseProxy);
        Assert.Null(handler.Proxy);
        Assert.Same(CredentialCache.DefaultCredentials, handler.DefaultProxyCredentials);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.Equal(TimeSpan.FromMinutes(15), handler.PooledConnectionLifetime);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
        using HttpClient client = AdoHttpHandlerFactory.CreateClient();
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    [InlineData(5_000_000)]
    public void RequestTimeoutOutsideTheSupportedRangeIsRejectedBeforeAnyRequest(int seconds)
    {
        using HttpClient client = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectService(client, new AdoConnection
        {
            CollectionUri = new Uri("https://ado.example.test/Collection"),
            RequestTimeoutSeconds = seconds,
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdoHttpPipeline(client,
            new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public void RegistryContainsOnlyUniqueExactServer2020Endpoints()
    {
        Assert.Equal(20, EndpointRegistry.All.Count);
        Assert.Equal(EndpointRegistry.All.Count, EndpointRegistry.All.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("6.0", EndpointRegistry.ProjectsList.ApiVersion);
        Assert.Equal(PagingStrategy.TopSkip, EndpointRegistry.ProjectsList.Paging);
        Assert.True(EndpointRegistry.ProjectsList.IsSafeToRetry);
        Assert.DoesNotContain(EndpointRegistry.All, item => item.ApiVersion.EndsWith("-preview", StringComparison.Ordinal));
        // Test-area routes and versions pinned in Slice 5 (§6.2).
        Assert.Equal("6.0", EndpointRegistry.TestRunsList.ApiVersion);
        Assert.Equal(PagingStrategy.TopSkip, EndpointRegistry.TestRunsList.Paging);
        Assert.Equal(PagingStrategy.TopSkip, EndpointRegistry.TestResultsList.Paging);
        Assert.Equal("6.0", EndpointRegistry.BuildGet.ApiVersion);
        Assert.Equal("{project}/_apis/build/builds/{buildId}", EndpointRegistry.BuildGet.RouteTemplate);
        Assert.Equal("{project}/_apis/test/runs", EndpointRegistry.TestRunsList.RouteTemplate);
        Assert.Equal("{project}/_apis/test/Runs/{runId}/results", EndpointRegistry.TestResultsList.RouteTemplate);
        Assert.Equal("{project}/_apis/test/Runs/{runId}/results/{resultId}", EndpointRegistry.TestResultGet.RouteTemplate);
        Assert.Equal(EndpointRegistry.TestResultAttachmentsList.RouteTemplate,
            EndpointRegistry.TestSubResultAttachmentsList.RouteTemplate);
        Assert.All(new[] { EndpointRegistry.TestResultAttachmentsList, EndpointRegistry.TestSubResultAttachmentsList,
            EndpointRegistry.TestResultAttachmentContent }, static item => Assert.Equal("6.0-preview.1", item.ApiVersion));
        Assert.Equal(TimeoutClass.Download, EndpointRegistry.TestResultAttachmentContent.Timeout);
        // Server 2020 documents the state list with its categories only as a preview version.
        Assert.Equal("{project}/_apis/wit/workitemtypes/{type}/states", EndpointRegistry.WorkItemTypeStates.RouteTemplate);
        Assert.Equal("6.0-preview.1", EndpointRegistry.WorkItemTypeStates.ApiVersion);
        Assert.Equal(TimeoutClass.Metadata, EndpointRegistry.WorkItemTypeStates.Timeout);
        Assert.Equal("application/octet-stream", EndpointRegistry.TestResultAttachmentContent.Accept);
        Assert.All(EndpointRegistry.All, static item => Assert.True(item.IsSafeToRetry));
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    [Trait("Acceptance", "S0-8")]
    public void EncodesEachRouteSegmentAndQueryValueOnceWithInvariantTransport()
    {
        EndpointDefinition endpoint = EndpointRegistry.ProjectsList with { RouteTemplate = "{project}/_apis/projects" };
        List<string> uris = [];
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach (string name in new[] { "en-US", "fr-CA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                using HttpRequestMessage request = RequestBuilder.Create(new Uri("https://ado.example.test/tfs/Collection"),
                    endpoint, CultureInfo.CurrentCulture, new Dictionary<string, string> { ["project"] = "Équipe / Web" },
                    new Dictionary<string, string> { ["continuationToken"] = "opaque +/%?&", ["$top"] = 123.ToString(CultureInfo.InvariantCulture) });
                Assert.Equal(HttpVersion.Version11, request.Version);
                Assert.Equal(HttpVersionPolicy.RequestVersionExact, request.VersionPolicy);
                Assert.Equal(name, request.Headers.AcceptLanguage.ToString());
                Assert.Contains("%C3%89quipe%20%2F%20Web", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
                Assert.Contains("opaque%20%2B%2F%25%3F%26", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
                Assert.Contains("api-version=6.0", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
                uris.Add(request.RequestUri.AbsoluteUri);
            }
            Assert.Equal(uris[0], uris[1]);
        }
        finally { CultureInfo.CurrentCulture = original; }
        Assert.Throws<ArgumentException>(() => RequestBuilder.Create(new Uri("https://ado.example.test/Collection"),
            endpoint, CultureInfo.InvariantCulture, new Dictionary<string, string> { ["project"] = "sample" },
            new Dictionary<string, string> { ["api-version"] = "7.0" }));
    }

    // Uri collapses "." and ".." segments even after escaping, so such a value would send an
    // authenticated request above the collection, for example to the server-level /tfs/_apis.
    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("  ")]
    public void RouteValuesThatAreEmptyOrDotSegmentsAreRejectedBeforeAnyRequest(string project)
    {
        Assert.Throws<ArgumentException>(() => RequestBuilder.Create(new Uri("https://ado.example.test/tfs/Collection"),
            EndpointRegistry.BuildGet, CultureInfo.InvariantCulture, new Dictionary<string, string> { ["project"] = project, ["buildId"] = "1" }));
        using HttpRequestMessage kept = RequestBuilder.Create(new Uri("https://ado.example.test/tfs/Collection"),
            EndpointRegistry.BuildGet, CultureInfo.InvariantCulture, new Dictionary<string, string> { ["project"] = "...", ["buildId"] = "1" });
        Assert.StartsWith("/tfs/Collection/.../_apis/", kept.RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task MapsProjectsAndOpaqueIdentitiesWithoutExposingJson()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("success.json"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("projects-empty.json"));
        using HttpClient client = new(handler);
        AdoConnection connection = new() { CollectionUri = new Uri("https://ado.example.test/Collection") };
        ProjectService service = new(client, connection);
        IReadOnlyList<AdoProject> projects = await service.GetProjectsAsync(CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);
        AdoProject project = Assert.Single(projects);
        Assert.Equal("Équipe Web", project.Name);
        Assert.Equal(connection.CollectionUri, project.CollectionUri);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), project.Id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("fr-CA", request.Language));
        Assert.Empty(client.DefaultRequestHeaders.AcceptLanguage);
        IdentityDto wire = JsonSerializer.Deserialize("""{"id":"opaque:sample","displayName":"Sample User","uniqueName":"sample"}""", AdoJsonContext.Default.IdentityDto)!;
        Assert.Equal("opaque:sample", wire.ToDomain().Id);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData("malformed-body.json.txt")]
    [InlineData("empty-body.txt")]
    public async Task MalformedOrEmptySuccessIsAResponseFormatErrorWithoutRetry(string fixture)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture(fixture));
        using HttpClient client = new(handler);
        ProjectService service = new(client, new AdoConnection { CollectionUri = new Uri("https://ado.example.test/Collection") });
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => service.GetProjectsAsync(CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData("{}")]
    [InlineData("{\"value\":[null]}")]
    [InlineData("{\"value\":[{\"id\":\"invalid\",\"name\":\"sample\"}]}")]
    public async Task InvalidSuccessShapesProduceTypedFormatErrors(string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(body));
        using HttpClient client = new(handler);
        ProjectService service = new(client, new AdoConnection { CollectionUri = new Uri("https://ado.example.test/Collection") });
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => service.GetProjectsAsync(CultureInfo.InvariantCulture, TestContext.Current.CancellationToken, top: 1));
        Assert.Single(handler.Requests);
    }
}
