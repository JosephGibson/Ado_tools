namespace AdoToolkit.Core.Reporting.Errors;

// One attempt's error as the classifier read it. The attempt is named by its number, never held:
// an export works on copies of the failures (AttachmentDownloader), so the report looks the attempt
// up in the failure it renders.
internal sealed class ErrorOccurrence
{
    // The test's place in the list the classifier read.
    internal required int Test { get; init; }
    internal required int AttemptNumber { get; init; }
    // The pipeline group of the attempt's run, or null in a build without groups.
    internal int? Group { get; init; }
    // False for the one attempt read when no failed attempt has a message (D-1).
    internal required bool Failed { get; init; }
    internal required ErrorSignature Form { get; init; }
    // The language its framework text shows, which tells an English group from a French one.
    internal ErrorLanguage Evidence { get; init; }
    internal string? ExceptionType { get; init; }
    // The test's own frames, method and line: two attempts with one fingerprint failed at one place.
    internal string? Fingerprint { get; init; }
}
