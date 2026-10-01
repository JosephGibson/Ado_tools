namespace AdoToolkit.Core.TestManagement;

// A work item linked to a Test Case. Title, State and WorkItemType are null when it could not be read.
public sealed class AdoLinkedWorkItem
{
    public required int Id { get; init; }
    // The reference name of the link type, such as Microsoft.VSTS.Common.TestedBy-Reverse.
    public required string LinkType { get; init; }
    // The name of the link as seen from the Test Case, such as Tests; null when the server sent none.
    public string? LinkName { get; init; }
    public string? Comment { get; init; }
    public string? Title { get; init; }
    public string? State { get; init; }
    public string? WorkItemType { get; init; }
    public string? TeamProject { get; init; }
    public bool IsResolved { get; init; }
    public required Uri WebUrl { get; init; }
}
