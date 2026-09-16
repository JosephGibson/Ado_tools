using System.Xml;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-2")]
public sealed class StepsXmlParserTests
{
    [Theory]
    [InlineData("01-direct.xml", 1, null)]
    [InlineData("02-multiple.xml", 2, null)]
    [InlineData("03-empty-result.xml", 1, null)]
    [InlineData("04-unknown-type.xml", 1, DiagnosticCodes.UnknownStepType)]
    [InlineData("05-unformatted.xml", 1, null)]
    [InlineData("06-rich.xml", 1, null)]
    [InlineData("07-nested.xml", 1, DiagnosticCodes.NestedEncodingDecoded)]
    [InlineData("08-literal-entity.xml", 1, DiagnosticCodes.NestedEncodingDecoded)]
    [InlineData("09-french.xml", 1, null)]
    [InlineData("17-missing-ref.xml", 1, DiagnosticCodes.InvalidSharedStepReference)]
    [InlineData("18-invalid-ref.xml", 1, DiagnosticCodes.InvalidSharedStepReference)]
    [InlineData("19-compref-children.xml", 2, DiagnosticCodes.UnexpectedComprefChildren)]
    [InlineData("23-malformed-root.xml.txt", 0, DiagnosticCodes.MalformedStepsXml)]
    [InlineData("23-malformed-shared.xml.txt", 0, DiagnosticCodes.MalformedStepsXml)]
    [InlineData("26-hostile.xml", 1, null)]
    [InlineData("27-dtd.xml.txt", 0, DiagnosticCodes.MalformedStepsXml)]
    [InlineData("29-urls.xml", 1, null)]
    public void EachDocumentFixtureProducesExpectedNodesAndDiagnostics(string file, int count, string? code)
    {
        StepDocument document = Parse(ParserFixture.Read("Steps/" + file));
        Assert.Equal(42, document.WorkItemId);
        Assert.Equal(3, document.Rev);
        Assert.Equal(count, document.Nodes.Count);
        if (code is null) Assert.Empty(document.Diagnostics);
        else Assert.Equal(code, Assert.Single(document.Diagnostics).Code);
        Assert.All(document.Diagnostics, diagnostic => Assert.Equal(42, diagnostic.WorkItemId));
        Assert.Throws<NotSupportedException>(() => ((IList<StepNode>)document.Nodes).Clear());
    }

    [Fact]
    [Trait("Acceptance", "S1-5")]
    public void UnformattedValuesRemainLiteralAndSourcesAreXmlDecoded()
    {
        StepNode plain = Assert.Single(Parse(ParserFixture.Read("Steps/05-unformatted.xml")).Nodes);
        Assert.Equal("List<int> & @user", plain.Action);
        Assert.Equal("x < 2 & y > 1", plain.ExpectedResult);
        Assert.Equal(plain.Action, plain.ActionSource);
        StepNode rich = Assert.Single(Parse(ParserFixture.Read("Steps/07-nested.xml")).Nodes);
        Assert.Equal("&amp;lt;b&amp;gt;Open&amp;lt;/b&amp;gt;", rich.ActionSource);
        Assert.Equal("Open", rich.Action);
    }

