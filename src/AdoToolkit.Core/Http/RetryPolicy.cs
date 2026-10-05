using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace AdoToolkit.Core.Http;

internal sealed class RetryPolicy(ISystemClock clock)
{
    internal static bool IsRetryable(HttpStatusCode status) => (int)status is 429 or 502 or 503 or 504;

    internal static bool IsTransient(Exception error) => error switch
    {
        HttpRequestException request => request.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError
            || (request.HttpRequestError == HttpRequestError.Unknown && request.InnerException is SocketException),
        // An error found while reading a body carries its kind too: a malformed response is not
        // worth another attempt (§6.5); a connection that ended or failed mid-body is.
        HttpIOException body => body.HttpRequestError is HttpRequestError.ResponseEnded or HttpRequestError.ConnectionError
            or HttpRequestError.NameResolutionError or HttpRequestError.Unknown,
        IOException => true,
        _ => false,
    };

    internal TimeSpan Delay(HttpResponseMessage? response, int attempt)
    {
        if (response is not null && response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values))
        {
            string? value = values.FirstOrDefault();
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds))
                return TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 60));
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date))
                return TimeSpan.FromSeconds(Math.Clamp((date - clock.UtcNow).TotalSeconds, 0, 60));
        }
        return TimeSpan.FromSeconds(Math.Pow(2, attempt - 1) * (0.8 + 0.4 * Math.Clamp(clock.NextJitter(), 0, 1)));
    }
}
