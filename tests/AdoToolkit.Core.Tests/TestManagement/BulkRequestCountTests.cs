using System.Globalization;
using System.Net.Http;
using System.Text.Json.Nodes;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

// The 300-case suite is generated here and never committed (DD-024).
[Trait("Acceptance", "S3-4")]
public sealed class BulkRequestCountTests
{
    private const int CaseCount = 300;
    private const int PageSize = 50;
    private static readonly int[] BatchSizes = [200, 100, 10, 10];
    private static readonly int[] SuiteOrder = [814, 816, 818];
    private static readonly (int SuiteId, int Cases)[] Layout = [(814, 120), (816, 100), (818, 80)];
    private static readonly Lazy<BulkFixture> Generated = new(Generate);

    [Fact]
    public async Task ThreeHundredCasesSharingTwentySharedStepsOverTwoLevelsUseFourBatchRequests()
    {
        using FakeHttpMessageHandler handler = Generated.Value.Handler();
        using HttpClient client = new(handler);
        List<TestCaseMembership> memberships = [.. await new SuiteMembershipService(client, BulkFixture.Connection).GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 814, Recurse = true },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken)];
        TestCaseResult result = await new TestCaseService(client, BulkFixture.Connection, new())
            .GetTestCasesForMembershipsAsync(memberships, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

        RequestSnapshot[] batches = handler.Requests.Where(request => request.Method == "POST").ToArray();
        // At most 2 root batches + 1 per Shared Steps level; no category lookups.
        Assert.Equal(BatchSizes, batches.Select(batch => BulkFixture.BatchIds(batch).Length));
        Assert.Equal(20, batches.Skip(2).SelectMany(BulkFixture.BatchIds).Distinct().Count());
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath.Contains("workitemtypecategories", StringComparison.Ordinal));

        // Suite paging is counted separately: one plan listing, one tree, and each suite's membership pages.
        RequestSnapshot[] listings = handler.Requests.Where(request => request.Method == "GET").ToArray();
        Assert.Single(listings, request => request.Uri.AbsolutePath.EndsWith("/testplan/plans", StringComparison.Ordinal));
        Assert.Single(listings, request => request.Uri.AbsolutePath.EndsWith("/suites", StringComparison.Ordinal));
        Assert.Equal(3 + 2 + 2, listings.Count(request => request.Uri.AbsolutePath.EndsWith("/TestCase", StringComparison.Ordinal)));

        Assert.Equal(CaseCount, result.TestCases.Count);
        Assert.Equal(Enumerable.Range(10001, CaseCount), result.TestCases.Select(item => item.Id));
        Assert.Equal(SuiteOrder, result.TestCases.Select(item => item.Suite!.SuiteId).Distinct());
        Assert.All(result.TestCases, item =>
        {
            Assert.Equal(AdoTestCaseStatus.Complete, item.Status);
            // Two first-level groups, each with two expanded second-level groups of one step.
            Assert.Equal(10, item.Steps.Count);
            Assert.Equal(4, item.StepCount);
            Assert.Equal("2.2.1", item.Steps[^1].Number);
        });
    }

    private static BulkFixture Generate()
    {
        BulkFixture fixture = new();
        int id = 10001;
        foreach ((int suiteId, int cases) in Layout)
        {
            JsonObject[] entries = Enumerable.Range(0, cases).Select(index => BulkFixture.Entry(id + index, index + 1)).ToArray();
            foreach (JsonObject[] page in entries.Chunk(PageSize)) fixture.Page(suiteId, page);
            id += cases;
        }
        for (int index = 0; index < CaseCount; index++)
            fixture.Item(ExpansionFixture.Item(10001 + index, References(20001 + index % 10, 20001 + (index + 3) % 10)));
        for (int level = 0; level < 10; level++)
        {
            fixture.Item(ExpansionFixture.Item(20001 + level, References(20011 + level, 20011 + (level + 1) % 10), "Étape partagée"));
            fixture.Item(ExpansionFixture.Item(20011 + level, ExpansionFixture.Xml("01-direct.xml"), "Étape partagée"));
        }
        return fixture;
    }

    private static string References(int first, int second) => string.Create(CultureInfo.InvariantCulture,
        $"<steps><compref id=\"1\" ref=\"{first}\"/><compref id=\"2\" ref=\"{second}\"/></steps>");
}
