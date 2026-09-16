namespace AdoToolkit.Core.TestManagement;

// One requested occurrence of a test case: by ID (no suite) or through a suite.
public sealed class TestCaseMembership
{
    public int TestCaseId { get; init; }
    public AdoTestSuiteRef? Suite { get; init; }
}
