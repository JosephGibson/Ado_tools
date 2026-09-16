using System.Net;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

internal static partial class TestFailureMarkup
{
    internal static string Policy(string html) => WebUtility.HtmlDecode(Csp().Match(html).Groups[1].Value);
    internal static string WithoutScripts(string html) => Scripts().Replace(html, "");
    internal static string NormalizeGolden(string html) => Csp().Replace(Scripts().Replace(html, "<script>__SCRIPT_ASSET__</script>"),
        match => Hashes().Replace(match.Value, "sha256-__SCRIPT_SHA256__"));
    internal static string Text(string html) => WebUtility.HtmlDecode(Tags().Replace(html, ""));

    [GeneratedRegex("<script\\b(?<attributes>[^>]*)>(?<body>[\\s\\S]*?)</script\\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex Scripts();
    [GeneratedRegex("<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]*)\">", RegexOptions.CultureInvariant)]
    private static partial Regex Csp();
    [GeneratedRegex("sha256-[A-Za-z0-9+/=]+", RegexOptions.CultureInvariant)]
    private static partial Regex Hashes();
    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)]
    internal static partial Regex Tags();
}
