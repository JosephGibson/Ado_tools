namespace AdoToolkit.Core.TestRuns;

// Not an IOException, so the HTTP pipeline never retries a body that passed a byte limit.
internal sealed class AttachmentLimitException : Exception;
