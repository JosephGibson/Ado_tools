namespace AdoToolkit.Core.TestManagement;

// A hyperlink of a Test Case, as text: a report links it only when its scheme is http, https or mailto.
public sealed class AdoTestCaseHyperlink
{
    public required string Url { get; init; }
    public string? Comment { get; init; }
}
