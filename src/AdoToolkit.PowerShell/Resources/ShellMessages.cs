using System.Resources;

namespace AdoToolkit.Resources;

internal static class ShellMessages
{
    private static readonly ResourceManager Manager = new("AdoToolkit.PowerShell.Resources.Strings", typeof(ShellMessages).Assembly);
    internal static string Get(AdoMessage key, CultureInfo culture) =>
        Manager.GetString(key.ToString(), culture) ?? Messages.Get(key, culture);
}
