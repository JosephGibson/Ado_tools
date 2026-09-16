namespace AdoToolkit.Core.TestRuns;

// Not an IOException, so the HTTP pipeline never retries a body that passed a byte limit.
internal sealed class AttachmentLimitException : Exception
{
    public AttachmentLimitException() { }
    public AttachmentLimitException(string? message) : base(message) { }
    public AttachmentLimitException(string? message, Exception? innerException) : base(message, innerException) { }
}
