using System.Text.Json;
using System.Text.Json.Serialization;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.TestManagement;

// The test points query and its answer [Verify V-32]. Every response field is optional. Reference
// IDs are read as text because the test area sends them as strings, and the tester and the date
// are read as raw values so that an unexpected shape drops one value, not the whole page.
internal sealed class TestPointsQueryDto
{
    public required TestPointsFilterDto PointsFilter { get; init; }
}

internal sealed class TestPointsFilterDto
{
    public required int[] TestcaseIds { get; init; }
}

internal sealed class TestPointsPageDto { public List<TestPointDto>? Points { get; init; } }

internal sealed class TestPointDto
{
    public int Id { get; init; }
    // Read only for the plan and suite IDs in its path; never requested.
    public string? Url { get; init; }
    public string? Outcome { get; init; }
    public string? State { get; init; }
    public JsonElement LastUpdatedDate { get; init; }
    public JsonElement AssignedTo { get; init; }
    public TestPointReferenceDto? Configuration { get; init; }
    public TestPointReferenceDto? Suite { get; init; }
    public TestPointReferenceDto? TestPlan { get; init; }
    public TestPointReferenceDto? TestCase { get; init; }
    public TestPointReferenceDto? LastTestRun { get; init; }
    public TestPointReferenceDto? LastResult { get; init; }
}

internal sealed class TestPointReferenceDto
{
    [JsonConverter(typeof(ReferenceIdConverter))]
    public string? Id { get; init; }
    public string? Name { get; init; }
}
