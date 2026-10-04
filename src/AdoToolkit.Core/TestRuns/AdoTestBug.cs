using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.TestRuns;

// An open bug of a reported test: associated with one of its test results, linked to its Test Case
// by any work item link, or both. Closed bugs are not reported. A bug that could not be read is
// kept with a null Title, State and WorkItemType.
public sealed class AdoTestBug
{
    public required int Id { get; init; }
    public string? Title { get; init; }
    public string? State { get; init; }
    public string? WorkItemType { get; init; }
    public string? TeamProject { get; init; }
    // Proposed, InProgress, Resolved, Completed or Removed as the server names it. Null when the
    // state categories could not be read, so IsOpen comes from the default state names.
    public string? StateCategory { get; init; }
    // True when the state is outside the Completed and Removed categories; null when the bug could
    // not be read. Never false in a retrieved set: a closed bug is left out.
    public bool? IsOpen { get; init; }
    public bool IsResolved { get; init; }
    // The day the bug was filed, as the server records it, in UTC; null when it could not be read.
    public DateTimeOffset? CreatedDate { get; init; }
    // Who the bug is assigned to; null when nobody is, or when it could not be read.
    public AdoIdentityRef? AssignedTo { get; init; }
    public bool IsAssociatedWithResult { get; init; }
    public bool IsLinkedToTestCase { get; init; }
    public required Uri WebUrl { get; init; }
}
