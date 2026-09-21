using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.Tests.TestRuns;

// Approved audit regressions F01–F03 and F05–F08; all payloads are synthetic.
public sealed class WireAuditRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateResultIdsWithinOneRunAreRejected(bool separatePages)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(separatePages
            ? """{"value":[{"id":1},{"id":"2"}]}"""
            : """{"value":[{"id":1},{"id":"1"}]}"""));
        if (separatePages) handler.Enqueue(FakeHttpMessageHandler.Response("""{"value":[{"id":2},{"id":3}]}"""));
        handler.Enqueue(FakeHttpMessageHandler.Response(TestRunFixture.EmptyPage));
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() =>
            new TestRunService(client, TestRunFixture.Connection).GetResultsAsync(TestRunFixture.Project, 201,
                CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("TestResultsList", error.Operation);
    }

    [Fact]
    public async Task F01NestedAttemptsOrderBeforeClocksAndRespectParentResets()
    {
        const string body = """
            {"value":[
              {"id":4,"startedDate":"2026-01-01T01:00:00Z","pipelineReference":{"stageReference":{"attempt":2},"phaseReference":{"attempt":1},"jobReference":{"attempt":1}}},
              {"id":3,"startedDate":"2026-01-01T02:00:00Z","pipelineReference":{"stageReference":{"attempt":1},"phaseReference":{"attempt":2},"jobReference":{"attempt":1}}},
              {"id":2,"startedDate":"2026-01-01T03:00:00Z","pipelineReference":{"stageReference":{"attempt":"1"},"phaseReference":{"attempt":"1"},"jobReference":{"attempt":"2"}}},
              {"id":1,"startedDate":"2026-01-01T04:00:00Z","pipelineReference":{"stageReference":{"attempt":1},"phaseReference":{"attempt":1},"jobReference":{"attempt":1}}}
            ]}
            """;
        using FakeHttpMessageHandler handler = new TestRunFixture().RouteBody(body, "/test/runs", "%24skip=0&").Handler();
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTestRun> runs = await new TestRunService(client, TestRunFixture.Connection)
            .GetRunsAsync(TestRunFixture.Build(), CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal([1, 2, 3, 4], runs.Select(run => run.Id));
        Assert.Equal(2, runs[1].PipelineAttempt);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("\"2147483648\"")]
    public void F02LogCountsUseTheDocumentedInt64Range(string count)
    {
        BuildLogDto log = Assert.Single(JsonSerializer.Deserialize("{\"value\":[{\"id\":1,\"lineCount\":" + count + "}]}",
            AdoJsonContext.Default.BuildLogPageDto)!.Value!);
        Assert.Equal(2147483648L, (long?)log.LineCount);
    }

    [Theory]
    [InlineData(2147483648L)]
    [InlineData(long.MaxValue)]
    public async Task F02LargeLogTailKeepsInvariantInt64Bounds(long count)
    {
        using TestDirectory directory = new();
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"value\":[{\"id\":1,\"lineCount\":\""
            + count.ToString(CultureInfo.InvariantCulture) + "\"}]}"));
        handler.Enqueue(FakeHttpMessageHandler.Response("synthetic log", media: "text/plain"));
        using HttpClient client = new(handler);
        await new BuildLogService(client, TestRunFixture.Connection).SaveAsync(TestRunFixture.Project, 401, 1,
            Path.Combine(directory.Root, "tail.txt"), 200, CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);
        Assert.Equal("?startLine=" + (count - 200).ToString(CultureInfo.InvariantCulture) + "&endLine="
            + (count - 1).ToString(CultureInfo.InvariantCulture) + "&api-version=6.0", handler.Requests[1].Uri.Query);
    }

    [Theory]
    [InlineData("utf-8", true)]
    [InlineData("utf-16", false)]
    [InlineData("utf-16", true)]
    [InlineData("utf-16BE", false)]
    [InlineData("iso-8859-1", false)]
    public async Task F03ListsAndDetailsDecodeBomAndCharset(string charset, bool bom)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(Encoded("{\"value\":[{\"id\":1,\"name\":\"Équipe\"}]}", charset, bom));
        handler.Enqueue(FakeHttpMessageHandler.Response(TestRunFixture.EmptyPage));
        handler.Enqueue(Encoded("{\"id\":2,\"outcome\":\"Failed\",\"errorMessage\":\"Échec\"}", charset, bom));
        using HttpClient client = new(handler);
        TestRunService service = new(client, TestRunFixture.Connection);
        Assert.Equal("Équipe", Assert.Single(await service.GetRunsAsync(TestRunFixture.Build(),
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken)).Name);
        Assert.Equal("Échec", (await service.GetResultAsync(TestRunFixture.Project, 1, 2,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken)).ErrorMessage);
    }

    [Theory]
    [InlineData("utf-8", true)]
    [InlineData("utf-16", false)]
    public async Task F03ErrorMessagesDecodeWithoutChangingStatus(string charset, bool bom)
    {
        using HttpResponseMessage response = Encoded("{\"message\":\"Échec api-version\"}", charset, bom);
        response.StatusCode = HttpStatusCode.BadRequest;
        AdoException error = await ErrorTranslator.TranslateAsync(response, EndpointRegistry.TestRunsList,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("Échec api-version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task F03EncodedWiqlLimitErrorRetainsItsNarrowingHint()
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = Encoded("{\"message\":\"VS402337 Échec synthétique\"}", "utf-16", true);
        response.StatusCode = HttpStatusCode.BadRequest;
        handler.Enqueue(response);
        using HttpClient client = new(handler);
        AdoRequestException error = await Assert.ThrowsAsync<AdoRequestException>(() => new WiqlService(client, TestRunFixture.Connection)
            .QueryAsync(TestRunFixture.Project, "SELECT [System.Id] FROM WorkItems", null, CultureInfo.GetCultureInfo("fr-CA"),
                TestContext.Current.CancellationToken));
        Assert.Contains("VS402337", error.Message, StringComparison.Ordinal);
        Assert.Contains(Messages.Get(AdoMessage.WiqlLimitHint, CultureInfo.GetCultureInfo("fr-CA")), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-charset")]
    [InlineData("utf-8")]
    public async Task F03InvalidEncodingIsAFormatErrorAndDoesNotMaskHttpStatus(string charset)
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new ByteArrayContent([0xff, 0xff]) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = charset };
        using HttpResponseMessage errorResponse = new(HttpStatusCode.Forbidden) { Content = new ByteArrayContent([0xff, 0xff]) };
        errorResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = charset };
        handler.Enqueue(response);
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => new TestRunService(client, TestRunFixture.Connection)
            .GetRunsAsync(TestRunFixture.Build(), CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("TestRunsList", error.Operation);
        Assert.IsType<AdoAuthorizationException>(await ErrorTranslator.TranslateAsync(errorResponse, EndpointRegistry.TestRunsList,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task F05HistoryCannotIssueMoreRequestsThanItsBudget()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("{\"value\":[{\"id\":400,\"buildNumber\":\"earlier\"}]}", "/_apis/build/builds")
            .RouteBody("{\"value\":[{\"id\":2}]}", "/test/runs", "Build%2F400", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/test/runs")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/2/results").Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 2, MaximumHistoryRequests = 2 }, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        Assert.Equal(3, handler.Requests.Count); // one current request, then two history requests
        Assert.Contains(set.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.HistoryLimitExceeded);
        Assert.False(set.History[0].IsAvailable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task F05WindowPagesAndRetriesAlsoRespectTheBudget(bool retry)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(TestRunFixture.EmptyPage));
        HttpResponseMessage window = FakeHttpMessageHandler.Response("{\"value\":[{\"id\":400,\"buildNumber\":\"earlier\"}]}", retry ? 429 : 200);
        window.Headers.Add("x-ms-continuationtoken", "next");
        window.Headers.Add("Retry-After", "0");
        handler.Enqueue(window);
        // A bug issuing another request gets a valid terminal response, so assertions check the budget itself.
        handler.Fallback = (_, _) => Task.FromResult(FakeHttpMessageHandler.Response(TestRunFixture.EmptyPage));
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 3, MaximumHistoryRequests = 1 }, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(set.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.HistoryLimitExceeded);
        Assert.True(Assert.Single(set.History).IsCurrent);
    }

    [Fact]
    public async Task F06IgnoredUrlsCannotBreakRunsOrResultsOrBecomeAdditionalFields()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("{\"value\":[{\"id\":1,\"url\":\"http://[\"}]}", "/test/runs", "%24skip=0&")
            .RouteBody("{\"id\":2,\"url\":\"http://[\"}", "/Runs/1/results/2?").Handler();
        using HttpClient client = new(handler);
        TestRunService service = new(client, TestRunFixture.Connection);
        AdoTestRun run = Assert.Single(await service.GetRunsAsync(TestRunFixture.Build(), CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken));
        Assert.StartsWith("https://ado.example.test/", run.WebUrl!.AbsoluteUri, StringComparison.Ordinal);
        TestResultDto result = await service.GetResultAsync(TestRunFixture.Project, 1, 2, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain("url", TestAttemptMapper.FromResult(result, 1, 1, AdoTestAttemptSource.Single, null,
            TestContext.Current.CancellationToken).AdditionalFields.Keys);
    }

    [Fact]
    public void F07MissingCustomFieldValueIsSkippedAndExplicitNullIsKept()
    {
        TestResultDto result = JsonSerializer.Deserialize("""
            {"id":1,"customFields":[{"fieldName":"absent"},{"fieldName":"null","value":null},{"fieldName":"present","value":"é"}]}
            """, AdoJsonContext.Default.TestResultDto)!;
        AdoTestAttempt attempt = TestAttemptMapper.FromResult(result, 1, 1, AdoTestAttemptSource.Single, null,
            TestContext.Current.CancellationToken);
        Assert.False(attempt.CustomFields.ContainsKey("absent"));
        Assert.Null(attempt.CustomFields["null"]);
        Assert.Equal("é", attempt.CustomFields["present"]);
    }

    [Theory]
    [InlineData("42", "42")]
    [InlineData("\"42\"", "42")]
    [InlineData("2147483648", "2147483648")]
    [InlineData("1.5", "1.5")]
    [InlineData("null", null)]
    public void F08NumericTestCaseReferencesRemainTextForExistingValidation(string id, string? expected)
    {
        TestResultDto result = JsonSerializer.Deserialize("{\"id\":1,\"testCase\":{\"id\":" + id + "}}",
            AdoJsonContext.Default.TestResultDto)!;
        Assert.Equal(expected, result.TestCase!.Id);
        Assert.Equal(expected == "42", TestCaseLinkResolver.TryParseReference(result.TestCase.Id, out _));
    }

    private static HttpResponseMessage Encoded(string json, string charset, bool bom)
    {
        Encoding encoding = Encoding.GetEncoding(charset);
        byte[] bytes = bom ? [.. encoding.GetPreamble(), .. encoding.GetBytes(json)] : encoding.GetBytes(json);
        HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = charset };
        return response;
    }
}
