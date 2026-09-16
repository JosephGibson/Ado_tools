using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

internal static class ExpansionFixture
{
    private static readonly int[] Values1 = [1];
    internal static AdoConnection Connection { get; } = new() { CollectionUri = new Uri("https://ado.example.test/Collection"), DefaultProject = "Wrong" };
    internal static string Xml(string name) => ParserFixture.Read("Steps/" + name);
    internal static JsonObject Item(int id, string? xml, string type = "Cas personnalisé") => new()
    {
        ["id"] = id, ["rev"] = 3, ["fields"] = new JsonObject
        {
            ["System.Title"] = "Synthetic " + id.ToString(CultureInfo.InvariantCulture),
            ["System.WorkItemType"] = type, ["System.TeamProject"] = "Équipe / Web", ["System.State"] = "Ready",
            ["System.ChangedDate"] = "2026-09-15T12:00:00Z", ["Microsoft.VSTS.TCM.Steps"] = xml,
        },
    };
    internal static FakeHttpMessageHandler Handler(params JsonObject[] items)
    {
        Dictionary<int, JsonObject> byId = items.ToDictionary(item => item["id"]!.GetValue<int>());
        return new FakeHttpMessageHandler { Fallback = async (request, token) =>
        {
            if (request.Method == HttpMethod.Get)
                return FakeHttpMessageHandler.Fixture(request.RequestUri!.AbsolutePath.EndsWith("Microsoft.TestCaseCategory", StringComparison.Ordinal)
                    ? "test-category.json" : "shared-category.json");
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            JsonArray returned = [];
            foreach (JsonElement id in body.RootElement.GetProperty("ids").EnumerateArray().Reverse())
                if (byId.TryGetValue(id.GetInt32(), out JsonObject? item)) returned.Add(item.DeepClone());
            return FakeHttpMessageHandler.Response(new JsonObject { ["value"] = returned }.ToJsonString());
        } };
    }
    internal static async Task<AdoTestCase> Retrieve(FakeHttpMessageHandler handler, int depth = 10, int rows = 5000, int documents = 10000)
    {
        using HttpClient client = new(handler, disposeHandler: false);
        TestCaseResult result = await new TestCaseService(client, Connection, new()).GetTestCasesAsync(Values1,
            CultureInfo.CurrentCulture, TestContext.Current.CancellationToken, new()
            { MaximumSharedStepDepth = depth, MaximumExpandedSteps = rows, MaximumResolvedWorkItems = documents });
        return Assert.Single(result.TestCases);
    }
}
