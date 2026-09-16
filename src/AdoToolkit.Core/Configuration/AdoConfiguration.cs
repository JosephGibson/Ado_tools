using System.Text.Json.Nodes;

namespace AdoToolkit.Core.Configuration;

public sealed class AdoConfiguration
{
    public int SchemaVersion { get; init; } = 1;
    public bool IsReadOnly => SchemaVersion > 1;
    public string? DefaultProfile { get; init; }
    public IReadOnlyDictionary<string, AdoProfile> Profiles { get; init; } = new Dictionary<string, AdoProfile>(StringComparer.OrdinalIgnoreCase);
    public TestCaseOptions TestCases { get; init; } = new();
    public TestResultOptions TestResults { get; init; } = new();
    public ReportingOptions Reporting { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = [];
    internal JsonObject Preserved { get; init; } = [];
}
