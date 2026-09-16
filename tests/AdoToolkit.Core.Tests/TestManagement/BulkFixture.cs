using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

// Routes synthetic testplan listings and work item batches by request path [V-04 shapes].
internal sealed class BulkFixture
{
    internal const string Project = "Équipe Web";
    internal static readonly AdoConnection Connection = new() { CollectionUri = new Uri("https://ado.example.test/Collection"), DefaultProject = "Wrong" };
    private readonly Dictionary<int, JsonObject> items = [];
    private readonly Dictionary<int, List<JsonArray>> pages = [];

    internal string PlansBody { get; set; } = ParserFixture.Read("Rest/testplans-first.json");
    internal string SuitesBody { get; set; } = MergedSuites();

    internal static string MergedSuites()
    {
        JsonObject first = JsonNode.Parse(ParserFixture.Read("Rest/testsuites-first.json"))!.AsObject();
        foreach (JsonNode? suite in JsonNode.Parse(ParserFixture.Read("Rest/testsuites-last.json"))!["value"]!.AsArray())
            first["value"]!.AsArray().Add(suite!.DeepClone());
        return first.ToJsonString();
    }

    internal static JsonObject Entry(int id, int order) => new() { ["workItem"] = new JsonObject { ["id"] = id }, ["order"] = order };

    // Adds one continuation page for a suite; several calls produce several pages.
    internal BulkFixture Page(int suiteId, params JsonObject[] entries)
    {
        if (!pages.TryGetValue(suiteId, out List<JsonArray>? list)) pages[suiteId] = list = [];
        list.Add([.. entries]);
        return this;
    }

    internal BulkFixture Item(JsonObject item)
    {
        items[item["id"]!.GetValue<int>()] = item;
        return this;
    }

    internal FakeHttpMessageHandler Handler() => new() { Fallback = async (request, token) =>
    {
        string path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
        if (request.Method == HttpMethod.Post)
        {
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            JsonArray returned = [];
            foreach (JsonElement id in body.RootElement.GetProperty("ids").EnumerateArray().Reverse())
                if (items.TryGetValue(id.GetInt32(), out JsonObject? item)) returned.Add(item.DeepClone());
            return FakeHttpMessageHandler.Response(new JsonObject { ["value"] = returned }.ToJsonString());
        }
        if (path.EndsWith("/_apis/testplan/plans", StringComparison.Ordinal)) return FakeHttpMessageHandler.Response(PlansBody);
        if (path.EndsWith("/suites", StringComparison.Ordinal)) return FakeHttpMessageHandler.Response(SuitesBody);
        string[] segments = path.Split('/');
        int suiteId = int.Parse(segments[^2], CultureInfo.InvariantCulture);
        List<JsonArray> suitePages = pages.GetValueOrDefault(suiteId) ?? [[]];
        string? continuation = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["continuationToken"];
        int index = continuation is null ? 0 : int.Parse(continuation, CultureInfo.InvariantCulture);
        HttpResponseMessage response = FakeHttpMessageHandler.Response(new JsonObject { ["value"] = suitePages[index].DeepClone() }.ToJsonString());
        if (index + 1 < suitePages.Count)
            response.Headers.TryAddWithoutValidation("x-ms-continuationtoken", (index + 1).ToString(CultureInfo.InvariantCulture));
        return response;
    } };

    internal static int[] BatchIds(RequestSnapshot request)
    {
        using JsonDocument body = JsonDocument.Parse(request.Body!);
        return body.RootElement.GetProperty("ids").EnumerateArray().Select(id => id.GetInt32()).ToArray();
    }
}
