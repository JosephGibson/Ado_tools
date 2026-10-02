using AdoToolkit.Core.IO;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// The result of §13.4 steps 1–2: what one ShouldProcess call covers before anything is written.
public sealed class TestFailureExportPlan
{
    internal TestFailureExportPlan(TestFailureReportModel model, GenerationFolderPlan commit, TestFailureExportOptions options,
        bool downloadsAttachments, AttachmentSelection attachments)
    {
        Model = model;
        Commit = commit;
        Options = options;
        Attachments = attachments;
        AttachmentDirectory = downloadsAttachments ? commit.FolderPath : null;
    }

    public string ReportPath => Commit.ReportPath;
    // The folder that downloads would create; null when nothing is downloaded.
    public string? AttachmentDirectory { get; }
    public bool DownloadsAttachments => AttachmentDirectory is not null;
    // Report culture fallback warnings, in the session culture.
    public IReadOnlyList<string> Warnings => Model.Warnings;
    internal TestFailureReportModel Model { get; }
    internal GenerationFolderPlan Commit { get; }
    internal TestFailureExportOptions Options { get; }
    // The JSON and text attachments that are downloaded; DownloadsAttachments still gates the operation.
    internal AttachmentSelection Attachments { get; }
}
