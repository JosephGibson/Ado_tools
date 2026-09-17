using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Http;

internal static class ErrorTranslator
{
    internal static async Task<AdoException> TranslateAsync(HttpResponseMessage response, EndpointDefinition endpoint, CultureInfo culture, CancellationToken cancellationToken)
    {
        int status = (int)response.StatusCode;
        string? remote = null;
        string? media = response.Content.Headers.ContentType?.MediaType;
        bool json = media is null || media.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || media.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (status != 401 && json)
            {
                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                byte[] buffer = new byte[8193];
                int read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (read <= 8192)
                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(ResponseJson.Decode(buffer.AsSpan(0, read),
                            response.Content.Headers.ContentType?.CharSet));
                        if (document.RootElement.ValueKind == JsonValueKind.Object &&
                            document.RootElement.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
                            remote = Sanitize(message.GetString(), 1024);
                    }
                    catch (JsonException) { }
            }
        }
        catch (Exception error) when (error is IOException or HttpRequestException)
        {
            // Once a status is known, an unreadable error body must not replace it or alter retries.
        }
        AdoMessage key = status switch
        {
            >= 300 and <= 399 => AdoMessage.Redirect,
            401 => AdoMessage.Authentication,
            403 => AdoMessage.Authorization,
            404 => AdoMessage.NotFound,
            429 => AdoMessage.Throttled,
            >= 500 => AdoMessage.Server,
            _ => AdoMessage.Request,
        };
        string hint = key == AdoMessage.Redirect
            ? Messages.Get(key, culture, RedirectTarget(response.Headers.Location))
            : Messages.Get(key, culture);
        if (status == 400 && remote is not null &&
            (remote.Contains("api-version", StringComparison.OrdinalIgnoreCase) || remote.Contains("api version", StringComparison.OrdinalIgnoreCase)))
            hint += " " + Messages.Get(AdoMessage.RegistryMismatch, culture);
        string text = string.IsNullOrWhiteSpace(remote) ? hint : remote + " " + hint;
        string? correlation = response.Headers.TryGetValues("ActivityId", out IEnumerable<string>? ids) ? Sanitize(ids.FirstOrDefault(), 128) : null;
        return status switch
        {
            >= 300 and <= 399 => new AdoRedirectException(text) { Operation = endpoint.Name, StatusCode = status, CorrelationId = correlation },
            401 => new AdoAuthenticationException(text) { Operation = endpoint.Name, StatusCode = status, CorrelationId = correlation },
            403 => new AdoAuthorizationException(text) { Operation = endpoint.Name, StatusCode = status, CorrelationId = correlation },
            404 => new AdoNotFoundException(text) { Operation = endpoint.Name, StatusCode = status, CorrelationId = correlation },
            429 => new AdoThrottledException(text) { Operation = endpoint.Name, StatusCode = status, CorrelationId = correlation, IsRetryable = true },
            >= 500 => new AdoServerException(text) { Operation = endpoint.Name, StatusCode = status, CorrelationId = correlation, IsRetryable = RetryPolicy.IsRetryable(response.StatusCode) },
            _ => new AdoRequestException(text) { Operation = endpoint.Name, StatusCode = status, CorrelationId = correlation },
        };
    }

    internal static string? Sanitize(string? input, int limit)
    {
        if (input is null) return null;
        string bounded = input[..Math.Min(input.Length, 4096)];
        string plain = WebUtility.HtmlDecode(Regex.Replace(bounded, "<[^>]*>", "", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
        StringBuilder result = new();
        foreach (char character in plain)
        {
            if (result.Length >= limit) break;
            result.Append(char.IsControl(character) ? ' ' : character);
        }
        return result.ToString();
    }

    private static string RedirectTarget(Uri? target)
    {
        if (target is null) return "";
        if (!target.IsAbsoluteUri)
        {
            string path = target.OriginalString.Split('?', '#')[0];
            return Sanitize(path, 512) ?? "";
        }
        if (target.Scheme is not ("http" or "https")) return target.Scheme;
        UriBuilder safe = new(target) { UserName = "", Password = "", Query = "", Fragment = "" };
        return safe.Uri.AbsoluteUri;
    }
}
