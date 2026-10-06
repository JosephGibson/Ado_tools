namespace AdoToolkit.Core.Reporting.Errors;

// One test's errors: each error it hit in a failed attempt, with the error the report places it
// under. The primary error is the most frequent error that is not generic; a tie goes to the error
// of the latest attempt. A test with generic errors only takes the most frequent of them.
internal sealed class ErrorProfile
{
    // The test's place in the list the classifier read.
    internal required int Test { get; init; }
    // Most frequent first, then the latest.
    internal required IReadOnlyList<ErrorProfileEntry> Entries { get; init; }
    // Null for a test without an error message.
    internal ErrorProfileEntry? Primary { get; init; }
    // Failed attempts, with or without a message: the m of "n of m failed attempts".
    internal required int FailedAttempts { get; init; }
    // Another error was as frequent, and the primary error won as the latest.
    internal bool IsTie { get; init; }
    // The errors other than the primary one, in the order of Entries.
    internal IEnumerable<ErrorProfileEntry> Others => Entries.Where(entry => !ReferenceEquals(entry, Primary));
}
