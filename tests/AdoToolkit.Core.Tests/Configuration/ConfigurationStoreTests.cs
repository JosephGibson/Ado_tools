using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.Builds;
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

    // A hand-edited file with a repeated name must fail as configuration, not as an unhandled
    // ArgumentException that every command and the profile completer would surface raw.
    [Theory]
    [InlineData("{\"defaultProfile\":\"a\",\"defaultProfile\":\"b\"}")]
    [InlineData("{\"profiles\":{\"work\":{\"collectionUrl\":\"https://ado.example.test/Collection\"},\"work\":{\"collectionUrl\":\"https://ado.example.test/Other\"}}}")]
    [InlineData("{\"testResults\":{\"historyCount\":5,\"historyCount\":6}}")]
    public void DuplicatePropertyNamesAreAConfigurationError(string json)
    {
        Assert.Throws<AdoConfigurationException>(() => ConfigurationStore.Parse(json, English));
        using TestDirectory directory = new();
        File.WriteAllText(directory.ConfigPath, json);
        Assert.Throws<AdoConfigurationException>(() => new ConfigurationStore(directory.ConfigPath).Load(English));
    }

    [Fact]
    public void ProfileDefaultsAreReadWithoutWarnings()
    {
        AdoConfiguration loaded = ConfigurationStore.Parse("""
        {"profiles":{
          "named":{"collectionUrl":"https://ado.example.test/Collection","defaultBranch":"develop","defaultBuildDefinition":"Test_Plan",
                   "defaultTestPlanId":812,"defaultTestSuiteId":813},
          "numbered":{"collectionUrl":"https://ado.example.test/Collection","defaultBranch":"refs/heads/release/Été","defaultBuildDefinition":42}}}
        """, English);
        Assert.Empty(loaded.Warnings);
        AdoProfile named = loaded.Profiles["named"];
        Assert.Equal("develop", named.DefaultBranch);
        Assert.Equal(BuildDefinitionSelector.FromName("Test_Plan"), named.DefaultBuildDefinition);
        Assert.Null(named.DefaultBuildDefinition!.Id);
        Assert.Equal(812, named.DefaultTestPlanId);
        Assert.Equal(813, named.DefaultTestSuiteId);
        AdoProfile numbered = loaded.Profiles["numbered"];
        Assert.Equal("refs/heads/release/Été", numbered.DefaultBranch);
        Assert.Equal(42, numbered.DefaultBuildDefinition!.Id);
        Assert.Null(numbered.DefaultBuildDefinition.Name);
        Assert.Equal("42", numbered.DefaultBuildDefinition.ToString());
        Assert.Null(numbered.DefaultTestPlanId);
        Assert.Null(numbered.DefaultTestSuiteId);
    }

    [Theory]
    [InlineData("""{"collectionUrl":"https://ado.example.test/Collection"}""")]
    [InlineData("""{"collectionUrl":"https://ado.example.test/Collection","defaultBranch":null,"defaultBuildDefinition":null,"defaultTestPlanId":null,"defaultTestSuiteId":null}""")]
    public void AbsentOrNullProfileDefaultsAreUnset(string profile)
    {
        AdoConfiguration loaded = ConfigurationStore.Parse("""{"profiles":{"work":""" + profile + "}}", English);
        AdoProfile work = loaded.Profiles["work"];
        Assert.Empty(loaded.Warnings);
        Assert.Null(work.DefaultBranch);
        Assert.Null(work.DefaultBuildDefinition);
        Assert.Null(work.DefaultTestPlanId);
        Assert.Null(work.DefaultTestSuiteId);
    }

    [Theory]
    [InlineData("\"defaultBranch\":\"\"")]
    [InlineData("\"defaultBranch\":\"   \"")]
    [InlineData("\"defaultBranch\":5")]
    [InlineData("\"defaultBranch\":[\"develop\"]")]
    [InlineData("\"defaultBuildDefinition\":\"\"")]
    [InlineData("\"defaultBuildDefinition\":\" \"")]
    [InlineData("\"defaultBuildDefinition\":0")]
    [InlineData("\"defaultBuildDefinition\":-42")]
    [InlineData("\"defaultBuildDefinition\":4.5")]
    [InlineData("\"defaultBuildDefinition\":2147483648")]
    [InlineData("\"defaultBuildDefinition\":true")]
    [InlineData("\"defaultBuildDefinition\":{\"id\":42}")]
    [InlineData("\"defaultTestPlanId\":0")]
    [InlineData("\"defaultTestPlanId\":-812")]
    [InlineData("\"defaultTestPlanId\":\"812\"")]
    [InlineData("\"defaultTestPlanId\":812.5")]
    [InlineData("\"defaultTestPlanId\":2147483648")]
    [InlineData("\"defaultTestSuiteId\":0")]
    [InlineData("\"defaultTestSuiteId\":\"\"")]
    public void InvalidProfileDefaultsAreAConfigurationError(string setting)
    {
        string json = """{"profiles":{"work":{"collectionUrl":"https://ado.example.test/Collection",""" + setting + "}}}";
        Assert.Throws<AdoConfigurationException>(() => ConfigurationStore.Parse(json, English));
        using TestDirectory directory = new();
        File.WriteAllText(directory.ConfigPath, json);
        Assert.Throws<AdoConfigurationException>(() => new ConfigurationStore().Load(English));
    }

    // The defaults belong to a profile: misspelled names and the same names outside a profile warn.
    [Fact]
    public void MisplacedOrMisspelledProfileDefaultsWarn()
    {
        AdoConfiguration loaded = ConfigurationStore.Parse("""
        {"defaultBranch":"develop","profiles":{"work":{"collectionUrl":"https://ado.example.test/Collection",
          "defaultTestPlan":812,"defaultDefinition":"Test_Plan","defaultTestSuiteId":813}},"testResults":{"defaultBranch":"main"}}
        """, English);
        Assert.Equal(
            ["Unknown configuration property retained: defaultBranch", "Unknown configuration property retained: profiles.work.defaultTestPlan",
             "Unknown configuration property retained: profiles.work.defaultDefinition", "Unknown configuration property retained: testResults.defaultBranch"],
            loaded.Warnings);
        Assert.Equal(813, loaded.Profiles["work"].DefaultTestSuiteId);
        Assert.Null(loaded.Profiles["work"].DefaultTestPlanId);
    }

    // A file written before the defaults existed saves byte for byte as it was read.
    [Fact]
    public void FileWithoutProfileDefaultsSavesUnchanged()
    {
        using TestDirectory directory = new();
        string original = JsonNode.Parse(CompleteConfiguration)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(directory.ConfigPath, original);
        ConfigurationStore store = new();
        store.Save(store.Load(English), English);
        Assert.Equal(original, File.ReadAllText(directory.ConfigPath));
    }

    [Fact]
    public void SettingAndClearingProfileDefaultsKeepsEveryOtherSetting()
    {
        using TestDirectory directory = new();
        File.WriteAllText(directory.ConfigPath, CompleteConfiguration);
        ConfigurationStore store = new();
        AdoProfile before = store.Load(English).Profiles["work"];
        AdoProfile saved = store.SetProfile(new AdoProfile
        {
            Name = "work", CollectionUri = before.CollectionUri, DefaultProject = before.DefaultProject,
            DefaultBranch = "develop", DefaultBuildDefinition = BuildDefinitionSelector.FromName("Test_Plan"),
            DefaultTestPlanId = 812, DefaultTestSuiteId = 813, RequestTimeoutSeconds = before.RequestTimeoutSeconds,
        }, false, English);
        Assert.Equal("develop", saved.DefaultBranch);
        JsonNode written = JsonNode.Parse(File.ReadAllText(directory.ConfigPath))!;
        JsonNode work = written["profiles"]!["work"]!;
        Assert.Equal("develop", work["defaultBranch"]!.GetValue<string>());
        Assert.Equal(JsonValueKind.String, work["defaultBuildDefinition"]!.GetValueKind());
        Assert.Equal("Test_Plan", work["defaultBuildDefinition"]!.GetValue<string>());
        Assert.Equal(812, work["defaultTestPlanId"]!.GetValue<int>());
        Assert.Equal(813, work["defaultTestSuiteId"]!.GetValue<int>());
        JsonNode expected = JsonNode.Parse(CompleteConfiguration)!;
        foreach (string key in new[] { "defaultBranch", "defaultBuildDefinition", "defaultTestPlanId", "defaultTestSuiteId" })
            expected["profiles"]!["work"]!.AsObject()[key] = work[key]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(expected, written));
        Assert.Equal("develop", store.Load(English).Profiles["work"].DefaultBranch);

        store.SetProfile(new AdoProfile
        {
            Name = "work", CollectionUri = before.CollectionUri, DefaultProject = before.DefaultProject,
            DefaultBuildDefinition = BuildDefinitionSelector.FromId(42), RequestTimeoutSeconds = before.RequestTimeoutSeconds,
        }, false, English);
        JsonNode numbered = JsonNode.Parse(File.ReadAllText(directory.ConfigPath))!["profiles"]!["work"]!;
        Assert.Equal(JsonValueKind.Number, numbered["defaultBuildDefinition"]!.GetValueKind());
        Assert.Equal(42, store.Load(English).Profiles["work"].DefaultBuildDefinition!.Id);

        store.SetProfile(before, false, English);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(CompleteConfiguration), JsonNode.Parse(File.ReadAllText(directory.ConfigPath))));
    }

    // Every setting that Save writes, with values other than the defaults, plus unknown properties.
    private const string CompleteConfiguration = """
    {"schemaVersion":1,"defaultProfile":"work","future":{"keep":true},
     "profiles":{
       "work":{"collectionUrl":"https://ado.example.test/Collection","defaultProject":"Équipe Web","authentication":"WindowsIntegrated",
               "requestTimeoutSeconds":45,"note":"kept"},
       "other":{"collectionUrl":"https://second.example.test/Other","defaultProject":null,"authentication":"WindowsIntegrated",
                "requestTimeoutSeconds":100}},
     "testCases":{"maximumSharedStepDepth":4,"maximumExpandedSteps":900,"maximumResolvedWorkItems":300},
     "testResults":{"historyCount":20,"historyScope":"AllBranches","maximumReportedFailures":50,"maximumHistoryRequests":60,
                    "maximumAttachmentBytes":1000,"maximumTotalAttachmentBytes":2000,"maximumInlineJsonBytes":300,"maximumInlineTotalBytes":400},
     "reporting":{"culture":"fr-CA"}}
    """;

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
