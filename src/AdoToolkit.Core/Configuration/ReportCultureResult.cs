namespace AdoToolkit.Core.Configuration;

public sealed class ReportCultureResult
{
    public required CultureInfo Culture { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
