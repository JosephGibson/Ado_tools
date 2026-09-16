namespace AdoToolkit.Core.TestManagement;

internal sealed class TestPlanDto
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public NamedReferenceDto? RootSuite { get; init; }
}
