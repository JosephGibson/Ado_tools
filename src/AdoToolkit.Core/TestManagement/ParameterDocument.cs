using System.Collections.ObjectModel;

namespace AdoToolkit.Core.TestManagement;

/// <summary>Parsed data and unresolved mapping; joining shared sets belongs to the service.</summary>
public sealed class ParameterDocument
{
    public AdoTestParameters Parameters { get; init; } = new();
    public IReadOnlyDictionary<string, int> SharedMapping { get; init; } = ReadOnlyDictionary<string, int>.Empty;
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
}
