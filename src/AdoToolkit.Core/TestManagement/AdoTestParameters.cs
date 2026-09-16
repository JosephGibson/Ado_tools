namespace AdoToolkit.Core.TestManagement;

public sealed class AdoTestParameters
{
    public AdoParameterSource Source { get; init; }
    public IReadOnlyList<string> Names { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } = Array.Empty<IReadOnlyDictionary<string, string>>();
    public IReadOnlyList<AdoSharedParameterInfo> SharedParameterSets { get; init; } = Array.Empty<AdoSharedParameterInfo>();
}
