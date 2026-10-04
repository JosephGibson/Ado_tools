using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.Architecture;

// tests/AGENTS.md: every fixture file has a row in tests/Fixtures/README.md.
[Trait("Culture", "Invariant")]
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

    // tests/AGENTS.md: delete a fixture that no test reads. The catalog keeps a row and a file in step,
    // so a golden dropped from a theory would stay reviewed, cataloged and never compared again. The
    // three golden theories name every report they compare, so their rows are the whole expected set.
    [Fact]
    public void EveryReportGoldenIsComparedByAGoldenTheory()
    {
        string[] files = [.. Directory.EnumerateFiles(Path.Combine(Root, "Reports"), "*", SearchOption.AllDirectories)
            .Select(static path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];
        string[] compared = [.. ExpectedGoldenNames().Order(StringComparer.Ordinal)];

        Assert.NotEmpty(files);
        Assert.Empty(files.Except(compared, StringComparer.Ordinal));
        Assert.Empty(compared.Except(files, StringComparer.Ordinal));
    }

    private static IEnumerable<string> ExpectedGoldenNames()
    {
        foreach (object?[] data in Rows(Reporting.GoldenReportTests.Cases))
            yield return "testcase-" + data[0] + "." + data[1] + "." + Reporting.GoldenReportTests.Extension((ReportFormat)data[2]!);
        foreach (object?[] data in Rows(Reporting.MultiCaseDocumentTests.GoldenCases))
            yield return "testcases-multi." + data[0] + "." + Reporting.GoldenReportTests.Extension((ReportFormat)data[1]!);
        foreach (object?[] data in Rows(Reporting.TestFailures.GoldenTestFailureReportTests.Cases))
            yield return "testfailures-" + data[0] + "." + data[1] + ".html";
    }

    private static IEnumerable<object?[]> Rows(IEnumerable<ITheoryDataRow> cases) => cases.Select(static row => row.GetData());

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
