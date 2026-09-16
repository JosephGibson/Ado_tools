namespace AdoToolkit.Core.Tests.Connections;

public sealed class CollectionUrlNormalizerTests
{
    [Theory]
    [Trait("Acceptance", "S0-2")]
    [InlineData("  \"https://ADO.example.test/tfs/DefaultCollection///\"  ", "https://ado.example.test/tfs/DefaultCollection")]
    [InlineData("'https://ado.example.test/Collection/'", "https://ado.example.test/Collection")]
    [InlineData("https://ado.example.test/Équipe/", "https://ado.example.test/%C3%89quipe")]
    public void NormalizesWithoutChangingPathCase(string input, string expected)
    {
        CollectionUrlNormalizationResult result = CollectionUrlNormalizer.Normalize(input, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result.CollectionUri.AbsoluteUri);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [Trait("Acceptance", "S0-2")]
    [InlineData("")]
    [InlineData("/DefaultCollection")]
    [InlineData("ftp://ado.example.test/Collection")]
    [InlineData("https://user:pass@ado.example.test/Collection")]
    [InlineData("https://ado.example.test/Collection?q=x")]
    [InlineData("https://ado.example.test/Collection?")]
    [InlineData("https://ado.example.test/Collection#fragment")]
    [InlineData("https://ado.example.test/Collection#")]
    [InlineData("https://DEV.AZURE.COM/collection")]
    [InlineData("https://TEAM.VisualStudio.com/collection")]
    [InlineData("https://ado.example.test/Collection/_APIS/projects")]
    [InlineData("https://ado.example.test/Collection/_git/repository")]
    [InlineData("https://ado.example.test/Collection/_workitems")]
    [InlineData("https://ado.example.test/Collection/_build")]
    [InlineData("https://ado.example.test/Collection/_testPlans")]
    [InlineData("https://ado.example.test/Collection/_testManagement")]
    [InlineData("https://ado.example.test/Collection/_settings")]
    [InlineData("https://ado.example.test/Collection/%5Fapis/projects")]
    public void RejectsInvalidCollectionTargets(string input)
    {
        Assert.Throws<AdoConfigurationException>(() => CollectionUrlNormalizer.Normalize(input, CultureInfo.InvariantCulture));
    }

    [Fact]
    [Trait("Acceptance", "S0-2")]
    public void WarnsForPlainHttpInRequestedLanguage()
    {
        CollectionUrlNormalizationResult result = CollectionUrlNormalizer.Normalize("http://ado.example.test/Collection", CultureInfo.GetCultureInfo("fr-CA"));
        Assert.Single(result.Warnings);
        Assert.Contains("authentification", result.Warnings[0], StringComparison.Ordinal);
    }
}
