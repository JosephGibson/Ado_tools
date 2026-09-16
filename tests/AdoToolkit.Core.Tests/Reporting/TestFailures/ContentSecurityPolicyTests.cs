using System.Security.Cryptography;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

[Trait("Acceptance", "S5-5")]
public sealed class ContentSecurityPolicyTests
{
    [Theory]
    [InlineData("failed", "en-US")]
    [InlineData("flaky", "fr-CA")]
    [InlineData("partial", "en-US")]
    [InlineData("hostile", "fr-CA")]
    public void ExactWrittenUtf8BytesHaveOneHashAndNoOtherScriptSources(string variant, string culture)
    {
        var model = TestFailureReportFixture.Model(variant, culture);
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.html");
        new AtomicFileWriter().Write(path, writer => HtmlTestFailureRenderer.Render(model, writer),
            temporary => TestFailureReportValidator.Validate(temporary, model), model.Culture, cancellationToken: TestContext.Current.CancellationToken);
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
        Assert.Equal(TestFailureAssets.Read("test-failures.js"), body);
        Assert.DoesNotContain(TestFailureReportFixture.Hostile, body, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFailureReportFixture.Project, body, StringComparison.Ordinal);
        Assert.DoesNotContain(model.ToolkitVersion, body, StringComparison.Ordinal);
        Assert.True(html.IndexOf("Content-Security-Policy", StringComparison.Ordinal) < html.IndexOf("<style>", StringComparison.Ordinal));
    }

    [Fact]
    public void HashingHonorsEveryByteIncludingLineEndingsAndUnicode()
    {
        foreach (string script in new[] { "a\nb", "a\r\nb", "é日本語\n", "a\nb\n" })
        {
            string expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            Assert.Contains("'sha256-" + expected + "'", ContentSecurityPolicy.Create([script]), StringComparison.Ordinal);
        }
        Assert.NotEqual(ContentSecurityPolicy.Create(["a\nb"]), ContentSecurityPolicy.Create(["a\r\nb"]));
        Assert.Equal(2, ContentSecurityPolicy.Create(["a", "b"]).Split("sha256-", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void StaticScriptAndPlaceholderDoNotVaryByReportOrCulture()
    {
        string? expected = null;
        foreach (string variant in new[] { "failed", "flaky", "partial", "hostile" })
        foreach (string culture in new[] { "en-US", "fr-CA" })
        {
            string html = TestFailureReportFixture.Render(variant, culture);
            string script = TestFailureMarkup.Scripts().Match(html).Groups["body"].Value;
            expected ??= script;
            Assert.Equal(expected, script);
            string normalized = TestFailureMarkup.NormalizeGolden(html);
            Assert.Contains("<script>__SCRIPT_ASSET__</script>", normalized, StringComparison.Ordinal);
            Assert.Contains("sha256-__SCRIPT_SHA256__", normalized, StringComparison.Ordinal);
            Assert.DoesNotContain(script, normalized, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GoldenPlaceholderDoesNotRewriteHashLikeRemoteText()
    {
        string html = TestFailureReportFixture.Render().Replace("</main>", "<p>sha256-RemoteText</p></main>", StringComparison.Ordinal);
        Assert.Contains("<p>sha256-RemoteText</p>", TestFailureMarkup.NormalizeGolden(html), StringComparison.Ordinal);
    }
}
