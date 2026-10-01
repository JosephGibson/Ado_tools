using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Architecture;

// tests/AGENTS.md: every fixture file has a row in tests/Fixtures/README.md.
public sealed partial class FixtureCatalogTests
{
    private static readonly string Root = Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures");

    [Fact]
    public void EveryFixtureFileHasOneCatalogEntryAndEveryEntryNamesAFile()
    {
        string[] files = [.. Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(static path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Where(static path => path != "README.md").Order(StringComparer.Ordinal)];
        (string Entry, Regex Pattern)[] entries = [.. Entries().Select(static entry => (entry, Pattern(entry)))];
        string[] notCatalogedOnce = [.. files.Where(file => entries.Count(entry => entry.Pattern.IsMatch(file)) != 1)];
        string[] withoutFile = [.. entries.Where(entry => !files.Any(entry.Pattern.IsMatch)).Select(static entry => entry.Entry)];

        Assert.NotEmpty(files);
        Assert.Empty(notCatalogedOnce);
        Assert.Empty(withoutFile);
    }

    // An entry is a code span that holds a path, in the first cell of a table row.
    private static IEnumerable<string> Entries() => File.ReadLines(Path.Combine(Root, "README.md"))
        .Where(static line => line.StartsWith("| `", StringComparison.Ordinal))
        .SelectMany(static line => CodeSpan().Matches(line.Split(" | ")[0]).Select(static match => match.Groups[1].Value))
        .Where(static entry => entry.Contains('/', StringComparison.Ordinal));

    // * stands for any run of characters inside one file name.
    private static Regex Pattern(string entry) =>
        new("^" + Regex.Escape(entry).Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$", RegexOptions.CultureInvariant);

    [GeneratedRegex("`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex CodeSpan();
}
