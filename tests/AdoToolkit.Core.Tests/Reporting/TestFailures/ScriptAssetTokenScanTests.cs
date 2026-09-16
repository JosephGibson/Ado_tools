using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

[Trait("Acceptance", "S5-5")]
public sealed class ScriptAssetTokenScanTests
{
    [Fact]
    public void AssetContainsNoForbiddenApiOrVisibleStringAssignment()
    {
        string script = TestFailureAssets.Read("test-failures.js");
        foreach (string token in new[] { "eval", "Function(", "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "fetch", "XMLHttpRequest",
            "WebSocket", "EventSource", "sendBeacon", "import(", "localStorage", "sessionStorage", "indexedDB", "window.open", "postMessage" })
            Assert.DoesNotContain(token, script, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(?:setTimeout|setInterval)\s*\(\s*['""`]", script);
        Assert.DoesNotMatch(@"\b(?:Function|import)\s*\(", script);
        Assert.DoesNotMatch(@"textContent\s*=\s*['""`]", script);
        Assert.DoesNotContain("</script", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status.textContent = status.dataset.labelDone", script, StringComparison.Ordinal);
        Assert.Contains("status.textContent = status.dataset.labelSelected", script, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(script) < 12 * 1024);
    }

    [Fact]
    public void MarkupHasNoInlineHandlersOrExecutableUrlsAndScriptControlsStartHidden()
    {
        string html = TestFailureMarkup.WithoutScripts(TestFailureReportFixture.Render("hostile"));
        foreach (Match tag in TestFailureMarkup.Tags().Matches(html))
        {
            // Remove quoted values so hostile display text is never confused with attribute names.
            string names = Regex.Replace(tag.Value, "\"[^\"]*\"|'[^']*'", "\"\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            Assert.DoesNotMatch(@"\son[a-z]+\s*=", names);
            Assert.DoesNotMatch("(?:href|src)=\"javascript:", tag.Value);
            if (tag.Value.StartsWith("<button", StringComparison.Ordinal) || tag.Value.Contains("data-enhance", StringComparison.Ordinal))
                Assert.Contains(" hidden", tag.Value, StringComparison.Ordinal);
        }
        Assert.Contains("<div class=\"interactive filter-controls\" data-enhance hidden>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }
}
