using System.Xml.Linq;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.RichText;

namespace AdoToolkit.Core.Tests.RichText;

[Trait("Acceptance", "S1-5")]
public sealed class PlainTextConverterTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData("06-rich.xml", "Tasks\n1. Open\n  - Nested\n2. Save\nName | Value\nA | B\nGuide (https://docs.example.test/guide) [image: Diagram] [image]", "Ready\nA B")]
    [InlineData("07-nested.xml", "Open", "")]
    [InlineData("08-literal-entity.xml", "Type now", "")]
    [InlineData("09-french.xml", "Écrire l’action «\u00a0cafe\u0301\u202f»\u00a0: oui\u00a0; prêt\u202f! sûr\u00a0? 1\u202f234 — été fin 😀 日本語", "")]
    [InlineData("26-hostile.xml", "Safe label[image]", "")]
    [InlineData("29-urls.xml", "HTTP (http://docs.example.test/a) HTTPS (https://docs.example.test/b) Mail JS Data File http://docs.example.test/a https://docs.example.test/b mailto:sample@example.test", "")]
    public void RichFixturesMatchPlainText(string fixture, string action, string expected)
    {
        XElement[] values = SafeXml.Parse(ParserFixture.Read("Steps/" + fixture)).Root!.Element("step")!.Elements("parameterizedString").ToArray();
        Assert.Equal(action, PlainTextConverter.Convert(values[0].Value, English).Text);
        Assert.Equal(expected, PlainTextConverter.Convert(values[1].Value, English).Text);
    }

    [Theory]
    [InlineData("<p>Hello</p>", "Hello", false)]
    [InlineData("&lt;b&gt;Hello&lt;/b&gt;", "Hello", true)]
    [InlineData("&amp;amp;", "&", true)]
    [InlineData("Already plain", "Already plain", false)]
    [InlineData("<!--hidden--><script>hidden</script><style>hidden</style><head>hidden</head>Safe", "Safe", false)]
    [InlineData("<SCRIPT>hidden</SCRIPT>Safe<style>unterminated", "Safe", false)]
    [InlineData("<p title='a > b'>A</p><p title=unquoted>B<br>C</p>", "A\nB\nC", false)]
    [InlineData("<div>A\t  B<br><br><br><br>C  </div>", "A B\n\nC", false)]
    [InlineData("a\r\nb\rc", "a\nb\nc", false)]
    [InlineData("<ol><li>A</li><li>B<ol><li>C</li></ol></li></ol><ol><li>D</li></ol>", "1. A\n2. B\n  1. C\n1. D", false)]
    [InlineData("<a href='https://docs.example.test/a'>https://docs.example.test/a</a>", "https://docs.example.test/a", false)]
    [InlineData("<a href='/relative'>Relative</a> <a href='mailto:sample@example.test'>Mail</a>", "Relative Mail", false)]
    [InlineData("<a href='https://docs.example.test/?a=1&amp;b=2'>Link</a>", "Link (https://docs.example.test/?a=1&b=2)", false)]
    [InlineData("<img alt='A &amp; B' onerror='hidden()'/>", "[image: A & B]", false)]
    [InlineData("2 < 3 & 4 > 1", "2 < 3 & 4 > 1", false)]
    [InlineData("<p title='unfinished", "<p title='unfinished", false)]
    [InlineData("A<!--unterminated", "A", false)]
    [InlineData("<unknown>A</unknown><br/>B", "A\nB", false)]
    [InlineData("<ul><li><p>A</p></li><li><div>B</div></li></ul>", "- A\n- B", false)]
    [InlineData("<table><tr><td><p>A</p><p>B</p></td><td><div>C</div></td></tr></table>", "A B | C", false)]
    public void TagAndWhitespaceRulesAreStable(string input, string expected, bool nested)
    {
        PlainTextResult result = PlainTextConverter.Convert(input, English, 42);
        Assert.Equal(expected, result.Text);
        Assert.Equal(nested, result.Diagnostics.Count != 0);
        if (nested)
        {
            AdoDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticCodes.NestedEncodingDecoded, diagnostic.Code);
            Assert.Equal(AdoDiagnosticSeverity.Info, diagnostic.Severity);
            Assert.Equal(42, diagnostic.WorkItemId);
        }
        Assert.Equal(expected, PlainTextConverter.Convert(result.Text, English).Text);
    }

    [Fact]
    public void NestedEncodingStopsAfterEightPasses()
    {
        string input = "&";
        for (int i = 0; i < 10; i++) input = input.Replace("&", "&amp;", StringComparison.Ordinal);
        PlainTextResult result = PlainTextConverter.Convert(input, English);
        Assert.Equal("&amp;amp;", result.Text);
        Assert.Single(result.Diagnostics);
    }

    [Theory]
    [InlineData("\u00a0")]
    [InlineData("\u202f")]
    public void FrenchSpacesArePreservedByContextRegardlessOfUiCulture(string space)
    {
        string input = $"A{space}: B{space}; C{space}! D{space}? «{space}E{space}» 1{space}234 X{space}Y";
        string expected = $"A{space}: B{space}; C{space}! D{space}? «{space}E{space}» 1{space}234 X Y";
        Assert.Equal(expected, PlainTextConverter.Convert(input, English).Text);
        Assert.Equal(expected, PlainTextConverter.Convert(input, CultureInfo.GetCultureInfo("fr-CA")).Text);
    }

    [Fact]
    public void FrenchImageTextUsesExplicitResources()
    {
        Assert.Equal("[image : Schéma]", PlainTextConverter.Convert("<img alt='Schéma'/>", CultureInfo.GetCultureInfo("fr-CA")).Text);
        Assert.Equal(string.Empty, PlainTextConverter.Convert(null, English).Text);
    }
}
