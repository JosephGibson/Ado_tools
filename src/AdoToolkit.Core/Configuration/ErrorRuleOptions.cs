namespace AdoToolkit.Core.Configuration;

// One rule of reporting.errorRules: wildcard patterns that put the errors they match in one class,
// under the rule's name. A generic rule's class also goes under Generic errors.
public sealed class ErrorRuleOptions
{
    public const int MaximumRules = 100;
    public const int MaximumNameLength = 100;
    public const int MaximumPatterns = 20;
    public const int MaximumPatternLength = 500;

    public required string Name { get; init; }
    public IReadOnlyList<string> Patterns { get; init; } = [];
    public bool Generic { get; init; } = true;
}
