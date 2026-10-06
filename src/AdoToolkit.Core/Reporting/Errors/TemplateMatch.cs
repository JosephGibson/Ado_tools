namespace AdoToolkit.Core.Reporting.Errors;

// What a catalog template recognized in an error. Identity holds the folded, tokenized texts that tell
// one error of the template from another, such as an expected value or a test author's message;
// value holes, such as an actual value, only show. A located match has no identity text, so the
// test's own frame tells its errors apart.
internal sealed class TemplateMatch
{
    // The template, the same for its English and French forms: their slots line up.
    internal required string Id { get; init; }
    internal required ErrorLanguage Language { get; init; }
    internal required IReadOnlyList<string> Identity { get; init; }
    internal required IReadOnlyList<ErrorPart> Parts { get; init; }
    internal bool IsLocated { get; init; }
}
