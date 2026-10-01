using System.Xml.Linq;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

// One formatted Validate step, read by the real parser: inline formatting, a nested list, a table
// and a link in the action; a paragraph and a list in the expected result.
internal static class RichStepFixture
{
    internal const string Action = "<div><p>Fill the <b>order</b> form as <i>@user</i>, then <u>save</u>:</p>"
        + "<ol><li>Open <strong>Orders</strong><ul><li>New order</li></ul></li><li>Enter the values below</li></ol>"
        + "<table><tr><th>Field</th><th>Value</th></tr><tr><td>Quantity</td>"
        + "<td>2 &lt; 3 &amp; <a href=\"https://docs.example.test/units\">units</a></td></tr></table></div>";
    internal const string Expected = "<p>The total is <b>42,00 $</b>.</p><ul><li>Status: <em>Ready</em></li><li>No error</li></ul>";

    // The second step of the reviewed report: a heading, preformatted text, a quote, a styled span, an
    // image, markup escaped twice and a table with a spanning cell.
    internal const string SecondAction = "<h3>Checks</h3><pre>GET /orders\n  Accept: application/json</pre><blockquote>Quoted note</blockquote>"
        + "<p><span style=\"font-weight:bold\">Bold span</span> <img alt=\"Diagram\" src=\"https://images.example.test/a.png\"> and &lt;b&gt;escaped twice&lt;/b&gt;</p>";
    internal const string SecondExpected = "<table><tr><td colspan=\"2\">Totals</td></tr><tr><td>A</td><td>B</td></tr></table>";

    internal static string Xml(string action = Action, string expected = Expected, bool formatted = true) =>
        new XElement("steps", Step(2, action, expected, formatted)).ToString(SaveOptions.DisableFormatting);

    // Two formatted steps and one that is not, with a parameter and two iterations.
    internal static AdoTestCase GoldenCase() => Case(new XElement("steps", Step(2, Action, Expected, true), Step(3, SecondAction, SecondExpected, true),
        Step(4, "Type <b> as @user\nthen open https://docs.example.test/help", "", false)).ToString(SaveOptions.DisableFormatting),
        new AdoTestParameters
        {
            Source = AdoParameterSource.Local, Names = ["user"],
            Rows = [new Dictionary<string, string>(StringComparer.Ordinal) { ["user"] = "alice" }, new Dictionary<string, string>(StringComparer.Ordinal) { ["user"] = "bob <admin>" }],
        });

    private static XElement Step(int id, string action, string expected, bool formatted) =>
        new("step", new XAttribute("id", id), new XAttribute("type", "ValidateStep"), Value(action, formatted), Value(expected, formatted));

    internal static AdoTestCase Case(string xml, AdoTestParameters? parameters = null)
    {
        StepDocument document = StepsXmlParser.Parse(xml, 10, 3, CultureInfo.GetCultureInfo("en-US"));
        return new AdoTestCase
        {
            Id = 10, Rev = 3, Title = "Synthetic formatted case", WorkItemType = "Test Case", State = "Ready",
            TeamProject = ReportFixture.Project, CollectionUri = ReportFixture.Collection, WebUrl = ReportFixture.Untrusted,
            ChangedDate = ReportFixture.Timestamp.AddDays(-2), RetrievedAt = ReportFixture.Timestamp.AddMinutes(-1),
            Parameters = parameters ?? new(),
            Steps = document.Nodes.Select((node, index) => new AdoTestStep
            {
                Sequence = index + 1, Number = (index + 1).ToString(CultureInfo.InvariantCulture), Kind = node.Kind,
                Action = node.Action, ExpectedResult = node.ExpectedResult, ActionSource = node.ActionSource,
                ExpectedResultSource = node.ExpectedResultSource, SourceWorkItemId = 10, SourceRev = 3, SourceStepId = node.SourceStepId,
            }).ToArray(),
        };
    }

    internal static ReportDocumentModel Model(string? xml = null, string culture = "en-US", AdoTestParameters? parameters = null) =>
        ReportModelBuilder.Build(Case(xml ?? Xml(), parameters), new AdoConnection { CollectionUri = ReportFixture.Collection },
            ReportFixture.Options(culture));

    internal static string Render(string? xml = null, ReportFormat format = ReportFormat.Html, AdoTestParameters? parameters = null) =>
        GoldenReportTests.Render(Model(xml, parameters: parameters), format);

    // The content of the first step's action or expected-result cell.
    internal static string Cell(string html, string name)
    {
        int start = html.IndexOf("<div class=\"" + name + "\">", StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing cell: " + name);
        int end = name == "action" ? html.IndexOf("<div class=\"expected\">", start, StringComparison.Ordinal)
            : html.IndexOf("</div>\n</div>\n", start, StringComparison.Ordinal);
        Assert.True(end > start, "Unterminated cell: " + name);
        return html[start..end];
    }

    private static XElement Value(string text, bool formatted)
    {
        XElement value = new("parameterizedString", text);
        if (formatted) value.SetAttributeValue("isformatted", "true");
        return value;
    }
}
