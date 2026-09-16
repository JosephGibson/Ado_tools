namespace AdoToolkit.Core.Configuration;

public static class ReportCultureResolver
{
    public static ReportCultureResult Resolve(string? explicitCulture, string? configuredCulture, CultureInfo sessionCulture)
    {
        ArgumentNullException.ThrowIfNull(sessionCulture);
        string name = explicitCulture ?? configuredCulture ?? sessionCulture.Name;
        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(name);
            if (culture.TwoLetterISOLanguageName is "en" or "fr")
                return new ReportCultureResult { Culture = culture };
        }
        catch (CultureNotFoundException) { }
        return new ReportCultureResult
        {
            Culture = CultureInfo.GetCultureInfo("en"),
            Warnings = [Messages.Get(AdoMessage.UnsupportedCulture, sessionCulture, name)],
        };
    }
}
