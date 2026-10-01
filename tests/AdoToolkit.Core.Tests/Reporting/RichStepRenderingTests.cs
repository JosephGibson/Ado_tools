using System.Text.Json;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

public sealed class RichStepRenderingTests
{
    [Fact]
    public void FormattedStepKeepsItsTableListAndInlineFormattingInTheHtmlReport()
    {
        string html = RichStepFixture.Render();
        string action = RichStepFixture.Cell(html, "action");
        Assert.Contains("Fill the <strong>order</strong> form as <em>@user</em>, then <u>save</u>:", action, StringComparison.Ordinal);
        Assert.Contains("<ol><li>Open <strong>Orders</strong><ul><li>New order</li></ul></li><li>Enter the values below</li></ol>", action, StringComparison.Ordinal);
        Assert.Contains("<table><tbody><tr><th>Field</th><th>Value</th></tr><tr><td>Quantity</td><td>2 &lt; 3 &amp; units (", action, StringComparison.Ordinal);
        Assert.Contains("</td></tr></tbody></table>", action, StringComparison.Ordinal);
        string expected = RichStepFixture.Cell(html, "expected");
        Assert.Contains("<p>The total is <strong>42,00 $</strong>.</p>", expected, StringComparison.Ordinal);
        Assert.Contains("<ul><li>Status: <em>Ready</em></li><li>No error</li></ul>", expected, StringComparison.Ordinal);
        // The flattened text of the other formats is not what the HTML report shows.
        Assert.DoesNotContain("Field | Value", action, StringComparison.Ordinal);
        Assert.DoesNotContain("1. Open Orders", action, StringComparison.Ordinal);
        Assert.DoesNotContain("- Status: Ready", expected, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownAndJsonKeepThePlainTextOfTheSameStep()
    {
        string markdown = RichStepFixture.Render(format: ReportFormat.Markdown);
        Assert.Contains("1\\. Open Orders", markdown, StringComparison.Ordinal);
        Assert.Contains("Field \\| Value", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("<table>", markdown, StringComparison.Ordinal);
        using JsonDocument json = JsonDocument.Parse(RichStepFixture.Render(format: ReportFormat.Json));
        JsonElement row = json.RootElement.GetProperty("cases")[0].GetProperty("rows")[0];
        Assert.Equal("Fill the order form as @user, then save:\n1. Open Orders\n  - New order\n2. Enter the values below\nField | Value\nQuantity | 2 < 3 & units (https://docs.example.test/units)",
            row.GetProperty("action").GetString());
        Assert.False(row.TryGetProperty("actionSource", out _));
    }

    [Fact]
    public void ALinkShowsItsAddressAndOnlySafeSchemesAreLinked()
    {
        string action = RichStepFixture.Cell(RichStepFixture.Render(), "action");
        // As in plain text, the address follows the link text, so the target is always visible.
        Assert.Contains("units (<a rel=\"noreferrer\" href=\"https://docs.example.test/units\">https://docs.example.test/units</a>)", action, StringComparison.Ordinal);
        string unsafeLinks = RichStepFixture.Cell(RichStepFixture.Render(RichStepFixture.Xml(
            "<p><a href=\"javascript:alert(1)\">Run</a> <a href=\"data:text/html,x\">Data</a> <a href=\"file:///C:/x\">File</a> "
            + "<a href=\"mailto:qa@example.test\">Mail</a> <a href=\"/relative\">Relative</a></p>")), "action");
        Assert.Contains("<p>Run Data File Mail Relative</p>", unsafeLinks, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", unsafeLinks, StringComparison.Ordinal);
    }

    [Fact]
    public void TextThatWasNotFormattedKeepsItsCharactersAndLines()
    {
        string html = RichStepFixture.Render(RichStepFixture.Xml("Type <b> & press\nEnter", "Seen <ul><li>literally</li></ul>", formatted: false));
        Assert.Contains("<div class=\"action\"><h5>Action</h5>Type &lt;b&gt; &amp; press<br>Enter</div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"expected\"><h5>Expected Result</h5>Seen &lt;ul&gt;&lt;li&gt;literally&lt;/li&gt;&lt;/ul&gt;</div>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rich-text", html[html.IndexOf("<main", StringComparison.Ordinal)..html.IndexOf("</main>", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("<p>Original &amp; @user</p>")]
    [InlineData("<script>alert(1)</script><table><tr><td>Unrelated</td></tr></table>")]
    public void SourceThatIsNotTheOriginOfTheStepTextIsNotRendered(string? source)
    {
        // A hand-made object can pair any source with any text; the report then shows the text.
        AdoTestCase testCase = new()
        {
            Id = 10, Rev = 3, Title = "Synthetic", WorkItemType = "Test Case", State = "Ready", TeamProject = ReportFixture.Project,
            CollectionUri = ReportFixture.Collection, WebUrl = ReportFixture.Untrusted,
            Steps = [new() { Number = "1", Sequence = 1, Action = "Enter @user", ActionSource = source, ExpectedResult = "", ExpectedResultSource = source }],
        };
        string html = GoldenReportTests.Render(ReportModelBuilder.Build(testCase, new AdoConnection { CollectionUri = ReportFixture.Collection },
            ReportFixture.Options()), ReportFormat.Html);
        Assert.Contains("<div class=\"action\"><h5>Action</h5>Enter @user</div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"expected\"><h5>Expected Result</h5></div>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Original", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Unrelated", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AFormattedValueWithoutTextRendersAnEmptyCell()
    {
        // The editor stores an empty expected result as markup without text.
        string html = RichStepFixture.Render(RichStepFixture.Xml("<P>Open</P>", "<DIV><P><BR/></P></DIV>"));
        Assert.Contains("<div class=\"action\"><h5>Action</h5><div class=\"rich-text\"><p>Open</p></div></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"expected\"><h5>Expected Result</h5></div>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AStepConvertedInAnotherCultureIsStillRenderedFromItsMarkup()
    {
        // The image text is the only localized part of the conversion: the case was read in English.
        string xml = RichStepFixture.Xml("<p>See <img alt=\"Diagram\"> and <img></p>");
        string html = GoldenReportTests.Render(RichStepFixture.Model(xml, "fr-CA"), ReportFormat.Html);
        Assert.Contains("<p>See <span class=\"rich-image\">[image : Diagram]</span> and <span class=\"rich-image\">[image]</span></p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("src=", html[html.IndexOf("<main", StringComparison.Ordinal)..html.IndexOf("</main>", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void DeclaredParametersAreMarkedInFormattedAndPlainText()
    {
        AdoTestParameters parameters = new()
        {
            Source = AdoParameterSource.Local, Names = ["user", "user name", "résultat"],
            Rows = [new Dictionary<string, string>(StringComparer.Ordinal) { ["user"] = "alice", ["résultat"] = "42" }],
        };
        string formatted = RichStepFixture.Render(RichStepFixture.Xml("<p>Sign in as <i>@USER</i>, mail qa@user.test, @username, @user name: @résultat.</p>"), parameters: parameters);
        Assert.Contains("<em><span class=\"param\" data-param=\"0\">@USER</span></em>, mail qa@user.test, @username, "
            + "<span class=\"param\" data-param=\"1\">@user name</span>: <span class=\"param\" data-param=\"2\">@résultat</span>.</p>", formatted, StringComparison.Ordinal);
        string plain = RichStepFixture.Render(RichStepFixture.Xml("Sign in as @user\n@other @@user", "", formatted: false), parameters: parameters);
        Assert.Contains("<h5>Action</h5>Sign in as <span class=\"param\" data-param=\"0\">@user</span><br>@other @@user</div>", plain, StringComparison.Ordinal);
        // The select that shows the values of an iteration is offered to the script only.
        Assert.Contains("<label class=\"interactive iteration-select\" data-enhance hidden>Show the values of <select data-iteration><option value=\"\">Parameter names</option>"
            + "<option value=\"0\">Iteration 1</option></select></label>", plain, StringComparison.Ordinal);
        Assert.Contains("<tbody data-parameters>\n<tr><td>alice</td><td></td><td>42</td></tr>\n</tbody>", plain, StringComparison.Ordinal);
        // Without declared parameters nothing is marked.
        Assert.DoesNotContain("class=\"param\"", RichStepFixture.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void MarkupOfAStepCannotAddMarkersThatTheCommitValidationCounts()
    {
        string xml = RichStepFixture.Xml("<pre>line\n&lt;div class=\"step-card\"&gt;\n&lt;/article&gt;</pre><div class=\"step-card\">x</div>\n<article class=\"test-case\">y</article>");
        ReportDocumentModel model = RichStepFixture.Model(xml);
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.html");
        File.WriteAllText(path, GoldenReportTests.Render(model, ReportFormat.Html));
        TestCaseExporter.Validate(path, model, ReportFormat.Html);
        string action = RichStepFixture.Cell(File.ReadAllText(path), "action");
        Assert.DoesNotContain("class=\"step-card\"", action, StringComparison.Ordinal);
        Assert.DoesNotContain("<article", action, StringComparison.Ordinal);
    }
}
