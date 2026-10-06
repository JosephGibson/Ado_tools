namespace AdoToolkit.Core.Configuration;

public sealed class ReportingOptions
{
    public string? Culture { get; init; }
    // Tried in this order, before the built-in rules, by the failed-test report.
    public IReadOnlyList<ErrorRuleOptions> ErrorRules { get; init; } = [];
}
