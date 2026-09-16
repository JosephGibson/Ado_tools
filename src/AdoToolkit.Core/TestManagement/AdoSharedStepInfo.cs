namespace AdoToolkit.Core.TestManagement;

public sealed class AdoSharedStepInfo
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public int? Rev { get; init; }
    public string? TeamProject { get; init; }
    public Uri? WebUrl { get; init; }
    public int ReferenceCount { get; init; }
}
