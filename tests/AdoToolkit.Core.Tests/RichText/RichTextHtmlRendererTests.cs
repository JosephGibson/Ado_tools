using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.RichText;

namespace AdoToolkit.Core.Tests.RichText;

public sealed partial class RichTextHtmlRendererTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
    {
        "p", "div", "blockquote", "pre", "ul", "ol", "li", "table", "thead", "tbody", "tfoot", "tr", "td", "th",
        "strong", "em", "u", "s", "code", "sub", "sup", "span", "br", "hr",
    };

    [Theory]
    [InlineData("<p>Hello</p>", "<p>Hello</p>")]
    [InlineData("<P>A<BR>B<br/>C</P>", "<p>A<br>B<br>C</p>")]
    [InlineData("<div>A</div><div>B</div>", "<div>A</div><div>B</div>")]
    [InlineData("  <p>  A \t\r\n B  </p>  ", "<p>A B </p>")]
    [InlineData("<b>bold</b> <i>italic</i> <u>under</u> <s>gone</s> <code>x</code> H<sub>2</sub>O x<sup>2</sup>",
        "<strong>bold</strong> <em>italic</em> <u>under</u> <s>gone</s> <code>x</code> H<sub>2</sub>O x<sup>2</sup>")]
    [InlineData("<strong>a</strong><em>b</em><ins>c</ins><strike>d</strike><del>e</del><tt>f</tt>", "<strong>a</strong><em>b</em><u>c</u><s>d</s><s>e</s><code>f</code>")]
    [InlineData("<h1>Title</h1><h6>Small</h6>", "<p class=\"rich-heading\">Title</p><p class=\"rich-heading\">Small</p>")]
    [InlineData("<blockquote>Quote</blockquote><hr><pre>A   B\n  C</pre>", "<blockquote>Quote</blockquote><hr><pre>A   B\n  C</pre>")]
    [InlineData("<ul><li>A</li><li>B<ol start=\"3\"><li>C</li></ol></li></ul>", "<ul><li>A</li><li>B<ol start=\"3\"><li>C</li></ol></li></ul>")]
    [InlineData("<table><thead><tr><th>H</th></tr></thead><tbody><tr><td colspan=\"2\" rowspan=\"3\">A</td></tr></tbody></table>",
        "<table><thead><tr><th>H</th></tr></thead><tbody><tr><td colspan=\"2\" rowspan=\"3\">A</td></tr></tbody></table>")]
    [InlineData("<span style=\"font-weight:bold; font-style: italic\">x</span>", "<strong><em>x</em></strong>")]
    [InlineData("<span style=\"font-weight: 700; text-decoration: underline\">x</span><span style=\"FONT-WEIGHT:400\">y</span>", "<strong><u>x</u></strong>y")]
    [InlineData("<span style=\"text-decoration:line-through; color: red; background:url(https://images.example.test/a)\">x</span>", "<s>x</s>")]
    [InlineData("<font color=\"red\" size=\"7\">x</font>", "x")]
    public void AllowedStructureAndFormattingAreKept(string source, string expected) => Assert.Equal(expected, Render(source));

    [Theory]
    // Unclosed, crossed and stray tags.
    [InlineData("<b>bold", "<strong>bold</strong>")]
    [InlineData("<b><i>x</b>y</i>", "<strong><em>x</em></strong>y")]
    [InlineData("text</b></p></div></li></ul></td></tr></table>", "text")]
    [InlineData("<p>A<p>B", "<p>A</p><p>B</p>")]
    [InlineData("<p>A<ul><li>B</li></ul>C</p>", "<p>A</p><ul><li>B</li></ul>C")]
    [InlineData("<p><b>A<div>B</div>C</b></p>", "<p><strong>A</strong></p><div>B</div>C")]
    // Lists.
    [InlineData("<ul><li>A<li>B</ul>", "<ul><li>A</li><li>B</li></ul>")]
    [InlineData("<li>A</li><li>B</li>", "<ul><li>A</li><li>B</li></ul>")]
    [InlineData("<ul>text<b>bold</b></ul>", "<ul><li>text<strong>bold</strong></li></ul>")]
    [InlineData("<ul> <li>A</li> </ul>", "<ul><li>A</li></ul>")]
    [InlineData("<ul><ul><li>A</li></ul></ul>", "<ul><li><ul><li>A</li></ul></li></ul>")]
    [InlineData("<ul><li>A<ul><li>B</li></li></ul><li>C</li></ul>", "<ul><li>A<ul><li>B</li></ul></li><li>C</li></ul>")]
    // Tables.
    [InlineData("<td>A</td><td>B</td>", "<table><tbody><tr><td>A</td><td>B</td></tr></tbody></table>")]
    [InlineData("<tr><td>A</td></tr>", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [InlineData("<tbody><tr><td>A</td></tr></tbody>", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [InlineData("<table><tr><td>A<tr><td>B</table>", "<table><tbody><tr><td>A</td></tr><tr><td>B</td></tr></tbody></table>")]
    [InlineData("<table>text<tr>more<td>A</td></tr></table>", "<table><tbody><tr><td>text</td></tr><tr><td>more</td><td>A</td></tr></tbody></table>")]
    [InlineData("<table> <tr> <td>A</td> </tr> </table>", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [InlineData("<table><tr><td><table><tr><td>in</td></tr></table>out</td></tr></table>",
        "<table><tbody><tr><td><table><tbody><tr><td>in</td></tr></tbody></table>out</td></tr></tbody></table>")]
    [InlineData("<table><tr><td><ul><li>A</td><td>B</td></tr></table>", "<table><tbody><tr><td><ul><li>A</li></ul></td><td>B</td></tr></tbody></table>")]
    [InlineData("<table><tr><td>A</td></tr><thead><tr><th>H</th></tr></thead></table>",
        "<table><tbody><tr><td>A</td></tr></tbody><thead><tr><th>H</th></tr></thead></table>")]
    [InlineData("<table><tr><td>A</td></tr></table></td></tr>after", "<table><tbody><tr><td>A</td></tr></tbody></table>after")]
    public void MalformedNestingIsRepaired(string source, string expected)
    {
        string rendered = Render(source);
        Assert.Equal(expected, rendered);
        AssertBalancedAllowlist(rendered);
    }

    [Theory]
    [InlineData("<script>alert(1)</script><style>p{}</style><head><title>t</title></head><!--c-->Safe", "Safe")]
    [InlineData("<SCRIPT>hidden</SCRIPT>Safe<style>unterminated", "Safe")]
    [InlineData("<p onclick=\"x()\" style=\"background:url(https://images.example.test/a)\" id=\"report-1-steps\" class=\"step-card\">A</p>", "<p>A</p>")]
    [InlineData("<td colspan=\"0\" rowspan=\"1001\" width=\"99\">A</td>", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [InlineData("<td colspan=\"2&quot; onmouseover=&quot;x()\">A</td>", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [InlineData("<ol start=\"-1\" type=\"a\"><li>A</li></ol>", "<ol><li>A</li></ol>")]
    [InlineData("<iframe src=\"https://images.example.test/\"></iframe><object data=\"x\"></object><form action=\"x\"><input value=\"v\"><button>Go</button></form>", "Go")]
    [InlineData("<svg onload=\"x()\"><text>T</text></svg><math><mi>m</mi></math>", "Tm")]
    [InlineData("<unknown onclick=\"x\">A</unknown><x-y>B</x-y>", "AB")]
    [InlineData("<img src=\"https://images.example.test/a.png\" onerror=\"x()\">", "<span class=\"rich-image\">[image]</span>")]
    [InlineData("<img alt=\"A &amp; <b>\" src=\"data:image/png;base64,AAAA\">", "<span class=\"rich-image\">[image: A &amp; &lt;b&gt;]</span>")]
    [InlineData("<a href=\"javascript:alert(1)\" onclick=\"x()\">Run</a>", "Run")]
    [InlineData("<a href=\"https://docs.example.test/a\">Guide</a>", "Guide (https://docs.example.test/a)")]
    [InlineData("<a href=\"https://docs.example.test/a\">https://docs.example.test/a</a>", "https://docs.example.test/a")]
    [InlineData("<a href='https://docs.example.test/?a=1&amp;b=2'><b>Link</b></a>", "<strong>Link</strong> (https://docs.example.test/?a=1&amp;b=2)")]
    [InlineData("<p><a href=\"https://docs.example.test/a\">Open</p>next", "<p>Open (https://docs.example.test/a)</p>next")]
    [InlineData("2 < 3 & 4 > 1", "2 &lt; 3 &amp; 4 &gt; 1")]
    [InlineData("<p title='unfinished", "&lt;p title=&#x27;unfinished")]
    public void NothingOfTheSourceIsCopiedExceptEncodedText(string source, string expected)
    {
        string rendered = Render(source);
        Assert.Equal(expected, rendered);
        AssertBalancedAllowlist(rendered);
    }

    [Theory]
    // §11.3: markup that is escaped more than once is markup on a later pass of the plain converter.
    [InlineData("&lt;b&gt;Hello&lt;/b&gt;", "<strong>Hello</strong>")]
    [InlineData("&amp;lt;b&amp;gt;Open&amp;lt;/b&amp;gt;", "<strong>Open</strong>")]
    [InlineData("<p>Type &lt;b&gt;now&lt;/b&gt;</p>", "<p>Type <strong>now</strong></p>")]
    [InlineData("<div>&lt;table&gt;&lt;tr&gt;&lt;td&gt;A&lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</div>", "<div><table><tbody><tr><td>A</td></tr></tbody></table></div>")]
    [InlineData("&lt;script&gt;alert(1)&lt;/script&gt;Safe", "Safe")]
    [InlineData("&amp;amp;", "&amp;")]
    [InlineData("2 &lt; 3 &amp;amp; 4", "2 &lt; 3 &amp; 4")]
    public void MarkupEscapedMoreThanOnceIsReadAgain(string source, string expected)
    {
        string rendered = Render(source);
        Assert.Equal(expected, rendered);
        AssertBalancedAllowlist(rendered);
        // The text is that of the other formats.
        Assert.Equal(Letters(PlainTextConverter.Convert(source, English).Text), Letters(TagPattern().Replace(rendered, "")));
    }

    [Fact]
    public void EscapingStopsAfterEightPassesLikeThePlainConverter()
    {
        string source = "&";
        for (int index = 0; index < 10; index++) source = source.Replace("&", "&amp;", StringComparison.Ordinal);
        Assert.Equal(SinkEncoding.Attribute(PlainTextConverter.Convert(source, English).Text), Render(source));
    }

    [Theory]
    [InlineData("06-rich.xml")]
    [InlineData("07-nested.xml")]
    [InlineData("08-literal-entity.xml")]
    [InlineData("09-french.xml")]
    [InlineData("26-hostile.xml")]
    [InlineData("29-urls.xml")]
    public void StepFixturesRenderBalancedMarkupWithTheWordsOfTheirPlainText(string fixture)
    {
        foreach (System.Xml.Linq.XElement value in AdoToolkit.Core.IO.SafeXml.Parse(ParserFixture.Read("Steps/" + fixture)).Root!.Element("step")!.Elements("parameterizedString"))
        {
            string rendered = Render(value.Value);
            AssertBalancedAllowlist(rendered);
            Assert.Equal(Letters(PlainTextConverter.Convert(value.Value, English).Text), Letters(TagPattern().Replace(rendered, "")));
            Assert.True(RichTextHtmlRenderer.IsSourceOf(value.Value, PlainTextConverter.Convert(value.Value, English).Text) || value.Value.Length == 0
                || string.Equals(value.Value, PlainTextConverter.Convert(value.Value, English).Text, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void NestingDeeperThanTheLimitKeepsTheTextAndStaysBalanced()
    {
        string source = string.Concat(Enumerable.Repeat("<div><b>", 500)) + "deep" + string.Concat(Enumerable.Repeat("</b></div>", 500))
            + string.Concat(Enumerable.Repeat("<table><tr><td>", 200)) + "cell" + string.Concat(Enumerable.Repeat("<ul><li>", 200)) + "item";
        string rendered = Render(source);
        AssertBalancedAllowlist(rendered);
        Assert.Contains("deep", rendered, StringComparison.Ordinal);
        Assert.Contains("cell", rendered, StringComparison.Ordinal);
        Assert.Contains("item", rendered, StringComparison.Ordinal);
        Assert.InRange(Regex.Count(rendered, "<div>"), 1, 64);
        Assert.InRange(Regex.Count(rendered, "<table>"), 1, 64);
    }

    [Fact]
    public void TextRunsReachTheSinkDecodedOnceAndWhitespaceIsCollapsedOutsidePreformattedText()
    {
        List<string> runs = [];
        string rendered = RichTextHtmlRenderer.Render("<p>A &amp;  B\n C&nbsp;: D&nbsp;E</p><pre>x  y\r\nz</pre>", English, run => { runs.Add(run); return "[" + run + "]"; });
        // French spacing before a colon is kept; another no-break space becomes a space, as in plain text.
        Assert.Equal(["A & B C : D E", "x  y", "z"], runs);
        Assert.Equal("<p>[A & B C : D E]</p><pre>[x  y]\n[z]</pre>", rendered);
    }

    [Theory]
    [InlineData(null, "text", false)]
    [InlineData("", "text", false)]
    [InlineData("text", "text", false)]
    [InlineData("<p>text</p>", "", false)]
    [InlineData("<p>text</p>", "text", true)]
    [InlineData("<p>other</p>", "text", false)]
    [InlineData("<img alt=\"Schéma\">", "[image: Schéma]", true)]
    [InlineData("<img alt=\"Schéma\">", "[image : Schéma]", true)]
    public void OnlyTheMarkupThatAPlainTextCameFromIsItsSource(string? source, string plain, bool expected) =>
        Assert.Equal(expected, RichTextHtmlRenderer.IsSourceOf(source, plain));

    private static string Render(string source) => RichTextHtmlRenderer.Render(source, English, SinkEncoding.Attribute);

    // The letters of a text in order: list markers, cell separators and spacing differ between the two forms.
    private static string Letters(string text) => string.Concat(System.Net.WebUtility.HtmlDecode(text).Where(char.IsLetter));

    // Every tag is one of the fixed elements with at most its fixed attributes, and every element is closed in order.
    private static void AssertBalancedAllowlist(string html)
    {
        Stack<string> open = new();
        foreach (Match tag in TagPattern().Matches(html))
        {
            string name = tag.Groups["name"].Value, attributes = tag.Groups["attributes"].Value;
            Assert.Contains(name, Elements);
            if (tag.Groups["close"].Length > 0)
            {
                Assert.Empty(attributes);
                Assert.True(open.Count > 0 && open.Pop() == name, "Unbalanced </" + name + "> in " + html);
                continue;
            }
            Assert.Matches("^(| class=\"rich-heading\"| class=\"rich-image\"| start=\"[0-9]+\"|( colspan=\"[0-9]+\")?( rowspan=\"[0-9]+\")?)$", attributes);
            if (name is not ("br" or "hr")) open.Push(name);
        }
        Assert.Empty(open);
        Assert.DoesNotContain('<', TagPattern().Replace(html, ""));
    }

    [GeneratedRegex("<(?<close>/?)(?<name>[a-z0-9]+)(?<attributes>[^<>]*)>", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();
}
