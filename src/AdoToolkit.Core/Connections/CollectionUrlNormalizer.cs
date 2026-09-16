namespace AdoToolkit.Core.Connections;

public static class CollectionUrlNormalizer
{
    private static readonly HashSet<string> RejectedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "_apis", "_git", "_workitems", "_build", "_testPlans", "_testManagement", "_settings",
    };

    public static CollectionUrlNormalizationResult Normalize(string input) => Normalize(input, CultureInfo.InvariantCulture);

    public static CollectionUrlNormalizationResult Normalize(string input, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(culture);
        string value = input.Trim();
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            value = value[1..^1].Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || value.Contains('?', StringComparison.Ordinal) ||
            value.Contains('#', StringComparison.Ordinal) ||
            uri.GetComponents(UriComponents.Path, UriFormat.Unescaped).Split('/').Any(RejectedSegments.Contains))
            throw new AdoConfigurationException(Messages.Get(AdoMessage.InvalidCollectionUrl, culture));
        if (uri.IdnHost.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
            uri.IdnHost.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            throw new AdoConfigurationException(Messages.Get(AdoMessage.ServicesUnsupported, culture));
        return new CollectionUrlNormalizationResult
        {
            CollectionUri = new Uri(uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/')),
            Warnings = uri.Scheme == Uri.UriSchemeHttp ? [Messages.Get(AdoMessage.PlainHttp, culture)] : [],
        };
    }
}
