using AdoToolkit.Core.IO;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-2")]
public sealed class ParameterDataParserTests
{
    private static readonly string[] LocalNames = ["user", "password"];
    private static readonly string[] FrenchNames = ["Prénom", "Ville préférée"];
    private static readonly string[] DeclaredNames = ["region", "user"];
    private static readonly int[] SharedIds = [7001, 7002];
    [Theory]
    [InlineData("01-names.xml", "01-local.xml", AdoParameterSource.Local, 2, 0)]
    [InlineData("01-names.xml", "02-shared.json", AdoParameterSource.Shared, 0, 0)]
    [InlineData("03-declared-only.xml", null, AdoParameterSource.None, 0, 0)]
    [InlineData("01-names.xml", "04-malformed.xml.txt", AdoParameterSource.Local, 0, 1)]
    [InlineData("01-names.xml", "04-malformed.json.txt", AdoParameterSource.Shared, 0, 1)]
    [InlineData("04-malformed-names.xml.txt", "01-local.xml", AdoParameterSource.Local, 2, 1)]
    [InlineData("01-names.xml", "04-dtd.xml.txt", AdoParameterSource.Local, 0, 1)]
    [InlineData("06-french-names.xml", "06-french-local.xml", AdoParameterSource.Local, 1, 0)]
    public void FixturesHaveExpectedSourceRowsAndDiagnostics(string declarations, string? data, AdoParameterSource source, int rows, int warnings)
    {
        ParameterDocument document = Parse(Read(declarations), data is null ? null : Read(data));
        Assert.Equal(source, document.Parameters.Source);
        Assert.Equal(rows, document.Parameters.Rows.Count);
        Assert.Equal(warnings, document.Diagnostics.Count);
        Assert.All(document.Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticCodes.MalformedParameterData, diagnostic.Code);
            Assert.Equal(AdoDiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal(42, diagnostic.WorkItemId);
        });
    }

    [Fact]
    public void LocalRowsSkipSchemaAndRetainDeclaredOrderEmptyValuesAndReadOnlyCollections()
    {
        AdoTestParameters parameters = Parse(Read("01-names.xml"), Read("01-local.xml")).Parameters;
        Assert.Equal(LocalNames, parameters.Names);
        Assert.Equal("Alice", parameters.Rows[0]["user"]);
        Assert.Equal("synthetic-one", parameters.Rows[0]["password"]);
        Assert.Equal("Bob", parameters.Rows[1]["user"]);
        Assert.Equal(string.Empty, parameters.Rows[1]["password"]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)parameters.Names).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<IReadOnlyDictionary<string, string>>)parameters.Rows).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)parameters.Rows[0]).Clear());
    }

    [Fact]
    public void SharedMappingRetainsNamesAndDistinctUnresolvedIdsWithoutJoining()
    {
        ParameterDocument document = Parse(Read("01-names.xml"), Read("02-shared.json"));
        Assert.Equal(7001, document.SharedMapping["user"]);
        Assert.Equal(7001, document.SharedMapping["password"]);
        Assert.Equal(7002, document.SharedMapping["region"]);
        Assert.Equal(SharedIds, document.Parameters.SharedParameterSets.Select(set => set.Id));
        Assert.All(document.Parameters.SharedParameterSets, set =>
        {
            Assert.Null(set.Title);
            Assert.Null(set.Rev);
            Assert.Null(set.TeamProject);
            Assert.Null(set.WebUrl);
        });
        Assert.Empty(document.Parameters.Rows);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)document.SharedMapping).Clear());
    }

    [Theory]
    [InlineData("02-shared-set.xml", 2, 0)]
    [InlineData("06-french-local.xml", 1, 0)]
    [InlineData("04-malformed.xml.txt", 0, 1)]
    [InlineData("04-dtd.xml.txt", 0, 1)]
    public void SharedSetXmlUsesTheSameSafeRowParser(string fixture, int rows, int diagnostics)
    {
        ParameterDocument document = ParameterDataParser.ParseSharedSet(Read(fixture), CultureInfo.CurrentCulture, 7001);
        Assert.Equal(AdoParameterSource.Shared, document.Parameters.Source);
        Assert.Equal(rows, document.Parameters.Rows.Count);
        Assert.Equal(diagnostics, document.Diagnostics.Count);
        if (fixture == "02-shared-set.xml")
        {
            Assert.Equal(LocalNames, document.Parameters.Names);
            Assert.Equal("Bob", document.Parameters.Rows[1]["user"]);
        }
        Assert.All(document.Diagnostics, diagnostic => Assert.Equal(7001, diagnostic.WorkItemId));
    }

    [Fact]
    public void FrenchColumnNamesDecodeToTheDeclaredNames()
    {
        AdoTestParameters parameters = Parse(Read("06-french-names.xml"), Read("06-french-local.xml")).Parameters;
        Assert.Equal(FrenchNames, parameters.Names);
        Assert.Equal("Hélène", Assert.Single(parameters.Rows)[parameters.Names[0]]);
        Assert.Equal("Québec", parameters.Rows[0][parameters.Names[1]]);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"user\":0}")]
    [InlineData("{\"user\":-1}")]
    [InlineData("{\"user\":1.5}")]
    [InlineData("{\"user\":2147483648}")]
    [InlineData("{\"user\":\"7001\"}")]
    [InlineData("{\"user\":7001,\"user\":7002}")]
    [InlineData("{\"user\":{\"id\":7001}}")]
    [InlineData("<data><row><user>A</user><user>B</user></row></data>")]
    [InlineData("<data><row><user><nested/></user></row></data>")]
    [InlineData("<data><row>unexpected</row></data>")]
    public void MalformedShapesAreWarningsAndRetainTheDeclaredNames(string data)
    {
        ParameterDocument document = Parse(Read("01-names.xml"), data);
        Assert.Equal(DiagnosticCodes.MalformedParameterData, Assert.Single(document.Diagnostics).Code);
        Assert.Equal(LocalNames, document.Parameters.Names);
        Assert.Empty(document.SharedMapping);
        Assert.Empty(document.Parameters.Rows);
        Assert.DoesNotContain(data, document.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<wrong/>")]
    [InlineData("<parameters><param/></parameters>")]
    [InlineData("<parameters><param name='A'/><param name='A'/></parameters>")]
    [InlineData("<parameters><unknown name='A'/></parameters>")]
    [InlineData("<!DOCTYPE parameters SYSTEM 'https://external.example.test/dtd'><parameters/>")]
    public void BadDeclarationsDoNotDiscardValidRows(string declarations)
    {
        ParameterDocument document = Parse(declarations, Read("01-local.xml"));
        Assert.Empty(document.Parameters.Names);
        Assert.Equal(2, document.Parameters.Rows.Count);
        Assert.Equal(DiagnosticCodes.MalformedParameterData, Assert.Single(document.Diagnostics).Code);
    }

    [Fact]
    public void EmptyInputAndDeclaredOnlyDataRemainDistinct()
    {
        ParameterDocument empty = Parse(null, null);
        Assert.Equal(AdoParameterSource.None, empty.Parameters.Source);
        Assert.Empty(empty.Parameters.Names);
        Assert.Empty(empty.Diagnostics);
        ParameterDocument declared = Parse(Read("03-declared-only.xml"), null);
        Assert.Equal(DeclaredNames, declared.Parameters.Names);
        Assert.Empty(declared.Parameters.Rows);
    }

    [Fact]
    public void SchemaIsSkippedWithoutLoadingTypesAndNamespacesUseLocalNames()
    {
        const string xml = "<d:data xmlns:d='urn:synthetic' xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:schema><xs:import schemaLocation='https://external.example.test/schema'/></xs:schema><d:row><d:user>A &amp; B</d:user><d:_x005F_x0041_>literal</d:_x005F_x0041_></d:row></d:data>";
        ParameterDocument document = Parse("<p:parameters xmlns:p='urn:synthetic'><p:param name='user'/></p:parameters>", xml);
        Assert.Empty(document.Diagnostics);
        Assert.Equal("A & B", Assert.Single(document.Parameters.Rows)["user"]);
        Assert.Equal("literal", document.Parameters.Rows[0]["_x0041_"]);
    }

    [Fact]
    public void SizeLimitAppliesToDeclarationsLocalDataAndSharedSets()
    {
        string huge = new('x', (int)SafeXml.MaximumCharacters);
        Assert.Single(Parse("<parameters>" + huge + "</parameters>", null).Diagnostics);
        string data = "<data><row><value>" + huge + "</value></row></data>";
        Assert.Single(Parse(null, data).Diagnostics);
        Assert.Single(ParameterDataParser.ParseSharedSet(data, CultureInfo.CurrentCulture).Diagnostics);
    }

    private static ParameterDocument Parse(string? names, string? data) => ParameterDataParser.Parse(names, data, CultureInfo.CurrentCulture, 42);
    private static string Read(string file) => ParserFixture.Read("Parameters/" + file);
}
