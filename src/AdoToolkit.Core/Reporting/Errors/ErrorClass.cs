namespace AdoToolkit.Core.Reporting.Errors;

// One error: the forms that a rule, the catalog or pairing joined. A class holds at most one form per
// pipeline group, unless a rule joined them.
internal sealed class ErrorClass
{
    // From 0, in the order the error first appears: test order, then attempt order.
    internal required int Id { get; init; }
    // In the order they first appear.
    internal required IReadOnlyList<ErrorSignature> Forms { get; init; }
    // The rule that named the error, if one did.
    internal ErrorRule? Rule { get; init; }
    internal IReadOnlyList<ErrorPairing> Pairings { get; init; } = [];
    // Generic when a generic rule named it: it says something about the environment, not the test.
    internal bool IsGeneric => Rule?.IsGeneric == true;
    // Every form keyed on the test's own frame, which a message without its trace cannot have.
    internal bool IsLocated => Forms.All(static form => form.IsLocated);
}
