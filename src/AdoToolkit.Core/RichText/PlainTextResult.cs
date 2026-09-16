namespace AdoToolkit.Core.RichText;

public sealed class PlainTextResult
{
    public required string Text { get; init; }
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
}
