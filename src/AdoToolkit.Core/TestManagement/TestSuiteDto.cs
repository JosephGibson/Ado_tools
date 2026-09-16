namespace AdoToolkit.Core.TestManagement;

internal sealed class TestSuiteDto
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public NamedReferenceDto? ParentSuite { get; init; }
}
