namespace AdoToolkit.Core.Reporting.Errors;

// One form of an error: what a failed attempt's message says, read through the rules, the catalog,
// the default messages of the runtime and, when none of them knows it, its tokenized key line.
// Attempts with equal keys have one form. Forms of one layout number their slots alike, so a value
// that differs between them, such as an actual value, lines up in the report.
internal sealed class ErrorSignature
{
    internal required string Key { get; init; }
    // The key without the test's own frame, for a message that comes without its trace.
    internal required string FramelessKey { get; init; }
    internal required string Layout { get; init; }
    // The key line, and for a template of several lines its identifying lines, as the server sent them.
    internal required IReadOnlyList<ErrorPart> Parts { get; init; }
    internal string? TemplateId { get; init; }
    // English or French when the template writes both languages, otherwise Neutral.
    internal ErrorLanguage Language { get; init; }
    internal ErrorRule? Rule { get; init; }
    // The key holds the test's own frame: a located template, or a default message of the runtime.
    internal bool IsLocated { get; init; }
    // The leading "Type:" of the key line and its length with the space after it, or null and 0.
    internal string? PrefixType { get; init; }
    internal int PrefixLength { get; init; }

    internal string Line => string.Concat(Parts.Select(static part => part.Text));
}
