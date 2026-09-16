namespace AdoToolkit.Core.Builds;

public sealed class BuildQuery
{
    public int? DefinitionId { get; init; }
    public string? DefinitionName { get; init; }
    public string? Branch { get; init; }
    public bool Latest { get; init; }
    public BuildStatus? Status { get; init; }
    public BuildResult? Result { get; init; }
    public int? Top { get; init; }

    internal Dictionary<string, string> Parameters(int? resolvedDefinition)
    {
        if (resolvedDefinition.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolvedDefinition.Value);
        if (Top.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Top.Value);
        Dictionary<string, string> values = [];
        if (resolvedDefinition.HasValue) values["definitions"] = resolvedDefinition.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(Branch)) values["branchName"] = Branch.StartsWith("refs/", StringComparison.Ordinal) ? Branch : "refs/heads/" + Branch;
        BuildStatus status = Status ?? (Latest ? BuildStatus.Completed : BuildStatus.All);
        values["statusFilter"] = EnumParameter(status);
        if (Result.HasValue) values["resultFilter"] = EnumParameter(Result.Value);
        if (Latest)
        {
            values["$top"] = "1";
            values["queryOrder"] = "finishTimeDescending";
        }
        return values;
    }

    private static string EnumParameter<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
