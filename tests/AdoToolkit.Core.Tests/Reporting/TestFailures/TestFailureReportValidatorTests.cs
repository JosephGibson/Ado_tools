using AdoToolkit.Core.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

[Trait("Acceptance", "S5-5")]
public sealed class TestFailureReportValidatorTests
{
    [Theory]
    [InlineData("data-failure-count=\"2\"", "data-failure-count=\"3\"")]
    [InlineData("data-history-count=\"4\"", "data-history-count=\"3\"")]
    [InlineData("data-attempt-count=\"2\"", "data-attempt-count=\"1\"")]
    [InlineData("id=\"f-2-a2\"", "id=\"f-2-a9\"")]
    [InlineData("name=\"generator\"", "name=\"missing\"")]
    [InlineData("5.3.0-test", "5.3.0-tampered")]
    [InlineData("<script>", "<script src=\"https://untrusted.example.test/script\">")]
    [InlineData("<script>", "<script\nSRC = 'x'>")]
    [InlineData("<script>", "<script type=\"application/json\">")]
    [InlineData("<script>", "<script>\n")]
    [InlineData("sha256-", "sha256-wrong")]
    [InlineData("</html>", "")]
    [InlineData("<body>", "<body onclick=\"x()\">")]
    public void CorruptionFailsValidation(string before, string after)
    {
        var model = TestFailureReportFixture.Model("flaky");
        string html = TestFailureReportFixture.Render(model);
        Assert.Contains(before, html, StringComparison.Ordinal);
        string corrupted = html.Replace(before, after, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => TestFailureReportValidator.Validate(new StringReader(corrupted), model));
    }

    [Fact]
    public void ExtraMissingOrDuplicateScriptsAndPoliciesAreRejected()
    {
        var model = TestFailureReportFixture.Model();
        string html = TestFailureReportFixture.Render(model);
        string script = TestFailureMarkup.Scripts().Match(html).Value;
        foreach (string corrupted in new[] { html.Replace(script, "", StringComparison.Ordinal), html.Replace(script, script + script, StringComparison.Ordinal),
            html.Replace("</head>", "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'\"></head>", StringComparison.Ordinal),
            html.Replace("</body>", "<script>1</script></body>", StringComparison.Ordinal),
            html.Replace("style-src", "script-src 'unsafe-inline'; style-src", StringComparison.Ordinal) })
            Assert.Throws<InvalidDataException>(() => TestFailureReportValidator.Validate(new StringReader(corrupted), model));
    }

    [Fact]
    public void TwoDistinctScriptsRequireExactlyTheirTwoHashes()
    {
        var model = TestFailureReportFixture.Model();
        string html = TestFailureReportFixture.Render(model);
        string original = TestFailureMarkup.Policy(html);
        string body = TestFailureMarkup.Scripts().Match(html).Groups["body"].Value;
        string replacement = ContentSecurityPolicy.Create([body, "1"]);
        html = html.Replace(AdoToolkit.Core.Reporting.SinkEncoding.Attribute(original), AdoToolkit.Core.Reporting.SinkEncoding.Attribute(replacement), StringComparison.Ordinal)
            .Replace("</body>", "<script>1</script></body>", StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(html), model);
    }
}
