namespace AdoToolkit.Core.TestRuns;

// A bug of a reported test: associated with one of its test results, linked to its Test Case by
// any work item link, or both. Title, State and WorkItemType are null when it could not be read.
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
    // True unless the state is in the Completed or Removed category; null when the bug could not be read.
    public bool? IsOpen { get; init; }
    public bool IsResolved { get; init; }
    public bool IsAssociatedWithResult { get; init; }
    public bool IsLinkedToTestCase { get; init; }
    public required Uri WebUrl { get; init; }
}
