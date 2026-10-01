using AdoToolkit.Core.Connections;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.TestManagement;

// One place where a Test Case is planned: a suite of a plan with one configuration, and the
// outcome of its latest run there.
public sealed class AdoTestPoint
{
    public required int Id { get; init; }
    public required int PlanId { get; init; }
    public string? PlanName { get; init; }
    public required int SuiteId { get; init; }
    public string? SuiteName { get; init; }
    public string? ConfigurationName { get; init; }
    // The server's outcome text, such as Passed, Failed or Blocked; null when the point was never run.
    public string? Outcome { get; init; }
    public AdoTestOutcomeClass OutcomeClass { get; init; }
    public bool HasRun { get; init; }
    public string? State { get; init; }
    public AdoIdentityRef? Tester { get; init; }
    public int? LastRunId { get; init; }
    public int? LastResultId { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
    public required string TeamProject { get; init; }
    public required Uri PlanWebUrl { get; init; }
    public required Uri SuiteWebUrl { get; init; }
    public Uri? LastRunWebUrl { get; init; }
}
