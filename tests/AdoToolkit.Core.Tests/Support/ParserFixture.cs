namespace AdoToolkit.Core.Tests.Support;

internal static class ParserFixture
{
    internal static string Read(string path) => File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests/Fixtures", path));
}
