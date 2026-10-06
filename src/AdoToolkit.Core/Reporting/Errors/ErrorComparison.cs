namespace AdoToolkit.Core.Reporting.Errors;

// A test's primary error against the error of its previous failed build: Same when a start of that
// build's listed messages has a form of the primary error (D-8).
internal sealed record ErrorComparison(int BuildId, string BuildNumber, bool Same);
