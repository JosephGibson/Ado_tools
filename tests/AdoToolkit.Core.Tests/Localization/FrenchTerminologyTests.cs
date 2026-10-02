using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Localization;

// The toolkit's French follows one glossary (§3.6): "build" is masculine, and a test run is a
// « série de tests ». « Exécution » is kept for a pipeline run. A no-break space precedes a colon.
public sealed partial class FrenchTerminologyTests
{
    private const string Catalog = "Strings.fr.resx";

    // These two strings are part of the rendered reports and still have an ordinary space: the
    // goldens that hold them change only through the update-goldens skill.
    private static readonly string[] ColonsHeldByGoldens = ["RichTextImageAlt", "PartialTestCase"];

    private static readonly string[] TestRunKeys =
    [
        "NoTestRuns", "TestRunInProgress", "UngroupedTestResult", "InvalidTestCaseReference", "ProgressTestRuns", "ProgressTestResults",
        "TestReportRun", "TestReportRunsAndHistory", "TestReportWindowNote",
        "AttachmentTooLarge", "AttachmentDownloadFailed", "AttachmentContentMismatch",
    ];

    // The string catalog, then every French help source.
    public static TheoryData<string> Sources
    {
        get
        {
            TheoryData<string> sources = [Catalog];
            foreach (string name in Directory.EnumerateFiles(HelpDirectory, "*.md").Select(static path => Path.GetFileName(path)).Order(StringComparer.Ordinal))
                sources.Add(name);
            return sources;
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void BuildIsMasculine(string source)
    {
        foreach ((string location, string text) in Read(source))
            Assert.False(FeminineBuild().IsMatch(text), location + ": " + text);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void ANoBreakSpacePrecedesAColon(string source)
    {
        foreach ((string location, string text) in Read(source))
            if (source != Catalog || !ColonsHeldByGoldens.Contains(location))
                Assert.False(text.Contains(" :", StringComparison.Ordinal), location + ": " + text);
    }

    [Fact]
    public void TestRunStringsUseTheGlossaryTerm()
    {
        Dictionary<string, string> french = ResourceCatalog.French();
        foreach (string key in TestRunKeys)
        {
            Assert.Contains("série", french[key], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("exécution", french[key], StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void ATestRunIsNeverCalledATestExecution(string source)
    {
        foreach ((string location, string text) in Read(source))
            Assert.False(TestExecution().IsMatch(text), location + ": " + text);
    }

    private static string HelpDirectory => Path.Combine(TestDirectory.RepositoryRoot, "docs", "commands", "fr-CA");

    private static IEnumerable<(string Location, string Text)> Read(string source) => source == Catalog
        ? ResourceCatalog.French().Select(static pair => (pair.Key, pair.Value))
        : File.ReadAllLines(Path.Combine(HelpDirectory, source)).Select((line, index) =>
            (source + ":" + (index + 1).ToString(CultureInfo.InvariantCulture), line));

    [GeneratedRegex(@"\b(?:la|une|cette|aucune|dernière|première)\s+build\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeminineBuild();

    [GeneratedRegex(@"\bexécutions?\s+de\s+tests?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TestExecution();
}
