using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

[Trait("Acceptance", "S5-4")]
public sealed class GoldenTestFailureReportTests
{
    private static readonly string[] Variants = ["failed", "flaky", "partial", "hostile"];
    private static readonly string[] Cultures = ["en-US", "fr-CA"];
    public static TheoryData<string, string> Cases => new(
        from variant in Variants
        from culture in Cultures
        select (variant, culture));

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesReviewedGoldenWithOnlyScriptAndHashReplaced(string variant, string culture)
    {
        var model = TestFailureReportFixture.Model(variant, culture);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string normalized = TestFailureMarkup.NormalizeGolden(html);
        string name = "testfailures-" + variant + "." + culture + ".html";
        string golden = Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Reports", name);
        // Explicit opt-in only, after the user reviews actual (unmodified) English/French reports.
        if (Environment.GetEnvironmentVariable("ADOTOOLKIT_UPDATE_GOLDEN") == "1")
            new AtomicFileWriter().Write(golden, writer => writer.Write(normalized),
                path => Assert.Equal(normalized, File.ReadAllText(path)), model.Culture, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(File.Exists(golden), "Missing reviewed golden: " + name);
        Assert.Equal(File.ReadAllBytes(golden), Encoding.UTF8.GetBytes(normalized));
    }
}
