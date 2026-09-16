using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Html;
using AdoToolkit.Core.Reporting.Json;
using AdoToolkit.Core.Reporting.Markdown;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S3-5")]
public sealed class MultiCaseDocumentTests
{
    private static readonly string[] Cultures = ["en-US", "fr-CA"];
    private static readonly string[] Anchors = ["tc-10", "tc-11", "tc-10-2", "tc-12"];
    private static readonly string[] Groups = ["Racine › Connexion", "Racine › Paiement &lt;b&gt;"];
    private static readonly Lazy<List<AdoTestCase>> Large = new(GenerateLarge);
    public static TheoryData<string, ReportFormat> GoldenCases => new(
        from culture in Cultures
        from format in Enum.GetValues<ReportFormat>()
        select (culture, format));

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void MultiCaseDocumentMatchesReviewedGolden(string culture, ReportFormat format)
    {
        ReportDocumentModel model = MultiCaseFixture.Model(culture);
        using TestDirectory directory = new();
        string name = "testcases-multi." + culture + "." + GoldenReportTests.Extension(format);
        string golden = Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Reports", name);
        string actual = Path.Combine(directory.Root, name);
        new AtomicFileWriter().Write(actual, writer => TestCaseExporter.Render(model, writer, format),
            path => TestCaseExporter.Validate(path, model, format), model.Culture, cancellationToken: TestContext.Current.CancellationToken);
        byte[] bytes = File.ReadAllBytes(actual);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        if (format == ReportFormat.Json) ReportFixture.AssertValid(File.ReadAllText(actual));
        if (Environment.GetEnvironmentVariable("ADOTOOLKIT_UPDATE_GOLDEN") == "1")
            new AtomicFileWriter().Write(golden, writer => TestCaseExporter.Render(model, writer, format),
                path => TestCaseExporter.Validate(path, model, format), model.Culture, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(File.Exists(golden), "Missing reviewed golden: " + name);
        Assert.Equal(File.ReadAllBytes(golden), bytes);
    }

    [Fact]
    public void HtmlHasCoverAndSuiteGroupedContentsWithWorkingUniqueAnchorsAndRestartedNumbering()
    {
        ReportDocumentModel model = MultiCaseFixture.Model();
        string html = GoldenReportTests.Render(model, ReportFormat.Html);
        Assert.Contains("<html lang=\"en-US\" data-case-count=\"4\">", html, StringComparison.Ordinal);
        string[] ids = Regex.Matches(html, "\\sid=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        string toc = html[html.IndexOf("<nav class=\"toc\"", StringComparison.Ordinal)..html.IndexOf("<main>", StringComparison.Ordinal)];
        string[] links = Regex.Matches(toc, "href=\"#([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(Anchors, links);
        Assert.All(links, link => Assert.Contains(link, ids));
        Assert.Equal(Groups, Regex.Matches(toc, "<h3>([^<]*)</h3>").Select(match => match.Groups[1].Value));
        Assert.Equal(Anchors, Regex.Matches(html, "<article class=\"test-case\" id=\"([^\"]+)\">").Select(match => match.Groups[1].Value));
        Assert.All(html.Split("<article ")[1..], article =>
            Assert.Equal("1", Regex.Match(article[article.IndexOf("<section class=\"steps\"", StringComparison.Ordinal)..], "<span class=\"outline-number\">([^<]+)</span>").Groups[1].Value));
        string cover = html[html.IndexOf("<header class=\"document-cover\">", StringComparison.Ordinal)..html.IndexOf("<nav class=\"toc\"", StringComparison.Ordinal)];
        Assert.Contains("<h1>Azure DevOps Test Case Report</h1>", cover, StringComparison.Ordinal);
        Assert.Contains("href=\"https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_testPlans/define?planId=40\"", cover, StringComparison.Ordinal);
        Assert.Contains("<dt>Test Suite</dt><dd>Racine</dd>", cover, StringComparison.Ordinal);
        Assert.Contains("<dt>Complete</dt><dd class=\"technical\">3</dd>", cover, StringComparison.Ordinal);
        Assert.Contains("<dt>Partial</dt><dd class=\"technical\">1</dd>", cover, StringComparison.Ordinal);
        Assert.DoesNotContain("untrusted.example.test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkdownContentsLinkToExplicitAnchorsBeforeEachCaseHeading()
    {
        string markdown = GoldenReportTests.Render(MultiCaseFixture.Model("fr-CA"), ReportFormat.Markdown);
        Assert.StartsWith("# Rapport de cas de test Azure DevOps\n", markdown, StringComparison.Ordinal);
        Assert.Equal(Anchors, Regex.Matches(markdown, "\\]\\(#(tc-[0-9-]+)\\)").Select(match => match.Groups[1].Value));
        Assert.Equal(Anchors, Regex.Matches(markdown, "<a id=\"(tc-[0-9-]+)\"></a>\n\n# ").Select(match => match.Groups[1].Value));
        Assert.Equal(5, Regex.Count(markdown, "^# ", RegexOptions.Multiline));
        Assert.Contains("**Racine › Paiement &lt;b&gt;**", markdown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("suites", "TestSuite", 4, 3, 1)]
    [InlineData("query", "Query", 2, 2, 0)]
    [InlineData("mixed", "Mixed", 2, 1, 1)]
    public void JsonCarriesSourceTotalsAndSuitesAndValidates(string shape, string source, int count, int complete, int partial)
    {
        IReadOnlyList<AdoTestCase> cases = shape switch
        {
            "suites" => MultiCaseFixture.Cases(),
            "query" => [MultiCaseFixture.Occurrence(10, "direct", null), MultiCaseFixture.Occurrence(11, "nested", null)],
            _ => [MultiCaseFixture.Occurrence(10, "direct", MultiCaseFixture.First), MultiCaseFixture.Occurrence(12, "partial", null)],
        };
        ReportDocumentModel model = MultiCaseFixture.Model("fr-CA", cases);
        using JsonDocument json = JsonDocument.Parse(ReportFixture.Render(model));
        JsonElement root = json.RootElement;
        Assert.Equal(source, root.GetProperty("source").GetString());
        Assert.Equal(count, root.GetProperty("totals").GetProperty("caseCount").GetInt32());
        Assert.Equal(complete, root.GetProperty("totals").GetProperty("complete").GetInt32());
        Assert.Equal(partial, root.GetProperty("totals").GetProperty("partial").GetInt32());
        Assert.Equal(count, root.GetProperty("cases").GetArrayLength());
        Assert.Equal(cases.Select(item => item.Suite?.SuiteId), root.GetProperty("cases").EnumerateArray()
            .Select(item => item.TryGetProperty("suite", out JsonElement suite) ? suite.GetProperty("suiteId").GetInt32() : (int?)null));
        if (shape != "suites")
        {
            string html = GoldenReportTests.Render(model, ReportFormat.Html);
            Assert.Contains("<dt>Source</dt><dd>Requête WIQL ou liste d’ID</dd>", html, StringComparison.Ordinal);
            Assert.Contains("<h3>Requête WIQL ou liste d’ID</h3>", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("single")]
    [InlineData("missing")]
    public void SchemaRejectsInvalidTotals(string mutation)
    {
        JsonObject root = JsonNode.Parse(ReportFixture.Render(MultiCaseFixture.Model()))!.AsObject();
        JsonObject totals = root["totals"]!.AsObject();
        switch (mutation)
        {
            case "extra": totals["other"] = 1; break;
            case "single": totals["caseCount"] = 1; break;
            default: totals.Remove("partial"); break;
        }
        Assert.False(ReportFixture.Evaluate(root.ToJsonString()).IsValid);
    }

    [Fact]
    public void ValidationRejectsTruncatedOrMiscountedMultiCaseOutput()
    {
        ReportDocumentModel model = MultiCaseFixture.Model();
        using TestDirectory directory = new();
        foreach (ReportFormat format in Enum.GetValues<ReportFormat>())
        {
            string path = Path.Combine(directory.Root, "report." + GoldenReportTests.Extension(format));
            string text = GoldenReportTests.Render(model, format);
            string broken = format switch
            {
                ReportFormat.Html => text[..text.LastIndexOf("<article ", StringComparison.Ordinal)] + "</main>\n</body>\n</html>\n",
                ReportFormat.Markdown => text[..text.LastIndexOf("<a id=", StringComparison.Ordinal)],
                _ => RemoveLastCase(JsonNode.Parse(text)!.AsObject()),
            };
            File.WriteAllText(path, broken);
            Assert.Throws<InvalidDataException>(() => TestCaseExporter.Validate(path, model, format));
            File.WriteAllText(path, text);
            TestCaseExporter.Validate(path, model, format);
        }
    }

    [Fact]
    public void GeneratedThreeHundredCaseDocumentWritesEachBodyBeforeBuildingTheNext()
    {
        IReadOnlyList<AdoTestCase> cases = Large.Value;
        ReportDocumentModel built = MultiCaseFixture.Model("fr-CA", cases);
        // Case models are built on access and never retained by the document.
        Assert.NotSame(built.Cases[0], built.Cases[0]);
        Assert.Equal(300, built.Contents.Count);
        Assert.Equal(300, built.Contents.Select(entry => entry.Anchor).Distinct(StringComparer.Ordinal).Count());
        foreach (ReportFormat format in Enum.GetValues<ReportFormat>())
        {
            using StringWriter writer = new(CultureInfo.InvariantCulture);
            List<int> positions = [];
            ReportDocumentModel recorded = new()
            {
                Culture = built.Culture, GeneratedAt = built.GeneratedAt, ToolkitVersion = built.ToolkitVersion, CollectionUri = built.CollectionUri,
                Project = built.Project, Source = built.Source, Labels = built.Labels, Contents = built.Contents,
                Cases = new RecordingCases(built.Cases, () => positions.Add(writer.GetStringBuilder().Length)),
            };
            switch (format)
            {
                case ReportFormat.Html: HtmlTestCaseRenderer.Render(recorded, writer); break;
                case ReportFormat.Markdown: MarkdownTestCaseRenderer.Render(recorded, writer); break;
                default: JsonTestCaseRenderer.Render(recorded, writer); break;
            }
            Assert.Equal(300, positions.Count);
            // Output grows between consecutive case models: bodies are interleaved with model enumeration.
            Assert.All(positions.Zip(positions.Skip(1)), pair => Assert.True(pair.Second > pair.First, format.ToString()));
        }

        using TestDirectory directory = new();
        foreach (ReportFormat format in Enum.GetValues<ReportFormat>())
        {
            FileInfo file = new AtomicFileWriter().Write(Path.Combine(directory.Root, "large." + GoldenReportTests.Extension(format)),
                writer => TestCaseExporter.Render(built, writer, format), path => TestCaseExporter.Validate(path, built, format), built.Culture,
                cancellationToken: TestContext.Current.CancellationToken);
            string text = File.ReadAllText(file.FullName);
            if (format == ReportFormat.Html)
            {
                Assert.Contains("data-case-count=\"300\"", text, StringComparison.Ordinal);
                Assert.Equal(300, Regex.Count(text, "<article class=\"test-case\" id=\"tc-"));
                Assert.Equal(300, Regex.Count(text, "<li><a href=\"#tc-"));
                Assert.Equal(3, Regex.Count(text, "<section class=\"toc-group\">"));
                string[] ids = Regex.Matches(text, "\\sid=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
                Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
            }
            else if (format == ReportFormat.Markdown) Assert.Equal(301, Regex.Count(text, "^# ", RegexOptions.Multiline));
            else
            {
                using JsonDocument json = JsonDocument.Parse(text);
                Assert.Equal(300, json.RootElement.GetProperty("cases").GetArrayLength());
                Assert.Equal(300, json.RootElement.GetProperty("totals").GetProperty("caseCount").GetInt32());
            }
        }
    }

    private static string RemoveLastCase(JsonObject root)
    {
        JsonArray items = root["cases"]!.AsArray();
        items.RemoveAt(items.Count - 1);
        return root.ToJsonString();
    }

    private static List<AdoTestCase> GenerateLarge()
    {
        List<AdoTestCase> cases = [];
        string[] variants = ["direct", "nested", "partial", "parameterized"];
        for (int index = 0; index < 300; index++)
        {
            // Groups of 100 per suite; IDs repeat across the second and third hundred to exercise -k anchors.
            AdoTestSuiteRef suite = index < 100 ? MultiCaseFixture.First : index < 200 ? MultiCaseFixture.Second : Third;
            int id = index < 200 ? 1000 + index : 1100 + (index - 200);
            cases.Add(MultiCaseFixture.Occurrence(id, variants[index % variants.Length], suite));
        }
        return cases;
    }

    private static readonly AdoTestSuiteRef Third = new()
    {
        PlanId = 40, SuiteId = 53, PlanName = "Plan « Été »", SuiteName = "Troisième", SuitePath = ["Racine", "Troisième"],
        TeamProject = ReportFixture.Project, CollectionUri = ReportFixture.Collection,
    };

    private sealed class RecordingCases(IReadOnlyList<TestCaseReportModel> inner, Action record) : IReadOnlyList<TestCaseReportModel>
    {
        public int Count => inner.Count;
        public TestCaseReportModel this[int index] => inner[index];
        public IEnumerator<TestCaseReportModel> GetEnumerator()
        {
            foreach (TestCaseReportModel item in inner)
            {
                record();
                yield return item;
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
