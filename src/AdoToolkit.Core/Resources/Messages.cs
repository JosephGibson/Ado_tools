using System.Resources;

namespace AdoToolkit.Core.Resources;

public static class Messages
{
    private static readonly ResourceManager ResourceManager = new("AdoToolkit.Core.Resources.Strings", typeof(Messages).Assembly);

    public static string Get(AdoMessage key, CultureInfo culture, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(culture);
        string template = ResourceManager.GetString(key.ToString(), culture) ?? throw new InvalidOperationException(key.ToString());
        return string.Format(culture, template, arguments);
    }
}
