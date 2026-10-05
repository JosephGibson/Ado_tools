using AdoToolkit.Core.IO;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// The result of §13.4 steps 1–2: what one ShouldProcess call covers before anything is written.
public sealed class TestFailureExportPlan
{
    internal TestFailureExportPlan(TestFailureReportModel model, string reportPath, GenerationFolderPlan? commit,
        TestFailureExportOptions options, bool downloadsAttachments, AttachmentSelection attachments)
    {
        Model = model;
        ReportPath = reportPath;
        Commit = commit;
        Options = options;
        Attachments = attachments;
        AttachmentDirectory = downloadsAttachments && commit is not null ? commit.FolderPath : null;
    }

    public string ReportPath { get; }
    // The folder that downloads would create; null when nothing is downloaded.
    public string? AttachmentDirectory { get; }
    public bool DownloadsAttachments => AttachmentDirectory is not null;
    // Report culture fallback warnings, in the session culture. A CSV file is the same in every
    // culture, so it has none.
    public IReadOnlyList<string> Warnings => Commit is null ? [] : Model.Warnings;
    internal TestFailureReportModel Model { get; }
    // The report and its generation folder; null for a CSV file, which has no folder.
    internal GenerationFolderPlan? Commit { get; }
    internal TestFailureExportOptions Options { get; }
    // The JSON and text attachments that are downloaded; DownloadsAttachments still gates the operation.
    internal AttachmentSelection Attachments { get; }
}
