using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Html;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting;

// The Test Case report shares the theme of the failed-test report: the tokens and components of
// report-base.css, on screen and in print, and one static script under a hash-only policy.
public sealed class TestCaseReportThemeTests
{
    private static readonly string[] OwnStyles = ["testcase-report.css", "testcase-document.css"];

    [Fact]
    public void OwnStylesDefineNoColorAndUseOnlyTheTokensOfTheSharedBase()
    {
        HashSet<string> tokens = Regex.Matches(TestFailureAssets.Read("report-base.css"), "(--[a-z0-9-]+)\\s*:")
            .Select(static match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        // Set by the script; every use carries a fallback.
        tokens.Add("--report-header-offset");
        foreach (string name in OwnStyles)
        {
            string css = TestFailureAssets.Read(name);
            Assert.DoesNotMatch("#[0-9a-fA-F]{3,8}\\b", css);
            Assert.DoesNotMatch("(?i)\\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color-mix)\\(", css);
            Assert.DoesNotMatch("(?i)\\bcolor-scheme\\b", css);
            // No second palette: nothing declares a custom property.
            Assert.DoesNotMatch("(?m)^\\s*--[a-z0-9-]+\\s*:", css);
            Assert.DoesNotMatch("[{;]\\s*--[a-z0-9-]+\\s*:", css);
            Assert.All(Regex.Matches(css, "var\\((--[a-z0-9-]+)").Select(static match => match.Groups[1].Value), token => Assert.Contains(token, tokens));
            Assert.All(Regex.Matches(css, "var\\(--report-header-offset([^)]*)\\)").Select(static match => match.Groups[1].Value), fallback => Assert.StartsWith(",", fallback, StringComparison.Ordinal));
            // A color is a token, a system color of forced-colors mode, or the absence of one.
            foreach (Match declaration in Regex.Matches(css, "(?<![a-z-])(?:color|background|background-color|border-color|outline-color|[a-z-]*-color)\\s*:\\s*([^;}]+)"))
                Assert.Matches("^(?:var\\(--[a-z0-9-]+\\)|CanvasText|Highlight|transparent|inherit)$", declaration.Groups[1].Value.Trim());
            Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("url(", css, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("prefers-color-scheme", css, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Regex.Matches(css, "(?<!-)\\bcontent\\s*:"));
        }
    }

    [Fact]
    public void ReportsEmbedTheSharedBaseThenTheirOwnStylesAndNothingElse()
    {
        string single = GoldenReportTests.Render(ReportFixture.Model("nested"), ReportFormat.Html);
        string multiple = GoldenReportTests.Render(MultiCaseFixture.Model(), ReportFormat.Html);
        string Styles(string html) => html[(html.IndexOf("<style>\n", StringComparison.Ordinal) + 8)..html.IndexOf("</style>", StringComparison.Ordinal)];
        Assert.Equal(TestFailureAssets.Read("report-base.css") + TestFailureAssets.Read("testcase-report.css"), Styles(single));
        Assert.Equal(Styles(single) + TestFailureAssets.Read("testcase-document.css"), Styles(multiple));
        // The palette of the failed-test report, dark on screen and light in print, is the only one.
        string failures = TestFailureReportFixture.Render();
        Assert.Contains(TestFailureAssets.Read("report-base.css"), failures, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(single, "color-scheme: dark"));
        Assert.Single(Regex.Matches(single, "color-scheme: light"));
        Assert.Single(Regex.Matches(single, "<style>"));
        Assert.DoesNotContain(" style=\"", TestFailureMarkup.WithoutScripts(multiple), StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentsOfTheFailedTestReportCarryTheMarkupAndTheRulesTheyNeed()
    {
        string html = GoldenReportTests.Render(MultiCaseFixture.Model(), ReportFormat.Html) + GoldenReportTests.Render(ReportFixture.Model("detailed"), ReportFormat.Html);
        string css = TestFailureAssets.Read("testcase-report.css") + TestFailureAssets.Read("testcase-document.css");
        foreach (string name in new[] { "top-bar-inner", "title-line", "report-brand", "count-label", "count-value", "section-links", "metadata-grid", "secondary-line", "name-section", "copy-feedback" })
        {
            Assert.Contains("class=\"" + name + "\"", html, StringComparison.Ordinal);
            Assert.Matches("(?m)^\\." + name + "\\b", css);
        }
        // Defined by the shared base and used as they are.
        foreach (string name in new[] { "top-bar", "diagnostic", "table-scroll" })
            Assert.Contains("class=\"" + name + "\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"status-badge status-failed\"", html, StringComparison.Ordinal);
        string print = css[css.IndexOf("@media print", StringComparison.Ordinal)..];
        Assert.Contains(".top-bar { max-height: none; overflow: visible; position: static; }", print, StringComparison.Ordinal);
        Assert.Contains(".section-links, .case-links, .filter-controls, .skip-link, .copy-feedback, [data-no-matches] { display: none !important; }", print, StringComparison.Ordinal);
        // Paper shows every step, whatever was collapsed on screen.
        Assert.Contains(".test-case.is-collapsed > .case-body, .shared-banner.is-collapsed + .shared-group { display: block; }", print, StringComparison.Ordinal);
        Assert.Contains(".step-card, .shared-banner, .diagnostic, .name-section, .metadata-grid > div { break-inside: avoid; }", print, StringComparison.Ordinal);
        Assert.Contains(".test-case + .test-case { break-before: page; }", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 700px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (forced-colors: active)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptAssetContainsNoForbiddenApiOrVisibleStringAssignment()
    {
        string script = TestFailureAssets.Read(HtmlTestCaseRenderer.ScriptAsset);
        foreach (string token in new[] { "eval", "Function(", "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "fetch", "XMLHttpRequest",
            "WebSocket", "EventSource", "sendBeacon", "import(", "localStorage", "sessionStorage", "indexedDB", "window.open", "postMessage", "createElement", "setAttribute('href'" })
            Assert.DoesNotContain(token, script, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(?:setTimeout|setInterval)\s*\(\s*['""`]", script);
        Assert.DoesNotMatch(@"\b(?:Function|import)\s*\(", script);
        Assert.DoesNotMatch(@"textContent\s*=\s*['""`]", script);
        Assert.DoesNotContain("</script", script, StringComparison.OrdinalIgnoreCase);
        // Text that the page shows comes from the page itself: labels in data attributes, values in table cells.
        Assert.Contains("status.textContent = status.dataset.labelDone", script, StringComparison.Ordinal);
        Assert.Contains("status.textContent = status.dataset.labelSelected", script, StringComparison.Ordinal);
        Assert.Contains("node.textContent = cell ? cell.textContent : tokens.get(node)", script, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(script) < 12 * 1024);
    }

    [Theory]
    [InlineData("parameterized", "en-US")]
    [InlineData("partial", "fr-CA")]
    [InlineData("rich", "en-US")]
    [InlineData("detailed", "fr-CA")]
    [InlineData("multi", "en-US")]
    public void ExactWrittenUtf8BytesHaveOneHashAndNoOtherScriptSources(string variant, string culture)
    {
        ReportDocumentModel model = variant == "multi" ? MultiCaseFixture.Model(culture) : ReportFixture.Model(variant, culture);
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.html");
        new AtomicFileWriter().Write(path, writer => TestCaseExporter.Render(model, writer, ReportFormat.Html),
            temporary => TestCaseExporter.Validate(temporary, model, ReportFormat.Html), model.Culture, cancellationToken: TestContext.Current.CancellationToken);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.DoesNotContain((byte)'\r', bytes);
        int start = bytes.AsSpan().IndexOf("<script>"u8) + "<script>"u8.Length;
        int length = bytes.AsSpan(start).IndexOf("</script>"u8);
        string hash = Convert.ToBase64String(SHA256.HashData(bytes.AsSpan(start, length)));
        string html = Encoding.UTF8.GetString(bytes);
        Assert.Single(TestFailureMarkup.Scripts().Matches(html));
        Assert.Equal("default-src 'none'; script-src 'sha256-" + hash + "'; style-src 'unsafe-inline'; img-src 'self' data:; base-uri 'none'; form-action 'none'",
            TestFailureMarkup.Policy(html));
        Assert.Empty(TestFailureMarkup.Scripts().Match(html).Groups["attributes"].Value);
        string body = Encoding.UTF8.GetString(bytes, start, length);
        // Static toolkit bytes only: the same for every report and culture, with nothing of the report inside.
        Assert.Equal(TestFailureAssets.Read(HtmlTestCaseRenderer.ScriptAsset), body);
        Assert.DoesNotContain(ReportFixture.Project, body, StringComparison.Ordinal);
        Assert.DoesNotContain(model.ToolkitVersion, body, StringComparison.Ordinal);
        Assert.True(html.IndexOf("Content-Security-Policy", StringComparison.Ordinal) < html.IndexOf("<style>", StringComparison.Ordinal));
        string normalized = TestFailureMarkup.NormalizeGolden(html);
        Assert.Contains("<script>__SCRIPT_ASSET__</script>", normalized, StringComparison.Ordinal);
        // The whole hash becomes the placeholder, whatever characters the encoder wrote it with.
        Assert.Equal("default-src 'none'; script-src 'sha256-__SCRIPT_SHA256__'; style-src 'unsafe-inline'; img-src 'self' data:; base-uri 'none'; form-action 'none'",
            TestFailureMarkup.Policy(normalized));
    }

    [Theory]
    [InlineData("parameterized")]
    [InlineData("nested")]
    [InlineData("detailed")]
    [InlineData("multi")]
    public void MarkupHasNoInlineHandlersOrExecutableUrlsAndScriptControlsStartHidden(string variant)
    {
        string html = TestFailureMarkup.WithoutScripts(GoldenReportTests.Render(variant == "multi" ? MultiCaseFixture.Model() : ReportFixture.Model(variant), ReportFormat.Html));
        html = html[html.IndexOf("<body>", StringComparison.Ordinal)..];
        int controls = 0;
        foreach (Match tag in TestFailureMarkup.Tags().Matches(html))
        {
            // Remove quoted values so display text is never confused with attribute names.
            string names = Regex.Replace(tag.Value, "\"[^\"]*\"|'[^']*'", "\"\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            Assert.DoesNotMatch(@"\son[a-z]+\s*=", names);
            Assert.DoesNotMatch(@"\sstyle\s*=", names);
            Assert.DoesNotMatch("(?i)(?:href|src)=\"(?:javascript|data|vbscript|file):", tag.Value);
            if (!tag.Value.StartsWith("<button", StringComparison.Ordinal) && !tag.Value.Contains("data-enhance", StringComparison.Ordinal)) continue;
            // Without the script the report shows no control that would do nothing.
            Assert.Contains(" hidden", tag.Value, StringComparison.Ordinal);
            controls++;
        }
        Assert.True(controls >= 2);
        Assert.Contains("<div class=\"interactive filter-controls\" data-enhance hidden>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", html, StringComparison.OrdinalIgnoreCase);
    }
}
