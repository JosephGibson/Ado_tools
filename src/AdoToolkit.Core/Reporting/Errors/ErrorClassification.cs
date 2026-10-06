using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.Errors;

// The errors of a report: each test's profile, in the order of the list the classifier read, and
// every error in the order it first appears.
internal sealed class ErrorClassification
{
    internal required IReadOnlyList<ErrorProfile> Profiles { get; init; }
    internal required IReadOnlyList<ErrorClass> Classes { get; init; }
    // The list the classifier read, which names the test of a pairing even when the report does not
    // show it.
    internal IReadOnlyList<AdoTestFailure> Tests { get; init; } = [];
    // Each test's primary error against its previous failed build, in the order of Profiles; null
    // when nothing can be said.
    internal IReadOnlyList<ErrorComparison?> Comparisons { get; init; } = [];

    internal static ErrorClassification Empty { get; } = new() { Profiles = [], Classes = [] };
}
