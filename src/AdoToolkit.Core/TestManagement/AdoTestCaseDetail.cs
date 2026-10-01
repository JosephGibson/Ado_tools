using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.TestManagement;

// What Export-AdoTestCase -IncludeDetail reads about one Test Case beyond its steps: the fields of
// its summary, its links, and where it is planned with the latest outcome there. A part that
// could not be read is absent and named in Diagnostics; it never fails the export.
public sealed class AdoTestCaseDetail
{
    public required int Id { get; init; }
    // False when the Test Case could not be read; the fields and links below are then empty.
    public bool IsResolved { get; init; }
    // Plain text, and the markup it was converted from.
    public string? Description { get; init; }
    public string? DescriptionSource { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public AdoIdentityRef? CreatedBy { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public string? AutomatedTestName { get; init; }
    public string? AutomatedTestStorage { get; init; }
    public string? AutomatedTestType { get; init; }
    public IReadOnlyList<AdoLinkedWorkItem> Links { get; init; } = Array.Empty<AdoLinkedWorkItem>();
    public IReadOnlyList<AdoTestCaseHyperlink> Hyperlinks { get; init; } = Array.Empty<AdoTestCaseHyperlink>();
    public IReadOnlyList<AdoTestCaseAttachment> Attachments { get; init; } = Array.Empty<AdoTestCaseAttachment>();
    // Null when the test points could not be read; empty when the Test Case is in no suite.
    public IReadOnlyList<AdoTestPoint>? Points { get; init; }
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
}
