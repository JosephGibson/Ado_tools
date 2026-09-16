namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestAttachment
{
    public required int Id { get; init; }
    public required int RunId { get; init; }
    public required int ResultId { get; init; }
    public int? SubResultId { get; init; }
    // Remote name: display only. Local paths always use toolkit-generated names (§18 item 7).
    public required string FileName { get; init; }
    public string? Comment { get; init; }
    public long? Size { get; init; }
    public string? AttachmentType { get; init; }
    public AdoTestAttachmentKind Kind { get; init; }
    // Both are set by export only.
    public string? LocalRelativePath { get; init; }
    public AdoTestAttachmentStatus DownloadStatus { get; init; }

    internal AdoTestAttachment WithDownload(AdoTestAttachmentStatus status, string? localRelativePath) => new()
    {
        Id = Id, RunId = RunId, ResultId = ResultId, SubResultId = SubResultId, FileName = FileName,
        Comment = Comment, Size = Size, AttachmentType = AttachmentType, Kind = Kind,
        LocalRelativePath = localRelativePath, DownloadStatus = status,
    };
}
