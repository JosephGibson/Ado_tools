using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Json;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-5")]
public sealed class JsonSchemaValidationTests
{
    private static readonly string[] Variants = ["direct", "nested", "partial", "parameterized", "french"];
    private static readonly string[] Cultures = ["en-US", "fr-CA"];
    private static readonly bool[] SourceOptions = [false, true];
    public static TheoryData<string, string, bool> Cases => new(
        from variant in Variants
        from culture in Cultures
        from includeSource in SourceOptions
        select (variant, culture, includeSource));

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryVariantValidatesWithAndWithoutSource(string variant, string culture, bool includeSource)
    {
        ReportDocumentModel model = ReportFixture.Model(variant, culture);
        string text = ReportFixture.Render(model, includeSource);
        using JsonDocument json = JsonDocument.Parse(text);
        JsonElement root = json.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(ReportFixture.Timestamp, root.GetProperty("generatedAt").GetDateTimeOffset());
        Assert.Equal(TimeSpan.FromHours(-3), root.GetProperty("generatedAt").GetDateTimeOffset().Offset);
        Assert.Equal("2.1.0-test", root.GetProperty("generator").GetProperty("version").GetString());
        Assert.Equal(culture, root.GetProperty("culture").GetString());
        JsonElement testCase = Assert.Single(root.GetProperty("cases").EnumerateArray());
        Assert.Equal(model.Cases[0].Status.ToString(), testCase.GetProperty("status").GetString());
        foreach (JsonElement row in testCase.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(includeSource, row.TryGetProperty("actionSource", out _));
            Assert.Equal(includeSource, row.TryGetProperty("expectedResultSource", out _));
        }
        if (includeSource)
            Assert.Equal("<p>Original &amp; @user</p>", testCase.GetProperty("rows")[0].GetProperty("actionSource").GetString());

        using TestDirectory directory = new();
        FileInfo file = new AtomicFileWriter().Write(Path.Combine(directory.Root, "report.json"),
            writer => JsonTestCaseRenderer.Render(model, writer, includeSource),
            temporary => ReportOutputValidator.ValidateJson(temporary, model.Culture), model.Culture,
            cancellationToken: TestContext.Current.CancellationToken);
        byte[] bytes = File.ReadAllBytes(file.FullName);
        Assert.Equal(Encoding.UTF8.GetBytes(text), bytes);
        ReportFixture.AssertValid(Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain('\r', text);
    }

    [Theory]
    [InlineData("missingCases")]
    [InlineData("emptyCases")]
    [InlineData("wrongVersion")]
    [InlineData("rootExtra")]
    [InlineData("caseExtra")]
    [InlineData("rowExtra")]
    [InlineData("localizedEnum")]
    [InlineData("badNumber")]
    [InlineData("badDate")]
    [InlineData("badUrl")]
    [InlineData("missingDiagnosticArguments")]
    [InlineData("wrongParameterValue")]
    public void SchemaRejectsContractViolations(string mutation)
    {
        JsonObject root = JsonNode.Parse(ReportFixture.Render(ReportFixture.Model("partial")))!.AsObject();
        JsonObject testCase = root["cases"]![0]!.AsObject();
        switch (mutation)
        {
            case "missingCases": root.Remove("cases"); break;
            case "emptyCases": root["cases"] = new JsonArray(); break;
            case "wrongVersion": root["schemaVersion"] = 2; break;
            case "rootExtra": root["extra"] = true; break;
            case "caseExtra": testCase["extra"] = true; break;
            case "rowExtra": testCase["rows"]![0]!["extra"] = true; break;
            case "localizedEnum": testCase["rows"]![0]!["kind"] = "Étape"; break;
            case "badNumber": testCase["rows"]![0]!["number"] = "1,2"; break;
            case "badDate": root["generatedAt"] = "15/09/2026"; break;
            case "badUrl": testCase["webUrl"] = "not a URI"; break;
            case "missingDiagnosticArguments": testCase["diagnostics"]![0]!.AsObject().Remove("arguments"); break;
            case "wrongParameterValue": testCase["parameters"]!["rows"] = new JsonArray(new JsonObject { ["user"] = 7 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        Assert.False(ReportFixture.Evaluate(root.ToJsonString()).IsValid);
    }

    [Fact]
    public void SchemaAcceptsMultipleCasesWithoutChangingVersion()
    {
        JsonObject root = JsonNode.Parse(ReportFixture.Render(ReportFixture.Model()))!.AsObject();
        root["cases"]!.AsArray().Add(root["cases"]![0]!.DeepClone());
        ReportFixture.AssertValid(root.ToJsonString());
    }

    [Fact]
    public void AmbientCultureDoesNotChangeJsonBytes()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;
        try
        {
            ReportDocumentModel model = ReportFixture.Model("parameterized", "fr-CA");
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            string englishMachine = ReportFixture.Render(model, true);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal(Encoding.UTF8.GetBytes(englishMachine), Encoding.UTF8.GetBytes(ReportFixture.Render(model, true)));
        }
        finally { CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetrievedNestedAndPartialCaseValidates(bool includeSource)
    {
        using var handler = ExpansionFixture.Handler(
            ExpansionFixture.Item(1, ExpansionFixture.Xml("28-numbering.xml")),
            ExpansionFixture.Item(2, ExpansionFixture.Xml("28-child.xml")),
            ExpansionFixture.Item(3, ExpansionFixture.Xml("01-direct.xml")));
        AdoTestCase testCase = await ExpansionFixture.Retrieve(handler);
        ReportDocumentModel model = ReportModelBuilder.Build(testCase, ExpansionFixture.Connection, ReportFixture.Options("fr-CA"));
        string json = ReportFixture.Render(model, includeSource);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(9, document.RootElement.GetProperty("cases")[0].GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public void LargeTextStreamsWithoutTruncatingUnicodeOrClosingTheSuppliedWriter()
    {
        string large = new('é', 600_000);
        AdoTestCase testCase = new()
        {
            Id = 10, Rev = 3, Title = "Large synthetic case", WorkItemType = "Test Case", TeamProject = ReportFixture.Project,
            State = "Ready", CollectionUri = ReportFixture.Collection, WebUrl = ReportFixture.Untrusted,
            ChangedDate = ReportFixture.Timestamp, RetrievedAt = ReportFixture.Timestamp,
            Steps = [new() { Sequence = 1, Number = "1", Action = large + "😀", SourceWorkItemId = 10, SourceRev = 3 }],
        };
        ReportDocumentModel model = ReportModelBuilder.Build(testCase, new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options());
        using TestDirectory directory = new();
        FileInfo file = new AtomicFileWriter().Write(Path.Combine(directory.Root, "report.json"),
            writer => JsonTestCaseRenderer.Render(model, writer),
            temporary => ReportOutputValidator.ValidateJson(temporary, model.Culture), model.Culture,
            cancellationToken: TestContext.Current.CancellationToken);
        string output = File.ReadAllText(file.FullName);
        ReportFixture.AssertValid(output);
        using JsonDocument json = JsonDocument.Parse(output);
        Assert.Equal(large + "😀", json.RootElement.GetProperty("cases")[0].GetProperty("rows")[0].GetProperty("action").GetString());
        using StringWriter text = new(CultureInfo.InvariantCulture);
        JsonTestCaseRenderer.Render(ReportFixture.Model(), text);
        ReportFixture.AssertValid(text.ToString());
        text.Write("still open");
        Assert.EndsWith("still open", text.ToString(), StringComparison.Ordinal);
    }
}
