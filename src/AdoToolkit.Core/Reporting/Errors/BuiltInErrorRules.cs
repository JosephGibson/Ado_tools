using AdoToolkit.Core.Configuration;

namespace AdoToolkit.Core.Reporting.Errors;

// The generic errors every report knows: failures of the environment that say nothing about the
// test. The list is short on purpose: a rule that is too wide would set a real regression apart.
// Each text was read from the local runtime or a downloaded package, as its comment says; a form
// that could not be read stays out (V-41, V-42), and a configured rule can add it.
internal static class BuiltInErrorRules
{
    internal static IReadOnlyList<ErrorRule> All { get; } =
    [
        // Winsock 10061 in English, as .NET 10 and .NET Framework 4.8 show it; Chrome's net error; the
        // .NET Framework WebException status ConnectFailure (System.dll, net_webstatus_ConnectFailure).
        new("ConnectionRefused", ["*No connection could be made because the target machine actively refused it*",
            "*net::ERR_CONNECTION_REFUSED*", "*Unable to connect to the remote server*"], true, "ConnectionRefused"),
        // Winsock 11001 in English; .NET Framework net_webstatus_NameResolutionFailure; Chrome's net error.
        new("NameResolution", ["*No such host is known*", "*The remote name could not be resolved*", "*net::ERR_NAME_NOT_RESOLVED*"],
            true, "NameResolution"),
        // The status shapes of System.Net.Http net_http_message_not_success_statuscode_reason ("{0} ({1})")
        // and of System.dll net_servererror with WebException's "({0}) {1}": neither depends on the
        // language, only the line must name the HTTP exception or hold its English text.
        new("ServerUnavailable", ["*: 502 (*", "*: 503 (*", "*: 504 (*", "*(502)*", "*(503)*", "*(504)*"], true, "ServerUnavailable",
            ["HttpRequestException", "WebException", "Response status code does not indicate success", "The remote server returned an error"]),
        // Selenium.WebDriver 4.50.0: the W3C error names, the HTTP timeout of a command and the driver
        // service that does not start. "chrome not reachable" and "disconnected" are chromedriver's (V-42).
        new("WebDriverSession", ["session not created*", "invalid session id*", "no such window*", "*chrome not reachable*",
            "*disconnected: not connected to DevTools*", "*The HTTP request to the remote WebDriver server for URL * timed out after * seconds*",
            "*Timed out waiting for driver service to initialize after*"], true, "WebDriverSession"),
        // chromedriver's page load timeout (V-42).
        new("PageLoadTimeout", ["timeout: Timed out receiving message from renderer*"], true, "PageLoadTimeout"),
        // Playwright's action timeout, the driver's "Timeout {0}ms exceeded." in Microsoft.Playwright
        // 1.41.2 and 1.63.0, which the environment causes more often than the test. A wait for an
        // event, "… exceeded while waiting for event …", stays specific: a download or a popup that
        // never comes says something about the test.
        new("PlaywrightTimeout", ["Timeout *ms exceeded."], true, "PlaywrightTimeout"),
    ];

    // The rules of one report: the configured ones in file order, compiled for that report, then the
    // built-in ones. The first rule that matches wins.
    internal static IReadOnlyList<ErrorRule> AfterConfigured(IReadOnlyList<ErrorRuleOptions> configured)
    {
        ArgumentNullException.ThrowIfNull(configured);
        return [.. configured.Select(static rule => new ErrorRule(rule.Name, rule.Patterns, rule.Generic)), .. All];
    }
}