    [Fact]
    public void MatchesLocalNamesAndReadsOnlyImmediateRootChildrenInOrder()
    {
        StepDocument document = Parse("""
            <s:steps xmlns:s="urn:synthetic">
              <!-- ignored --><?ignored data?>
              <s:step id="9" type="ActionStep"><s:parameterizedString>A</s:parameterizedString></s:step>
              <s:wrapper><s:step type="ActionStep"/></s:wrapper>
              <s:compref id="11" ref="123"><s:step type="ActionStep"/></s:compref>
              <s:step id="2" type="ValidateStep"><s:parameterizedString>B</s:parameterizedString><s:parameterizedString>C</s:parameterizedString></s:step>
            </s:steps>
            """);
        Assert.Equal(new int?[] { 9, 11, 2 }, document.Nodes.Select(n => n.SourceStepId));
        Assert.Equal(new[] { AdoTestStepKind.Action, AdoTestStepKind.SharedStep, AdoTestStepKind.Validate }, document.Nodes.Select(n => n.Kind));
        Assert.Equal(123, document.Nodes[1].SharedStepId);
        Assert.Null(document.Nodes[1].ActionSource);
        Assert.Null(document.Nodes[1].DiagnosticCode);
        Assert.Equal(new[] { DiagnosticCodes.UnknownStepElement, DiagnosticCodes.UnexpectedComprefChildren }, document.Diagnostics.Select(d => d.Code));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    [InlineData("")]
    public void InvalidReferencesNeverFallBackToTheStepId(string reference)
    {
        StepDocument document = Parse($"<steps><compref id='123' ref='{reference}'/></steps>");
        StepNode node = Assert.Single(document.Nodes);
        Assert.Equal(AdoTestStepKind.SharedStep, node.Kind);
        Assert.Null(node.SharedStepId);
        Assert.Equal(DiagnosticCodes.InvalidSharedStepReference, node.DiagnosticCode);
        Assert.Equal(AdoDiagnosticSeverity.Error, Assert.Single(document.Diagnostics).Severity);
    }

    [Theory]
    [InlineData("", AdoTestStepKind.Action)]
    [InlineData("<parameterizedString>A</parameterizedString><parameterizedString>E</parameterizedString>", AdoTestStepKind.Validate)]
    [InlineData("<parameterizedString>A</parameterizedString><parameterizedString isformatted='true'>&lt;br/&gt;</parameterizedString>", AdoTestStepKind.Action)]
    public void UnknownOrMissingTypesInferFromConvertedExpectedResult(string values, AdoTestStepKind expected)
    {
        StepDocument document = Parse($"<steps><step>{values}</step></steps>");
        Assert.Equal(expected, Assert.Single(document.Nodes).Kind);
        Assert.Equal(DiagnosticCodes.UnknownStepType, Assert.Single(document.Diagnostics).Code);
    }

    [Fact]
    public void ExtraValuesAreIgnoredAndKnownTypesAreNotInferred()
    {
        StepDocument document = Parse("<steps><step type='ActionStep'><parameterizedString>A</parameterizedString><parameterizedString>E</parameterizedString><parameterizedString>X</parameterizedString></step><step type='ValidateStep'/></steps>");
        Assert.Equal("A", document.Nodes[0].Action);
        Assert.Equal("E", document.Nodes[0].ExpectedResult);
        Assert.Equal(AdoTestStepKind.Action, document.Nodes[0].Kind);
        Assert.Equal(AdoTestStepKind.Validate, document.Nodes[1].Kind);
        Assert.Equal(string.Empty, document.Nodes[1].ExpectedResultSource);
        Assert.Equal(DiagnosticCodes.UnknownStepElement, Assert.Single(document.Diagnostics).Code);
    }

    [Theory]
    [InlineData("<wrong/>")]
    [InlineData("<steps/><steps/>")]
    [InlineData("<steps>\n<step></steps>")]
    public void BadDocumentsHaveNoNodesAndOnlyLocalizedLineAndPosition(string xml)
    {
        StepDocument document = Parse(xml);
        Assert.Empty(document.Nodes);
        AdoDiagnostic diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal(DiagnosticCodes.MalformedStepsXml, diagnostic.Code);
        Assert.Equal(2, diagnostic.Arguments.Count);
        Assert.All(diagnostic.Arguments, value => Assert.True(int.Parse(value, CultureInfo.InvariantCulture) > 0));
        Assert.DoesNotContain(xml, diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("<steps/>")]
    public void EmptyDocumentsHaveInfoDiagnostic(string? xml)
    {
        StepDocument document = Parse(xml);
        Assert.Empty(document.Nodes);
        Assert.Equal(DiagnosticCodes.EmptySteps, Assert.Single(document.Diagnostics).Code);
    }

    [Fact]
    public void SharedReaderSettingsAndCharacterLimitApplyToAllXml()
    {
        XmlReaderSettings settings = SafeXml.CreateSettings();
        Assert.Equal(DtdProcessing.Prohibit, settings.DtdProcessing);
        Assert.Equal(ValidationType.None, settings.ValidationType);
        Assert.True(settings.IgnoreComments);
        Assert.True(settings.IgnoreProcessingInstructions);
        Assert.Equal(10_485_760, settings.MaxCharactersInDocument);
        string prefix = "<steps><step type='ActionStep'><parameterizedString>";
        const string suffix = "</parameterizedString></step></steps>";
        string atLimit = prefix + new string('é', (int)SafeXml.MaximumCharacters - prefix.Length - suffix.Length) + suffix;
        Assert.Single(Parse(atLimit).Nodes); // Characters, not UTF-8 bytes.
        StepDocument overLimit = Parse(atLimit + " ");
        Assert.Empty(overLimit.Nodes);
        Assert.Equal(DiagnosticCodes.MalformedStepsXml, Assert.Single(overLimit.Diagnostics).Code);
    }

    [Fact]
    public void FrenchFixturePreservesNfdAndLiteralUtf8Spaces()
    {
        string xml = ParserFixture.Read("Steps/09-french.xml");
        Assert.Contains("cafe\u0301", xml, StringComparison.Ordinal);
        Assert.Contains("\u00a0", xml, StringComparison.Ordinal);
        Assert.Contains("\u202f", xml, StringComparison.Ordinal);
        Assert.False(xml.IsNormalized(NormalizationForm.FormC));
        Assert.False(Assert.Single(Parse(xml).Nodes).Action.IsNormalized(NormalizationForm.FormC));
    }

    private static StepDocument Parse(string? xml) => StepsXmlParser.Parse(xml, 42, 3, CultureInfo.CurrentCulture);
}
