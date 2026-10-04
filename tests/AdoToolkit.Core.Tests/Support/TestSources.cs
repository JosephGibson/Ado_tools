namespace AdoToolkit.Core.Tests.Support;

// The C# sources of a test project, for architecture tests that read the tests themselves.
internal static class TestSources
{
    internal sealed record Source(string Path, string Text);

    // Paths are relative to the project folder, with '/'. Build output is left out.
    internal static IEnumerable<Source> Sources(string root) => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
        .Select(path => System.IO.Path.GetRelativePath(root, path).Replace('\\', '/'))
        .Where(static path => !path.StartsWith("bin/", StringComparison.Ordinal) && !path.StartsWith("obj/", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .Select(path => new Source(path, File.ReadAllText(System.IO.Path.Combine(root, path))));
}
