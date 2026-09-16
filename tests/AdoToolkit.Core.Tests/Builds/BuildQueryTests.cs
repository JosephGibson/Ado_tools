using System.Net.Http;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.Builds;

[Trait("Acceptance", "S4-2")]
public sealed class BuildQueryTests
{
    internal static readonly AdoConnection Connection = new() { CollectionUri = new Uri("https://ado.example.test/Collection"), DefaultProject = "Other" };
    internal const string Project = "Équipe Web";
    private static readonly int[] BuildIds = [401, 402];
    private static readonly int[] DefinitionIds = [42, 43, 44];

    [Fact]
    public async Task LatestFailedMainHasExactlyTheRequiredParametersAndMapsTheCompleteBuild()
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = FakeHttpMessageHandler.Fixture("builds-first.json");
        response.Headers.Add("x-ms-continuationtoken", "unused");
        handler.Enqueue(response);
        using HttpClient client = new(handler);
        AdoBuild build = Assert.Single(await new BuildService(client, Connection).GetBuildsAsync(Project,
            new BuildQuery { DefinitionId = 42, Branch = "main", Latest = true, Result = BuildResult.Failed },
            CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken));
        RequestSnapshot request = Assert.Single(handler.Requests);
        Assert.Equal("/Collection/%C3%89quipe%20Web/_apis/build/builds", request.Uri.AbsolutePath);
        string[] expected = ["definitions=42", "branchName=refs/heads/main", "resultFilter=failed", "statusFilter=completed",
            "queryOrder=finishTimeDescending", "$top=1", "api-version=6.0"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), Parameters(request.Uri).Order(StringComparer.Ordinal));
        Assert.Equal(401, build.Id);
        Assert.Equal("20260915.1", build.BuildNumber);
        Assert.Equal(42, build.Definition.Id);
        Assert.Equal("Tâches", build.Definition.Name);
        Assert.Equal("refs/heads/main", build.SourceBranch);
        Assert.Equal(new string('a', 40), build.SourceVersion);
        Assert.Equal("TfsGit", build.RepositoryType);
        Assert.Equal("00000000-0000-0000-0000-000000000100", build.RepositoryId);
        Assert.Equal("completed", build.Status);
        Assert.Equal("failed", build.Result);
        Assert.Equal("manual", build.Reason);
        Assert.Equal("Synthetic User", build.RequestedFor!.DisplayName);
        Assert.Equal("user@example.test", build.RequestedFor.UniqueName);
        Assert.Equal(TimeSpan.Zero, build.FinishTime!.Value.Offset);
        Assert.Equal(8, build.StartTime!.Value.Hour);
        Assert.Equal("vstfs:///Build/Build/401", build.Uri!.AbsoluteUri);
        Assert.Equal(Project, build.TeamProject);
        Assert.Equal(Connection.CollectionUri, build.CollectionUri);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_build/results?buildId=401", build.WebUrl.AbsoluteUri);
    }

    [Theory]
    [InlineData("main", "refs/heads/main")]
    [InlineData("feature/écran", "refs/heads/feature/écran")]
    [InlineData("refs/heads/main", "refs/heads/main")]
    [InlineData("refs/tags/v1", "refs/tags/v1")]
    [InlineData("refs/pull/12/merge", "refs/pull/12/merge")]
    public void BranchNormalizationPreservesQualifiedRefs(string branch, string expected) =>
        Assert.Equal(expected, new BuildQuery { Branch = branch }.Parameters(42)["branchName"]);

    [Fact]
    public void StatusDefaultsAndExplicitOverrides()
    {
        Assert.Equal("all", new BuildQuery().Parameters(42)["statusFilter"]);
        Assert.Equal("completed", new BuildQuery { Latest = true }.Parameters(42)["statusFilter"]);
        Assert.Equal("inProgress", new BuildQuery { Latest = true, Status = BuildStatus.InProgress }.Parameters(42)["statusFilter"]);
        Assert.Equal("all", new BuildQuery { Latest = true, Status = BuildStatus.All }.Parameters(42)["statusFilter"]);
        Assert.Equal("partiallySucceeded", new BuildQuery { Result = BuildResult.PartiallySucceeded }.Parameters(42)["resultFilter"]);
        Assert.False(new BuildQuery { Top = 2 }.Parameters(42).ContainsKey("$top"));
    }

    [Fact]
    public async Task ExactNameResolutionNormalizesAccentsThenRequestsById()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("build-definitions-first.json"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("builds-first.json"));
        using HttpClient client = new(handler);
        await new BuildService(client, Connection).GetBuildsAsync(Project, new BuildQuery { DefinitionName = "TA\u0302CHES" },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/_apis/build/definitions", handler.Requests[0].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("definitions=42", Parameters(handler.Requests[1].Uri));
    }

    [Theory]
    [InlineData("Missing", "0")]
    [InlineData("Tâches", "2")]
    public async Task NonUniqueNamesReportCountAndIdGuidanceWithoutBuildRequest(string name, string count)
    {
        using FakeHttpMessageHandler handler = Pages("build-definitions-first.json", "build-definitions-last.json");
        using HttpClient client = new(handler);
        AdoRequestException error = await Assert.ThrowsAsync<AdoRequestException>(() => new BuildService(client, Connection)
            .GetBuildsAsync(Project, new BuildQuery { DefinitionName = name }, CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken));
        Assert.Contains(count + " définitions", error.Message, StringComparison.Ordinal);
        Assert.Contains("-Definition <id>", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task BuildAndDefinitionPagingContinueThroughEmptyPagesAndEncodeOpaqueTokensOnce()
    {
        using FakeHttpMessageHandler handler = Pages("builds-first.json", "builds-last.json");
        using HttpClient client = new(handler);
        IReadOnlyList<AdoBuild> builds = await new BuildService(client, Connection).GetBuildsAsync(Project,
            new BuildQuery { DefinitionId = 42, Branch = "main", Top = 2 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(BuildIds, builds.Select(item => item.Id));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("continuationToken=page%202%2B%2F%3D", handler.Requests[1].Uri.Query, StringComparison.Ordinal);
        Assert.All(handler.Requests, request => Assert.Contains("definitions=42", Parameters(request.Uri)));
        using FakeHttpMessageHandler definitions = Pages("build-definitions-first.json", "build-definitions-last.json");
        using HttpClient definitionClient = new(definitions);
        IReadOnlyList<AdoBuildDefinition> result = await new BuildDefinitionService(definitionClient, Connection)
            .GetDefinitionsAsync(Project, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(DefinitionIds, result.Select(item => item.Id));
        Assert.Equal("\\Main", result[0].Path);
        Assert.Equal(3, result[0].Revision);
        Assert.Equal("enabled", result[0].QueueStatus);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_build?definitionId=42", result[0].WebUrl.AbsoluteUri);
    }

    [Fact]
    public async Task RepeatedBuildTokenFails()
    {
        using FakeHttpMessageHandler handler = new();
        for (int i = 0; i < 2; i++)
        {
            HttpResponseMessage response = FakeHttpMessageHandler.Fixture("builds-empty.json");
            response.Headers.Add("x-ms-continuationtoken", "same");
            handler.Enqueue(response);
        }
        using HttpClient client = new(handler);
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => new BuildService(client, Connection).GetBuildsAsync(Project,
            new BuildQuery { DefinitionId = 42 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void RegistryUsesBuildAreaSixAndJson()
    {
        foreach (EndpointDefinition endpoint in new[] { EndpointRegistry.BuildDefinitionsList, EndpointRegistry.BuildsList,
            EndpointRegistry.BuildTimeline, EndpointRegistry.BuildLogsList })
        {
            Assert.Equal("6.0", endpoint.ApiVersion);
            Assert.Equal(HttpMethod.Get, endpoint.Method);
            Assert.True(endpoint.IsSafeToRetry);
            using HttpRequestMessage request = RequestBuilder.Create(Connection.CollectionUri, endpoint, CultureInfo.InvariantCulture,
                new Dictionary<string, string> { ["project"] = Project, ["buildId"] = "401" });
            Assert.Equal("application/json", Assert.Single(request.Headers.Accept).MediaType);
        }
        Assert.Equal(PagingStrategy.ContinuationHeader, EndpointRegistry.BuildDefinitionsList.Paging);
        Assert.Equal(PagingStrategy.ContinuationHeader, EndpointRegistry.BuildsList.Paging);
        Assert.Equal(PagingStrategy.None, EndpointRegistry.BuildTimeline.Paging);
        Assert.Equal(PagingStrategy.None, EndpointRegistry.BuildLogsList.Paging);
    }

    [Theory]
    [InlineData(false, "{}")]
    [InlineData(false, "{\"value\":[null]}")]
    [InlineData(false, "{\"value\":[{\"id\":401,\"buildNumber\":\"B\"}]}")]
    [InlineData(false, "{\"value\":[{\"id\":401,\"buildNumber\":\"B\",\"definition\":{\"id\":0,\"name\":\"D\"}}]}")]
    [InlineData(true, "{}")]
    [InlineData(true, "{\"value\":[null]}")]
    [InlineData(true, "{\"value\":[{\"id\":42}]}")]
    [InlineData(true, "{\"value\":[{\"id\":42,\"name\":\"D\"},{\"id\":42,\"name\":\"D\"}]}")]
    public async Task MalformedListingsFailWithoutEmittingPartialModels(bool definitions, string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(body));
        using HttpClient client = new(handler);
        if (definitions)
            await Assert.ThrowsAsync<AdoResponseFormatException>(() => new BuildDefinitionService(client, Connection)
                .GetDefinitionsAsync(Project, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        else
            await Assert.ThrowsAsync<AdoResponseFormatException>(() => new BuildService(client, Connection)
                .GetBuildsAsync(Project, new BuildQuery { DefinitionId = 42 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TopStopsAfterFirstPageWithoutChangingPageSizeOrFetchingTheToken()
    {
        using FakeHttpMessageHandler handler = Pages("builds-first.json", "builds-last.json");
        using HttpClient client = new(handler);
        Assert.Single(await new BuildService(client, Connection).GetBuildsAsync(Project, new BuildQuery { DefinitionId = 42, Top = 1 },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Single(handler.Requests);
        Assert.DoesNotContain(Parameters(handler.Requests[0].Uri), item => item.StartsWith("$top=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingBuildByIdIsNotFound()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("builds-empty.json"));
        using HttpClient client = new(handler);
        await Assert.ThrowsAsync<AdoNotFoundException>(() => new BuildService(client, Connection)
            .GetByIdAsync(Project, 401, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        string[] expected = ["api-version=6.0", "buildIds=401"];
        Assert.Equal(expected, Parameters(Assert.Single(handler.Requests).Uri).Order(StringComparer.Ordinal));
    }

    internal static FakeHttpMessageHandler Pages(string first, string last)
    {
        FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = FakeHttpMessageHandler.Fixture(first);
        response.Headers.Add("x-ms-continuationtoken", "page 2+/=");
        handler.Enqueue(response);
        response = FakeHttpMessageHandler.Fixture("builds-empty.json");
        response.Headers.Add("x-ms-continuationtoken", "last");
        handler.Enqueue(response);
        handler.Enqueue(FakeHttpMessageHandler.Fixture(last));
        return handler;
    }

    private static IEnumerable<string> Parameters(Uri uri) => uri.Query.TrimStart('?').Split('&').Select(Uri.UnescapeDataString);
}
