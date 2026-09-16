namespace AdoToolkit.Core.Reporting.Highlighting;

public sealed class CodeToken
{
    public required CodeTokenKind Kind { get; init; }
    public required string Text { get; init; }
    public bool IsFrameworkFrame { get; init; }
    public bool IsFirstUserFrame { get; init; }
}
