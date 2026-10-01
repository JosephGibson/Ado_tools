namespace AdoToolkit.Core.TestManagement;

// A file attached to a Test Case. Only its name and size are read, never its content.
public sealed class AdoTestCaseAttachment
{
    public required string Name { get; init; }
    public long? Size { get; init; }
    public string? Comment { get; init; }
}
