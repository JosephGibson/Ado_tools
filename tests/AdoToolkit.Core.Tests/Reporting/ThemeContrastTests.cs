using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.Tests.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting;

public sealed class ThemeContrastTests
{
    [Fact]
    public void EmbeddedScreenAndPrintPalettesMeetTextAndMeaningfulMarkContrast()
    {
        string css = Asset("report-base.css");
        int print = css.IndexOf("@media print", StringComparison.Ordinal);
        Assert.True(print > 0);
        Dictionary<string, string> screen = Properties(css[..print]);
        Dictionary<string, string> paper = new(screen, StringComparer.Ordinal);
        foreach ((string key, string value) in Properties(css[print..])) paper[key] = value;
        foreach (Dictionary<string, string> palette in new[] { screen, paper })
        {
            foreach (string background in new[] { "--bg", "--surface", "--surface-2", "--code-bg" })
            {
                foreach (string foreground in new[] { "--text", "--text-muted", "--link", "--link-visited" }) Check(palette, foreground, background, 4.5);
                foreach (string foreground in new[] { "--border", "--focus", "--current", "--pass", "--fail", "--flaky", "--other", "--unavailable", "--info" })
                    Check(palette, foreground, background, 3);
                // Status glyphs and labels are text too, not just chart marks.
                foreach (string foreground in new[] { "--pass", "--fail", "--flaky", "--other", "--unavailable" }) Check(palette, foreground, background, 4.5);
            }
            string[] codeTokens = palette.Keys.Where(static key => key.StartsWith("--tok-", StringComparison.Ordinal)).ToArray();
            Assert.True(codeTokens.Length >= 11);
            foreach (string token in codeTokens) Check(palette, token, "--code-bg", 4.5);
            foreach (string background in new[] { "--pass", "--fail", "--other" }) Check(palette, "--chart-label", background, 4.5);
        }
    }

    [Fact]
    public void AssetsAreStaticAccessibleAndSelfContained()
    {
        string css = Asset("report-base.css") + Asset("test-failures.css");
        Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prefers-color-scheme", css, StringComparison.OrdinalIgnoreCase);
        foreach (string rule in new[] { "max-width: 1280px", "position: sticky", "max-width: 700px", ":focus-visible", "prefers-reduced-motion",
            "@media print", "break-inside: avoid", "white-space: pre-wrap", "tab-size: 4", "[hidden]",
            "var(--font-sans)", "var(--font-mono)", "tbody tr:nth-child(even)", "thead { display: table-header-group; }",
            "@media (forced-colors: active)" }) Assert.Contains(rule, css, StringComparison.Ordinal);
        Assert.Empty(Regex.Matches(css, "(?<!-)\\bcontent\\s*:"));
        HashSet<string> defined = Regex.Matches(css, "(--[a-z0-9-]+)\\s*:").Select(static match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(css, "var\\((--[a-z0-9-]+)\\)")) Assert.Contains(match.Groups[1].Value, defined);
    }

    [Fact]
    public void ReportHierarchyHasMatchingMarkupAndPrintOverrides()
    {
        string html = TestFailureReportFixture.Render();
        string css = Asset("test-failures.css");
        foreach (string name in new[] { "report-brand", "section-heading", "count-label", "count-value", "attempt-title", "attempt-meta" })
        {
            Assert.Contains("class=\"" + name + "\"", html, StringComparison.Ordinal);
            Assert.Contains("." + name + " {", css, StringComparison.Ordinal);
        }
        string print = css[css.IndexOf("@media print", StringComparison.Ordinal)..];
        Assert.Contains(".top-bar { max-height: none; overflow: visible; position: static; }", print, StringComparison.Ordinal);
        Assert.Contains(".code-section.hide-framework .framework-frame { display: inline; }", print, StringComparison.Ordinal);
        Assert.Contains(".code-section pre { overflow: visible; max-height: none; }", print, StringComparison.Ordinal);
        Assert.Contains(".col-error { white-space: normal; max-width: none; }", print, StringComparison.Ordinal);
    }

    private static string Asset(string name)
    {
        using Stream stream = typeof(RunHistoryChart).Assembly.GetManifestResourceStream("AdoToolkit.Core.Reporting.Assets." + name)!;
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, string> Properties(string css)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(css, "(--[a-z0-9-]+)\\s*:\\s*(#[0-9a-fA-F]{6})\\s*;")) result[match.Groups[1].Value] = match.Groups[2].Value;
        return result;
    }

    private static void Check(Dictionary<string, string> palette, string foreground, string background, double required)
    {
        double a = Luminance(palette[foreground]), b = Luminance(palette[background]);
        double contrast = (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        Assert.True(contrast >= required, FormattableString.Invariant($"{foreground} {palette[foreground]} on {background} {palette[background]}: {contrast:F3} < {required}"));
    }

    private static double Luminance(string hex)
    {
        double Channel(int start)
        {
            double value = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}
