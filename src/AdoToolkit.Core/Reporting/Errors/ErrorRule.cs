namespace AdoToolkit.Core.Reporting.Errors;

// A rule that names errors: one of reporting.errorRules, or built in. Every line it matches belongs
// to one error under its name, in whatever language the line is; a generic rule also sets that error
// apart from the errors that say something about the test. A rule with context texts matches only
// a line that also holds one of them.
internal sealed class ErrorRule
{
    private readonly string[] context;

    internal ErrorRule(string name, IEnumerable<string> patterns, bool isGeneric, string? builtInId = null, IEnumerable<string>? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(patterns);
        Name = name;
        BuiltInId = builtInId;
        IsGeneric = isGeneric;
        Patterns = [.. patterns.Select(static pattern => new ErrorPattern(pattern))];
        this.context = [.. (context ?? []).Select(static text => FoldedText.Of(text).Text)];
    }

    // The name as configured, or the built-in ID.
    internal string Name { get; }
    // A built-in rule's invariant ID, such as ConnectionRefused; null for a configured rule.
    internal string? BuiltInId { get; }
    internal bool IsGeneric { get; }
    internal IReadOnlyList<ErrorPattern> Patterns { get; }

    // Whether a pattern matches one of the folded lines, and the line holds a context text if the
    // rule has any.
    internal bool Matches(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (string line in lines)
        {
            if (context.Length > 0 && !context.Any(text => line.Contains(text, StringComparison.Ordinal))) continue;
            if (Patterns.Any(pattern => pattern.IsMatch(line))) return true;
        }
        return false;
    }
}
