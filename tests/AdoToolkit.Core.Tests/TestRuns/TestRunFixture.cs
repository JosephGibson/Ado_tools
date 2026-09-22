using System.Net.Http;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// Routes fake responses by request path and query so a whole two-pass retrieval can be scripted
// from the synthetic TestRuns fixtures. Every unmatched request fails the test loudly.
internal sealed class TestRunFixture
{
    internal static readonly AdoConnection Connection = new()
    {
        CollectionUri = new Uri("https://ado.example.test/Collection"),
        DefaultProject = "Other",
    };
    internal const string Project = "Équipe Web";
    internal const string EmptyPage = "{\"count\":0,\"value\":[]}";
    private readonly List<FixtureRoute> routes = [];

    internal static string Read(string name) => ParserFixture.Read("TestRuns/" + name);

    internal static AdoBuild Build(int id = 401, string status = "completed", string? branch = "refs/heads/main",
        bool hasUri = true, bool finished = true) => new()
        {
            Id = id,
            BuildNumber = "20260915.1",
            Definition = new AdoBuildDefinitionRef { Id = 42, Name = "Tâches" },
            SourceBranch = branch,
            SourceVersion = new string('a', 40),
            Status = status,
            Result = "failed",
            QueueTime = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
            FinishTime = finished ? new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero) : null,
            Uri = hasUri ? new Uri("vstfs:///Build/Build/" + id.ToString(CultureInfo.InvariantCulture)) : null,
            TeamProject = Project,
            WebUrl = new Uri("https://ado.example.test/Collection/%C3%89quipe%20Web/_build/results?buildId="
                + id.ToString(CultureInfo.InvariantCulture)),
            CollectionUri = Connection.CollectionUri,
        };

    // Matches every fragment against the escaped path and query, in registration order.
    internal TestRunFixture Route(string fixture, params string[] fragments)
    {
        routes.Add(new FixtureRoute(fragments, Read(fixture), null));
        return this;
    }

    internal TestRunFixture RouteBody(string body, params string[] fragments)
    {
        routes.Add(new FixtureRoute(fragments, body, null));
        return this;
    }

    internal TestRunFixture RouteStatus(int status, string body, params string[] fragments)
    {
        routes.Add(new FixtureRoute(fragments, body, status));
        return this;
    }

    // Binary content, for example attachment files; register before any broader list route.
    internal TestRunFixture RouteBytes(byte[] body, params string[] fragments)
    {
        routes.Add(new FixtureRoute(fragments, string.Empty, null, body));
        return this;
    }

    // The Test Case and bug reads share the WorkItemsBatch URL, so these routes also match a
    // request body fragment: TestCaseBatch selects the Test Case read, BugBatch the bug read.
    // Register them before a broader "workitemsbatch" route.
    internal const string TestCaseBatch = "\"$expand\":\"relations\"";
    internal const string BugBatch = "\"fields\":";

    internal TestRunFixture RouteBatch(string bodyFragment, string body, int? status = null)
    {
        routes.Add(new FixtureRoute(["workitemsbatch"], body, status, BodyFragment: bodyFragment));
        return this;
    }

    // The bug lookup of result-detail-201-1.json: bug 2001 is Active (open) and 2002 Closed.
    internal TestRunFixture RouteBugs() => RouteBatch(BugBatch, Read("workitems-bugs.json"))
        .Route("workitemtype-states-bug.json", "/workitemtypes/Bug/states");

    internal FakeHttpMessageHandler Handler()
    {
        FakeHttpMessageHandler handler = new()
        {
            Fallback = async (request, token) =>
            {
                string target = request.RequestUri!.PathAndQuery;
                string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(token);
                foreach (FixtureRoute route in routes)
                    if (route.Fragments.All(fragment => target.Contains(fragment, StringComparison.Ordinal))
                        && (route.BodyFragment is null || body.Contains(route.BodyFragment, StringComparison.Ordinal)))
                        return route.Bytes is null ? FakeHttpMessageHandler.Response(route.Body, route.Status ?? 200)
                            : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(route.Bytes) };
                // TopSkip stops only on an empty page, so an unrouted later offset terminates it.
                if (target.Contains("%24skip=", StringComparison.Ordinal)
                    && !target.Contains("%24skip=0&", StringComparison.Ordinal))
                    return FakeHttpMessageHandler.Response(EmptyPage);
                throw new InvalidOperationException("No fixture route for " + target);
            },
        };
        return handler;
    }

    internal static TestFailureRetrievalService Service(HttpClient client, FakeClock? clock = null) =>
        new(client, Connection, null, clock ?? new FakeClock());

    private sealed record FixtureRoute(string[] Fragments, string Body, int? Status, byte[]? Bytes = null, string? BodyFragment = null);
}
