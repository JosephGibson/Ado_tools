namespace AdoToolkit.Commands.Infrastructure;

internal static class CultureCapture
{
    internal static CultureInfo Capture(SessionState session)
    {
        string? name = session.PSVariable.GetValue("PSUICulture") as string;
        return string.IsNullOrEmpty(name) ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(name);
    }
}
