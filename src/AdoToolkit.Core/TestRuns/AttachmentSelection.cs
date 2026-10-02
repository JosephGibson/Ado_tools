namespace AdoToolkit.Core.TestRuns;

// Which attachments one export downloads. Only JSON and text are ever downloaded. A run in
// FullRunIds gives every such file up to the per-file limit; a run in SmallRunIds gives those of
// at most SmallLimit bytes, or of unknown size, which are then cut off at SmallLimit while they
// are read. LatestRunId is the last run in attempt order: its files are downloaded and previewed
// first, so the limits on totals never spend themselves on older runs.
internal sealed record AttachmentSelection(IReadOnlySet<int> FullRunIds, IReadOnlySet<int> SmallRunIds, long SmallLimit, int? LatestRunId)
{
    internal bool Selects(AdoTestAttachment attachment) => AttachmentKinds.IsDownloadable(attachment.Kind)
        && (FullRunIds.Contains(attachment.RunId)
            || (SmallRunIds.Contains(attachment.RunId) && (attachment.Size is not long size || size <= SmallLimit)));

    // Selected for its size alone. A body that turns out larger than SmallLimit is dropped without a warning.
    internal bool IsSmallOnly(AdoTestAttachment attachment) => !FullRunIds.Contains(attachment.RunId);
}
