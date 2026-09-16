using System.Text.Json.Nodes;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.Tests.Configuration;

public sealed class ConfigurationStoreTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    [Trait("Acceptance", "S0-5")]
    public void DefaultResolutionIsAlwaysIsolatedInTests()
    {
        string? configured = Environment.GetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH");
        Assert.False(string.IsNullOrWhiteSpace(configured));
        Assert.Equal(Path.GetFullPath(configured), ConfigurationStore.ResolvePath());
        Assert.StartsWith(Path.GetTempPath(), configured, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdoToolkit", "config.json"), configured);
    }

    [Fact]
    [Trait("Acceptance", "S0-5")]
    public void ProfilesAreNormalizedSavedAndRemovedWithDefaultCleared()
    {
        using TestDirectory directory = new();
        ConfigurationStore store = new();
        Assert.False(File.Exists(directory.ConfigPath));
        AdoProfile profile = store.SetProfile(new AdoProfile
        {
            Name = "sample", CollectionUri = new Uri("https://ado.example.test/Collection///"),
            DefaultProject = "Équipe Web",
        }, true, English);
        Assert.Equal("https://ado.example.test/Collection", profile.CollectionUrl);
        AdoConfiguration loaded = store.Load(English);
        Assert.Equal("sample", loaded.DefaultProfile);
        Assert.Equal("Équipe Web", loaded.Profiles["sample"].DefaultProject);
        Assert.Equal(5000, loaded.TestCases.MaximumExpandedSteps);
        Assert.Equal(524288000, loaded.TestResults.MaximumTotalAttachmentBytes);
        store.RemoveProfile("SAMPLE", English);
        Assert.Empty(store.Load(English).Profiles);
        Assert.Null(store.Load(English).DefaultProfile);
    }

    [Fact]
    [Trait("Acceptance", "S0-5")]
    public void UnknownPropertiesAtEveryLevelSurviveAWriteAndWarn()
    {
        using TestDirectory directory = new();
        File.WriteAllText(directory.ConfigPath, """
        {"schemaVersion":1,"extra":{"keep":true},"profiles":{"sample":{"collectionUrl":"https://ado.example.test/Collection","extraProfile":42}},
         "testCases":{"futureLimit":7},"testResults":{"futureMode":"sample"},"reporting":{"extraLabel":"Été"}}
        """);
        ConfigurationStore store = new();
        AdoConfiguration loaded = store.Load(English);
        Assert.Equal(5, loaded.Warnings.Count);
        store.Save(loaded, English);
        JsonNode result = JsonNode.Parse(File.ReadAllText(directory.ConfigPath))!;
        Assert.True(result["extra"]!["keep"]!.GetValue<bool>());
        Assert.Equal(42, result["profiles"]!["sample"]!["extraProfile"]!.GetValue<int>());
        Assert.Equal(7, result["testCases"]!["futureLimit"]!.GetValue<int>());
        Assert.Equal("sample", result["testResults"]!["futureMode"]!.GetValue<string>());
        Assert.Equal("Été", result["reporting"]!["extraLabel"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Acceptance", "S0-5")]
    public void OldSchemaMigratesInMemoryAndNewSchemaCannotBeOverwritten()
    {
        using TestDirectory directory = new();
        File.WriteAllText(directory.ConfigPath, """{"schemaVersion":0}""");
        ConfigurationStore store = new();
        Assert.Equal(1, store.Load(English).SchemaVersion);
        Assert.Contains("\"schemaVersion\":0", File.ReadAllText(directory.ConfigPath), StringComparison.Ordinal);
        File.WriteAllText(directory.ConfigPath, """{"schemaVersion":2,"future":true}""");
        byte[] original = File.ReadAllBytes(directory.ConfigPath);
        Assert.True(store.Load(English).IsReadOnly);
        Assert.Throws<AdoConfigurationException>(() => store.Save(new AdoConfiguration(), English));
        Assert.Equal(original, File.ReadAllBytes(directory.ConfigPath));
    }

    [Theory]
    [Trait("Acceptance", "S0-5")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":\"future\"}")]
    [InlineData("{\"profiles\":{\"sample\":{\"collectionUrl\":\"https://ado.example.test/Collection\",\"requestTimeoutSeconds\":0}}}")]
    [InlineData("{\"profiles\":{\"sample\":{\"collectionUrl\":\"https://ado.example.test/Collection\",\"requestTimeoutSeconds\":86401}}}")]
    [InlineData("{\"profiles\":{\"sample\":{\"collectionUrl\":\"https://ado.example.test/Collection\",\"authentication\":\"Unsupported\"}}}")]
    public void RejectsInvalidConfiguration(string json)
    {
        Assert.Throws<AdoConfigurationException>(() => ConfigurationStore.Parse(json, English));
    }

    [Fact]
    [Trait("Acceptance", "S0-5")]
    public void FailedValidationPreservesExistingBytesAndRemovesTemporaryFile()
    {
        using TestDirectory directory = new();
        ConfigurationStore initial = new();
        initial.Save(new AdoConfiguration(), English);
        byte[] original = File.ReadAllBytes(directory.ConfigPath);
        ConfigurationStore failing = new(directory.ConfigPath, _ => throw new InvalidOperationException("injected"));
        Assert.Throws<InvalidOperationException>(() => failing.Save(new AdoConfiguration(), English));
        Assert.Equal(original, File.ReadAllBytes(directory.ConfigPath));
        Assert.Single(Directory.GetFiles(directory.Root));
    }

    [Fact]
    [Trait("Acceptance", "S0-5")]
    public void LockedDestinationRaisesLocalizedFileErrorAndLeavesNoTemporaryFile()
    {
        using TestDirectory directory = new();
        File.WriteAllText(directory.ConfigPath, "original");
        using (FileStream locked = new(directory.ConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            AdoFileOutputException error = Assert.Throws<AdoFileOutputException>(() =>
                AtomicFileReplace.Write(directory.ConfigPath, Encoding.UTF8.GetBytes("replacement"), English));
            Assert.Contains(directory.ConfigPath, error.Message, StringComparison.Ordinal);
        }
        Assert.Equal("original", File.ReadAllText(directory.ConfigPath));
        Assert.Single(Directory.GetFiles(directory.Root));
    }
}
