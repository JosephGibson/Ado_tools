using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.Configuration;

public sealed class ConfigurationStore
{
    private readonly Action<string>? validateBeforeCommit;
    public string FilePath { get; }

    public ConfigurationStore() : this(ResolvePath()) { }

    public ConfigurationStore(string path, Action<string>? validateBeforeCommit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = Path.GetFullPath(path);
        this.validateBeforeCommit = validateBeforeCommit;
    }

    public static string ResolvePath()
    {
        string? overridden = Environment.GetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH");
        return Path.GetFullPath(string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdoToolkit", "config.json")
            : overridden);
    }

    public AdoConfiguration Load(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        try
        {
            return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath), culture) : new AdoConfiguration();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AdoConfigurationException(Messages.Get(AdoMessage.InvalidConfiguration, culture, FilePath), error);
        }
    }

    public static AdoConfiguration Parse(string json, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(culture);
        try
        {
            JsonObject root = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException();
            int schema = root["schemaVersion"]?.GetValue<int>() ?? 0;
            if (schema < 0) throw new JsonException("schemaVersion");
            List<string> warnings = [];
            WarnUnknown(root, ["schemaVersion", "defaultProfile", "profiles", "testCases", "testResults", "reporting"], "", culture, warnings);
            Dictionary<string, AdoProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string name, JsonNode? value) in Object(root, "profiles"))
            {
                JsonObject profile = value?.AsObject() ?? throw new JsonException("profiles");
                WarnUnknown(profile, ["collectionUrl", "defaultProject", "authentication", "requestTimeoutSeconds"], "profiles." + name + ".", culture, warnings);
                CollectionUrlNormalizationResult normalized = CollectionUrlNormalizer.Normalize(profile["collectionUrl"]?.GetValue<string>() ?? "", culture);
                warnings.AddRange(normalized.Warnings);
                string authentication = profile["authentication"]?.GetValue<string>() ?? "WindowsIntegrated";
                if (!authentication.Equals("WindowsIntegrated", StringComparison.OrdinalIgnoreCase)) throw new JsonException("authentication");
                if (!profiles.TryAdd(name, new AdoProfile
                {
                    Name = name,
                    CollectionUri = normalized.CollectionUri,
                    DefaultProject = profile["defaultProject"]?.GetValue<string>(),
                    Authentication = "WindowsIntegrated",
                    RequestTimeoutSeconds = checked((int)Positive(profile, "requestTimeoutSeconds", 100,
                        AdoConnection.MaximumRequestTimeoutSeconds)),
                })) throw new JsonException("profiles");
            }
            JsonObject cases = Object(root, "testCases");
            JsonObject results = Object(root, "testResults");
            JsonObject reporting = Object(root, "reporting");
            WarnUnknown(cases, ["maximumSharedStepDepth", "maximumExpandedSteps", "maximumResolvedWorkItems"], "testCases.", culture, warnings);
            WarnUnknown(results, ["historyCount", "historyScope", "maximumReportedFailures", "maximumHistoryRequests", "maximumAttachmentBytes", "maximumTotalAttachmentBytes", "maximumInlineJsonBytes", "maximumInlineTotalBytes"], "testResults.", culture, warnings);
            WarnUnknown(reporting, ["culture"], "reporting.", culture, warnings);
            return new AdoConfiguration
            {
                SchemaVersion = Math.Max(1, schema),
                DefaultProfile = root["defaultProfile"]?.GetValue<string>(),
                Profiles = profiles,
                TestCases = new TestCaseOptions
                {
                    MaximumSharedStepDepth = checked((int)Positive(cases, "maximumSharedStepDepth", 10)),
                    MaximumExpandedSteps = checked((int)Positive(cases, "maximumExpandedSteps", 5000)),
                    MaximumResolvedWorkItems = checked((int)Positive(cases, "maximumResolvedWorkItems", 10000)),
                },
                TestResults = new TestResultOptions
                {
                    HistoryCount = checked((int)Positive(results, "historyCount", 10)),
                    HistoryScope = results["historyScope"]?.GetValue<string>() ?? "SameBranch",
                    MaximumReportedFailures = checked((int)Positive(results, "maximumReportedFailures", 1000)),
                    MaximumHistoryRequests = checked((int)Positive(results, "maximumHistoryRequests", 400)),
                    MaximumAttachmentBytes = Positive(results, "maximumAttachmentBytes", 52428800),
                    MaximumTotalAttachmentBytes = Positive(results, "maximumTotalAttachmentBytes", 524288000),
                    MaximumInlineJsonBytes = Positive(results, "maximumInlineJsonBytes", 262144),
                    MaximumInlineTotalBytes = Positive(results, "maximumInlineTotalBytes", 8388608),
                },
                Reporting = new ReportingOptions { Culture = reporting["culture"]?.GetValue<string>() },
                Warnings = warnings.AsReadOnly(),
                Preserved = root.DeepClone().AsObject(),
            };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new AdoConfigurationException(Messages.Get(AdoMessage.InvalidConfiguration, culture, error.GetType().Name), error);
        }
    }

    public void Save(AdoConfiguration configuration, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(culture);
        // Refuse overwriting a newer on-disk schema even if the caller holds an older snapshot.
        AdoConfiguration existing = Load(culture);
        int schema = Math.Max(existing.SchemaVersion, configuration.SchemaVersion);
        if (schema > 1) throw new AdoConfigurationException(Messages.Get(AdoMessage.NewerConfiguration, culture, schema));
        byte[] bytes = Serialize(configuration);
        AtomicFileReplace.Write(FilePath, bytes, culture, temporary =>
        {
            _ = Parse(File.ReadAllText(temporary), culture);
            validateBeforeCommit?.Invoke(temporary);
        });
    }

    public AdoProfile SetProfile(AdoProfile profile, bool makeDefault, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Name);
        AdoConfiguration existing = Load(culture);
        Dictionary<string, AdoProfile> profiles = new(existing.Profiles, StringComparer.OrdinalIgnoreCase) { [profile.Name] = profile };
        AdoConfiguration updated = Copy(existing, profiles, makeDefault ? profile.Name : existing.DefaultProfile);
        // Round-trip validation also normalizes URLs and checks timeout/authentication.
        AdoConfiguration validated = Parse(Encoding.UTF8.GetString(Serialize(updated)), culture);
        Save(validated, culture);
        return validated.Profiles[profile.Name];
    }

    public void RemoveProfile(string name, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(culture);
        AdoConfiguration existing = Load(culture);
        Dictionary<string, AdoProfile> profiles = new(existing.Profiles, StringComparer.OrdinalIgnoreCase);
        if (!profiles.Remove(name)) throw new AdoConfigurationException(Messages.Get(AdoMessage.MissingProfile, culture, name));
        Save(Copy(existing, profiles, string.Equals(existing.DefaultProfile, name, StringComparison.OrdinalIgnoreCase) ? null : existing.DefaultProfile), culture);
    }

    public static byte[] Serialize(AdoConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        JsonObject root = configuration.Preserved.DeepClone().AsObject();
        root["schemaVersion"] = configuration.SchemaVersion;
        root["defaultProfile"] = configuration.DefaultProfile;
        JsonObject originalProfiles = Object(root, "profiles");
        JsonObject profiles = [];
        foreach ((string name, AdoProfile profile) in configuration.Profiles)
        {
            JsonObject node = originalProfiles[name]?.DeepClone().AsObject() ?? [];
            node["collectionUrl"] = profile.CollectionUri.AbsoluteUri;
            node["defaultProject"] = profile.DefaultProject;
            node["authentication"] = profile.Authentication;
            node["requestTimeoutSeconds"] = profile.RequestTimeoutSeconds;
            profiles[name] = node;
        }
        root["profiles"] = profiles;
        JsonObject cases = Object(root, "testCases");
        cases["maximumSharedStepDepth"] = configuration.TestCases.MaximumSharedStepDepth;
        cases["maximumExpandedSteps"] = configuration.TestCases.MaximumExpandedSteps;
        cases["maximumResolvedWorkItems"] = configuration.TestCases.MaximumResolvedWorkItems;
        root["testCases"] = cases.DeepClone();
        JsonObject results = Object(root, "testResults");
        results["historyCount"] = configuration.TestResults.HistoryCount;
        results["historyScope"] = configuration.TestResults.HistoryScope;
        results["maximumReportedFailures"] = configuration.TestResults.MaximumReportedFailures;
        results["maximumHistoryRequests"] = configuration.TestResults.MaximumHistoryRequests;
        results["maximumAttachmentBytes"] = configuration.TestResults.MaximumAttachmentBytes;
        results["maximumTotalAttachmentBytes"] = configuration.TestResults.MaximumTotalAttachmentBytes;
        results["maximumInlineJsonBytes"] = configuration.TestResults.MaximumInlineJsonBytes;
        results["maximumInlineTotalBytes"] = configuration.TestResults.MaximumInlineTotalBytes;
        root["testResults"] = results.DeepClone();
        JsonObject reporting = Object(root, "reporting");
        reporting["culture"] = configuration.Reporting.Culture;
        root["reporting"] = reporting.DeepClone();
        return Encoding.UTF8.GetBytes(root.ToJsonString(JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static AdoConfiguration Copy(AdoConfiguration source, Dictionary<string, AdoProfile> profiles, string? defaultProfile) => new()
    {
        SchemaVersion = source.SchemaVersion, DefaultProfile = defaultProfile, Profiles = profiles,
        TestCases = source.TestCases, TestResults = source.TestResults, Reporting = source.Reporting,
        Preserved = source.Preserved,
    };

    private static JsonObject Object(JsonObject root, string name) => root[name]?.AsObject() ?? [];

    private static long Positive(JsonObject root, string name, long fallback, long maximum = long.MaxValue)
    {
        long value = root[name]?.GetValue<long>() ?? fallback;
        return value > 0 && value <= maximum ? value : throw new JsonException(name);
    }

    private static void WarnUnknown(JsonObject node, string[] known, string prefix, CultureInfo culture, List<string> warnings)
    {
        foreach (string name in node.Select(pair => pair.Key))
            if (!known.Contains(name, StringComparer.Ordinal))
                warnings.Add(Messages.Get(AdoMessage.UnknownConfiguration, culture, prefix + name));
    }
}
