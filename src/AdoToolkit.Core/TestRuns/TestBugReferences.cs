namespace AdoToolkit.Core.TestRuns;

// The bug candidates of one reported test: the IDs associated with its results and the work items
// linked to its Test Case.
internal sealed record TestBugReferences(IReadOnlySet<int> Associated, IReadOnlySet<int> Linked);
