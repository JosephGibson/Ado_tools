using System.Net.Http;
using System.Text.Json.Nodes;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.Builds;

[Trait("Acceptance", "S4-1")]
public sealed class BuildFailureDeriverTests
{
    [Theory]
    [InlineData("multi-stage.json", "failed", false, "Deploy › Release › Prepare|Deploy › Release › Publish", "1,1", "11,12")]
    [InlineData("retried-job.json", "failed", false, "Build › Retry job › Retry task", "2", "12")]
    [InlineData("canceled.json", "canceled", false, "Deploy › Waiting|Deploy › Failed first", "1,1", ",11")]
    [InlineData("canceled.json", "failed", false, "Deploy › Failed first", "1", "11")]
    [InlineData("warnings.json", "partiallySucceeded", false, "", "", "")]
    [InlineData("warnings.json", "partiallySucceeded", true, "Build › Compile › Analyze", "1", "11")]
    [InlineData("no-log.json", "failed", false, "Build › Agent allocation", "1", "")]
    public async Task FiveDocumentedTimelinesHaveExpectedDeepestFailures(string fixture, string buildResult, bool warnings,
        string paths, string attempts, string logs)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Timeline(fixture)));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("build-logs.json"));
        using HttpClient client = new(handler);
        IReadOnlyList<AdoBuildFailure> failures = await new TimelineService(client, BuildQueryTests.Connection)
            .GetFailuresAsync(Build(buildResult), warnings, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(paths, string.Join('|', failures.Select(item => item.Path)));
        Assert.Equal(attempts, string.Join(',', failures.Select(item => item.Attempt.ToString(CultureInfo.InvariantCulture))));
        Assert.Equal(logs, string.Join(',', failures.Select(item => item.LogId?.ToString(CultureInfo.InvariantCulture))));
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/builds/401/timeline", handler.Requests[0].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/builds/401/logs", handler.Requests[1].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.All(failures, failure =>
        {
            Assert.Equal(401, failure.BuildId);
            Assert.Equal("20260915.1", failure.BuildNumber);
            Assert.Equal(42, failure.Definition.Id);
            Assert.Equal("refs/heads/main", failure.Branch);
            Assert.Equal(BuildQueryTests.Connection.CollectionUri, failure.CollectionUri);
            Assert.Equal(TimeSpan.Zero, failure.StartTime!.Value.Offset);
            Assert.Equal(failure.LogId switch { 11 => 1000, 12 => 0, _ => (int?)null }, failure.LogLineCount);
            Assert.All(failure.ErrorIssues, issue => Assert.Equal("error", issue.Type));
        });
        if (fixture == "multi-stage.json")
        {
            Assert.Equal("Task", failures[1].RecordType);
            Assert.Equal("Publish", failures[1].RecordName);
            Assert.Equal("Synthetic publish error.", Assert.Single(failures[1].ErrorIssues).Message);
            Assert.Equal("General", failures[1].ErrorIssues[0].Category);
            Assert.Equal(1, failures[1].ErrorCount);
        }
        if (fixture == "warnings.json" && warnings) Assert.Equal(1, Assert.Single(failures).WarningCount);
    }

    [Fact]
    public async Task TimelineEmitsParentBeforeChildrenByOrderAndRetainsAttemptMetadata()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Timeline("multi-stage.json")));
        handler.Enqueue(FakeHttpMessageHandler.Response(Timeline("retried-job.json")));
        using HttpClient client = new(handler);
        TimelineService service = new(client, BuildQueryTests.Connection);
        IReadOnlyList<AdoTimelineRecord> records = await service.GetTimelineAsync(BuildQueryTests.Project, 401, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        string[] expected = ["Build", "Compile phase", "Compile job", "Compile", "Deploy", "Release", "Prepare", "Publish"];
        Assert.Equal(expected, records.Select(item => item.Name));
        records = await service.GetTimelineAsync(BuildQueryTests.Project, 401, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(5, records.Count);
        AdoTimelineRecord retry = Assert.Single(records, item => item.Name == "Retry job");
        Assert.Equal("job", retry.Identifier);
        Assert.Equal(2, retry.Attempt);
        Assert.Equal(1, Assert.Single(retry.PreviousAttempts).Attempt);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), retry.PreviousAttempts[0].RecordId);
    }

    [Fact]
    public async Task TypeNamesDoNotDriveFailuresAndSuccessfulRetryDropsOldSubtree()
    {
        JsonNode timeline = JsonNode.Parse(Timeline("retried-job.json"))!;
        foreach (JsonNode? node in timeline["records"]!.AsArray())
        {
            node!["type"] = "FutureUnknownShape";
            if (node["attempt"]!.GetValue<int>() == 2) node["result"] = "succeeded";
        }
        // The stage succeeds after the retry; the stale child has a unique identifier.
        timeline["records"]![0]!["result"] = "succeeded";
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(timeline.ToJsonString()));
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTimelineRecord> records = await new TimelineService(client, BuildQueryTests.Connection)
            .GetTimelineAsync(BuildQueryTests.Project, 401, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Empty(BuildFailureDeriver.Derive(Build("succeeded"), records, new Dictionary<int, int?>(), false,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeepestMeansAnyDescendantEvenAcrossSuccessfulIntermediateNodes()
    {
        JsonNode timeline = JsonNode.Parse(Timeline("no-log.json"))!;
        JsonObject child = (JsonObject)timeline["records"]![1]!.DeepClone();
        child["id"] = "00000000-0000-0000-0000-000000000003";
        child["parentId"] = "00000000-0000-0000-0000-000000000002";
        child["name"] = "Deep failure";
        child["identifier"] = "deep";
        child["type"] = "Unknown";
        timeline["records"]![1]!["result"] = "succeeded";
        timeline["records"]!.AsArray().Add(child);
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(timeline.ToJsonString()));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("builds-empty.json"));
        using HttpClient client = new(handler);
        AdoBuildFailure failure = Assert.Single(await new TimelineService(client, BuildQueryTests.Connection)
            .GetFailuresAsync(Build("failed"), false, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("Build › Agent allocation › Deep failure", failure.Path);
        Assert.Equal("Unknown", failure.RecordType);
        Assert.Null(failure.LogId);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("orphan")]
    [InlineData("cycle")]
    [InlineData("empty-id")]
    [InlineData("null-issue")]
    public async Task MalformedTreesRaiseFormatErrors(string kind)
    {
        JsonNode timeline = JsonNode.Parse(Timeline("no-log.json"))!;
        JsonArray records = timeline["records"]!.AsArray();
        switch (kind)
        {
            case "duplicate": records.Add(records[0]!.DeepClone()); break;
            case "orphan": records[1]!["parentId"] = "00000000-0000-0000-0000-000000000099"; break;
            case "cycle": records[0]!["parentId"] = records[1]!["id"]!.DeepClone(); break;
            case "empty-id": records[0]!["id"] = Guid.Empty.ToString(); break;
            case "null-issue": records[1]!["issues"]!.AsArray().Add((JsonNode?)null); break;
        }
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(timeline.ToJsonString()));
        using HttpClient client = new(handler);
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => new TimelineService(client, BuildQueryTests.Connection)
            .GetTimelineAsync(BuildQueryTests.Project, 401, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForeignBuildIsRejectedBeforeHttp()
    {
        using FakeHttpMessageHandler handler = new();
        using HttpClient client = new(handler);
        AdoConnection foreign = new() { CollectionUri = new Uri("https://other.example.test/Collection") };
        await Assert.ThrowsAsync<AdoConnectionMismatchException>(() => new TimelineService(client, foreign)
            .GetFailuresAsync(Build("failed"), false, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("{\"value\":[{\"id\":11,\"lineCount\":-1}]}")]
    [InlineData("{\"value\":[{\"id\":11},{\"id\":11}]}")]
    [InlineData("{\"value\":[null]}")]
    [InlineData("not JSON")]
    public async Task InvalidLogListsFailInsteadOfReturningUnreliableCounts(string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Timeline("multi-stage.json")));
        handler.Enqueue(FakeHttpMessageHandler.Response(body));
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => new TimelineService(client, BuildQueryTests.Connection)
            .GetFailuresAsync(Build("failed"), false, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("BuildLogsList", error.Operation);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AbsentLogCountStaysNullAndCancellationDoesNotEmitFailures()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Timeline("multi-stage.json")));
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTimelineRecord> records = await new TimelineService(client, BuildQueryTests.Connection)
            .GetTimelineAsync(BuildQueryTests.Project, 401, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.All(BuildFailureDeriver.Derive(Build("failed"), records, new Dictionary<int, int?>(), false, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken), failure => Assert.Null(failure.LogLineCount));
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => BuildFailureDeriver.Derive(Build("failed"), records, new Dictionary<int, int?>(), false,
            CultureInfo.InvariantCulture, canceled.Token));
    }

    internal static string Timeline(string name) => File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Timelines", name));

    internal static AdoBuild Build(string result) => new()
    {
        Id = 401, BuildNumber = "20260915.1", Definition = new() { Id = 42, Name = "Tâches" }, SourceBranch = "refs/heads/main",
        Result = result, TeamProject = BuildQueryTests.Project, CollectionUri = BuildQueryTests.Connection.CollectionUri,
        WebUrl = new Uri("https://ado.example.test/Collection/ignored"),
    };
}
