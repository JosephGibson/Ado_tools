using System.Text.Json;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// Custom field values keep the JSON shape the server sent (§15.11): an array, an object or an
// identity must show its content, never a CLR type name.
public sealed class FieldValueRenderingTests
{
    private const string Fields = """
        {
          "Browsers": ["chrome", "edge <beta>"],
          "Matrix": { "os": "Windows 11", "retries": 2, "headless": true, "notes": null },
          "Reviewer": { "displayName": "Fictional Reviewer", "uniqueName": "reviewer@example.test" },
          "Team": [{ "displayName": "Fictional Lead", "id": "lead-1" }],
          "Empty": [],
          "Ratio": 1234.5,
          "Flag": true,
          "Text": "plain é"
        }
        """;

    [Theory]
    [InlineData("Browsers", "[\"chrome\",\"edge <beta>\"]")]
    [InlineData("Matrix", "{\"os\":\"Windows 11\",\"retries\":2,\"headless\":true,\"notes\":null}")]
    [InlineData("Reviewer", "Fictional Reviewer <reviewer@example.test>")]
    [InlineData("Team", "[{\"displayName\":\"Fictional Lead\",\"id\":\"lead-1\"}]")]
    [InlineData("Empty", "[]")]
    [InlineData("Text", "plain é")]
    [InlineData("Flag", "True")]
    public void ValueTextShowsContent(string name, string expected) =>
        Assert.Equal(expected, HtmlTestFailureRenderer.FieldText(Values()[name]!, CultureInfo.GetCultureInfo("en-US")));

    [Theory]
    [InlineData("en-US", "1234.5")]
    [InlineData("fr-CA", "1234,5")]
    public void NumbersUseTheReportCulture(string culture, string expected) =>
        Assert.Equal(expected, HtmlTestFailureRenderer.FieldText(Values()["Ratio"]!, CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void RenderedReportNeverShowsATypeNameForACustomField(string culture)
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        AdoTestAttempt attempt = new()
        {
            Number = 1, RunId = 201, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure,
            ErrorMessage = "Synthetic failure", CustomFields = Values(),
        };
        AdoTestFailure failure = source.Failures[0].WithAttempts([attempt]);
        AdoBuildTestFailureSet set = new()
        {
            Build = source.Build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = [failure],
            FailedCount = 1, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        };
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options(culture));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string text = TestFailureMarkup.Text(TestFailureMarkup.WithoutScripts(html));
        Assert.DoesNotContain("System.Collections", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AdoToolkit.Core", text, StringComparison.Ordinal);
        Assert.Contains("[\"chrome\",\"edge <beta>\"]", text, StringComparison.Ordinal);
        Assert.Contains("{\"os\":\"Windows 11\",\"retries\":2,\"headless\":true,\"notes\":null}", text, StringComparison.Ordinal);
        Assert.Contains("Fictional Reviewer <reviewer@example.test>", text, StringComparison.Ordinal);
        // Field text is content: it is encoded at the sink like every other value.
        Assert.DoesNotContain("<beta>", TestFailureMarkup.WithoutScripts(html), StringComparison.Ordinal);
    }

    private static Dictionary<string, object?> Values()
    {
        using JsonDocument document = JsonDocument.Parse(Fields);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name,
            property => FieldValueMapper.MapValue(property.Value), StringComparer.Ordinal);
    }
}
