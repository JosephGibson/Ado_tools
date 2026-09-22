using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AdoToolkit.Core.Http;

internal static class RequestBuilder
{
    internal static HttpRequestMessage Create(Uri collection, EndpointDefinition endpoint, CultureInfo culture,
        IReadOnlyDictionary<string, string>? routeValues = null, IReadOnlyDictionary<string, string>? queryValues = null,
        byte[]? body = null)
    {
        string[] segments = endpoint.RouteTemplate.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            if (!segment.StartsWith('{') || !segment.EndsWith('}')) continue;
            string value = routeValues?[segment[1..^1]] ?? throw new ArgumentException(segment, nameof(routeValues));
            // Uri collapses "." and ".." even after escaping, which would leave the collection.
            if (!IsPathSegment(value)) throw new ArgumentException(segment, nameof(routeValues));
            segments[i] = Uri.EscapeDataString(value);
        }
        List<string> query = [];
        if (queryValues is not null)
            foreach ((string key, string value) in queryValues)
            {
                if (key.Equals("api-version", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(key, nameof(queryValues));
                query.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value));
            }
        query.Add("api-version=" + Uri.EscapeDataString(endpoint.ApiVersion));
        Uri uri = new(collection.AbsoluteUri.TrimEnd('/') + "/" + string.Join('/', segments) + "?" + string.Join('&', query));
        HttpRequestMessage request = new(endpoint.Method, uri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        request.Headers.AcceptLanguage.ParseAdd(string.IsNullOrEmpty(culture.Name) ? "en" : culture.Name);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(endpoint.Accept
            ?? (endpoint.Timeout == TimeoutClass.Download ? "application/octet-stream" : "application/json")));
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        return request;
    }

    // A route value names exactly one path segment below the collection: not blank, "." or "..".
    internal static bool IsPathSegment(string? value) => !string.IsNullOrWhiteSpace(value) && value is not ("." or "..");
}
