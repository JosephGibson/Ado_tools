namespace AdoToolkit.Core.Tests.Localization;

public sealed class CultureInvarianceTests
{
    [Fact]
    [Trait("Acceptance", "S0-8")]
    public void ConfigurationAndNormalizedUrisAreByteIdenticalAcrossCultures()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            List<byte[]> outputs = [];
            foreach (string culture in new[] { "en-US", "fr-CA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                AdoConfiguration configuration = ConfigurationStore.Parse("""
                {"schemaVersion":1,"profiles":{"sample":{"collectionUrl":"https://ado.example.test/Équipe///","requestTimeoutSeconds":123}}}
                """, CultureInfo.CurrentCulture);
                byte[] serialized = ConfigurationStore.Serialize(configuration);
                outputs.Add(serialized);
                outputs.Add(Encoding.UTF8.GetBytes(configuration.Profiles["sample"].CollectionUri.AbsoluteUri));
            }
            Assert.Equal(outputs[0], outputs[2]);
            Assert.Equal(outputs[1], outputs[3]);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [Trait("Acceptance", "S0-7")]
    [InlineData("fr-CA", "en-US", "en-US", "fr-CA", false)]
    [InlineData(null, "fr-FR", "en-US", "fr-FR", false)]
    [InlineData(null, null, "fr-CA", "fr-CA", false)]
    [InlineData("de-DE", "fr-CA", "en-US", "en", true)]
    [InlineData("not-a-real-culture!", null, "fr-CA", "en", true)]
    public void CultureSelectionHasExplicitPrecedenceAndEnglishFallback(string? explicitCulture, string? configuredCulture, string sessionCulture, string expected, bool warning)
    {
        ReportCultureResult result = ReportCultureResolver.Resolve(explicitCulture, configuredCulture, CultureInfo.GetCultureInfo(sessionCulture));
        Assert.Equal(expected, result.Culture.Name);
        Assert.Equal(warning, result.Warnings.Count > 0);
    }
}
