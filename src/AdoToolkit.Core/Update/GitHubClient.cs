using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace AdoToolkit.Core.Update;

// The updater's only network code, outside AdoHttpPipeline by decision. It sends GET requests without
// credentials, follows redirects itself to the three allowed hosts over HTTPS, bounds every response
// by bytes and time, and does not retry: each message tells the user to run the command again.
internal sealed class GitHubClient(HttpClient client, UpdateHttp.Limits limits, TimeProvider time, Version running, IAdoLog log)
{
    private const int BufferSize = 81920;

    internal async Task<byte[]> GetLatestReleaseAsync(CultureInfo culture, CancellationToken cancellationToken)
    {
        using MemoryStream body = new();
        await FetchAsync(UpdateHttp.LatestRelease, api: true, UpdateHttp.LatestOperation, body, hash: null, limits.RequestTime, progress: null,
            () => new AdoNotFoundException(Messages.Get(AdoMessage.UpdateNoRelease, culture)) { Operation = UpdateHttp.LatestOperation },
            length => length > limits.ReleaseBytes ? ReleaseFormat(culture) : null, culture, cancellationToken).ConfigureAwait(false);
        return body.ToArray();
    }

    // A small asset, such as the checksum file, read whole into memory.
    internal async Task<byte[]> DownloadSmallAsync(Version version, ReleaseReader.Asset asset, CultureInfo culture, CancellationToken cancellationToken)
    {
        using MemoryStream body = new();
        await DownloadAsync(version, asset, body, hash: null, limits.RequestTime, progress: null, culture, cancellationToken).ConfigureAwait(false);
        return body.ToArray();
    }

    // Writes exactly asset.Size bytes to destination. A body of another length fails; a destination
    // write failure, such as a full disk, passes through as the I/O error it is.
    internal Task DownloadAsync(Version version, ReleaseReader.Asset asset, Stream destination, IncrementalHash? hash, TimeSpan total,
        Action<long>? progress, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return FetchAsync(UpdateHttp.Download(version, asset.Name), api: false, UpdateHttp.DownloadOperation, destination, hash, total, progress,
            () => new AdoNotFoundException(Messages.Get(AdoMessage.UpdateAssetMissing, culture, UpdateHttp.VersionText(version), asset.Name))
            { Operation = UpdateHttp.DownloadOperation },
            length => length == asset.Size ? null : SizeMismatch(asset, length, culture), culture, cancellationToken, asset.Size);
    }

    // check receives a length that a header announced or that the body reached, and returns the
    // error for it, or null. With expected set, a body that ends shorter fails too.
    private async Task FetchAsync(Uri start, bool api, string operation, Stream destination, IncrementalHash? hash, TimeSpan total,
        Action<long>? progress, Func<AdoException> notFound, Func<long, AdoException?> check, CultureInfo culture,
        CancellationToken cancellationToken, long? expected = null)
    {
        using CancellationTokenSource timer = new(total, time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try
        {
            using HttpResponseMessage response = await SendAsync(start, api, operation, notFound, culture, linked.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is long announced && check(announced) is { } refused) throw refused;
            Stream body;
            try { body = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false); }
            catch (Exception error) when (error is HttpRequestException or IOException) { throw ConnectionLost(start, operation, culture, error); }
            using (body)
            {
                byte[] buffer = new byte[BufferSize];
                long received = 0;
                long limit = expected ?? long.MaxValue;
                while (true)
                {
                    int read = await ReadAsync(body, buffer, start, operation, culture, linked.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    received += read;
                    // More than announced fails before the extra bytes are kept.
                    if ((received > limit || expected is null) && check(received) is { } tooLong) throw tooLong;
                    hash?.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), linked.Token).ConfigureAwait(false);
                    progress?.Invoke(received);
                }
                if (expected is not null && check(received) is { } truncated) throw truncated;
            }
        }
        catch (OperationCanceledException error) when (timer.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw total >= TimeSpan.FromMinutes(2)
                ? new AdoTimeoutException(Messages.Get(AdoMessage.UpdateTimeoutMinutes, culture, Describe(start),
                    ((int)total.TotalMinutes).ToString(CultureInfo.InvariantCulture)), error) { Operation = operation }
                : new AdoTimeoutException(Messages.Get(AdoMessage.UpdateTimeoutSeconds, culture, Describe(start),
                    ((int)total.TotalSeconds).ToString(CultureInfo.InvariantCulture)), error) { Operation = operation };
        }
    }

    // Each read has its own inactivity timer.
    private async Task<int> ReadAsync(Stream body, byte[] buffer, Uri uri, string operation, CultureInfo culture, CancellationToken cancellationToken)
    {
        using CancellationTokenSource stall = new(limits.StallTime, time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stall.Token);
        try { return await body.ReadAsync(buffer, linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException error) when (stall.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new AdoTimeoutException(Messages.Get(AdoMessage.UpdateStalled, culture, uri.IdnHost,
                ((int)limits.StallTime.TotalSeconds).ToString(CultureInfo.InvariantCulture)), error) { Operation = operation };
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            throw ConnectionLost(uri, operation, culture, error);
        }
    }

    // Returns a 200 response, following redirects only to an allowed host over HTTPS.
    private async Task<HttpResponseMessage> SendAsync(Uri start, bool api, string operation, Func<AdoException> notFound,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        Uri current = start;
        for (int hop = 0; ; hop++)
        {
            EnsureAllowed(start, current, operation, culture);
            HttpResponseMessage response;
            long started = Stopwatch.GetTimestamp();
            using (HttpRequestMessage request = new(HttpMethod.Get, current))
            {
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue(UpdateHttp.Product, UpdateHttp.VersionText(running)));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(api ? UpdateHttp.ApiAccept : UpdateHttp.DownloadAccept));
                if (api) request.Headers.Add(UpdateHttp.ApiVersionHeader, UpdateHttp.ApiVersion);
                try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false); }
                catch (HttpRequestException error) { throw Unreachable(error, current, operation, culture); }
                // The handler's ConnectTimeout, which also covers the proxy tunnel and TLS, ends the request
                // as a cancellation that holds a TimeoutException and that neither token caused.
                catch (OperationCanceledException error) when (error.InnerException is TimeoutException && !cancellationToken.IsCancellationRequested)
                {
                    throw new AdoRequestException(Messages.Get(AdoMessage.UpdateUnreachable, culture, current.IdnHost,
                        nameof(SocketsHttpHandler.ConnectTimeout)), error) { Operation = operation };
                }
            }
            // Every path but the one that returns the response disposes it, the verbose log included:
            // it throws once the caller has cancelled.
            bool returned = false;
            try
            {
                int status = (int)response.StatusCode;
                log.Verbose(Messages.Get(AdoMessage.UpdateRequestLog, culture, Describe(current), status.ToString(CultureInfo.InvariantCulture),
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)));
                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    Uri? location = response.Headers.Location;
                    if (location is null || hop >= limits.MaximumRedirects)
                        throw new AdoRedirectException(Messages.Get(AdoMessage.UpdateRedirectLimit, culture, Describe(start)))
                        { Operation = operation, StatusCode = status };
                    current = location.IsAbsoluteUri ? location : new Uri(current, location);
                    log.Verbose(Messages.Get(AdoMessage.UpdateRedirectLog, culture, SchemeAndHost(current)));
                    continue;
                }
                if (status != 200) throw Translate(response, current, operation, notFound, culture);
                returned = true;
                return response;
            }
            finally
            {
                if (!returned) response.Dispose();
            }
        }
    }

    private static void EnsureAllowed(Uri start, Uri target, string operation, CultureInfo culture)
    {
        if (target.IsAbsoluteUri && string.Equals(target.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && target.IsDefaultPort
            && target.UserInfo.Length == 0 && UpdateHttp.AllowedHosts.Contains(target.IdnHost, StringComparer.OrdinalIgnoreCase)) return;
        throw new AdoRedirectException(Messages.Get(AdoMessage.UpdateRedirectBlocked, culture, Describe(start), SchemeAndHost(target)))
        { Operation = operation };
    }

    private AdoException Translate(HttpResponseMessage response, Uri uri, string operation, Func<AdoException> notFound, CultureInfo culture)
    {
        int status = (int)response.StatusCode;
        string code = status.ToString(CultureInfo.InvariantCulture);
        if (status == 407)
            return new AdoAuthenticationException(Messages.Get(AdoMessage.UpdateProxyAuthentication, culture, uri.IdnHost))
            { Operation = operation, StatusCode = status };
        if (status == 429 || (status == 403 && IsRateLimited(response))) return Throttled(response, operation, status, culture);
        return status switch
        {
            403 => new AdoAuthorizationException(Messages.Get(AdoMessage.UpdateForbidden, culture, Describe(uri))) { Operation = operation, StatusCode = status },
            404 => notFound(),
            >= 500 => new AdoServerException(Messages.Get(AdoMessage.UpdateServer, culture, Describe(uri), code)) { Operation = operation, StatusCode = status },
            _ => new AdoRequestException(Messages.Get(AdoMessage.UpdateRequest, culture, Describe(uri), code)) { Operation = operation, StatusCode = status },
        };
    }

    // GitHub answers an exhausted limit with 403 or 429 and x-ratelimit-remaining: 0, or, for its
    // secondary limits, with Retry-After.
    private static bool IsRateLimited(HttpResponseMessage response) =>
        response.Headers.RetryAfter is not null
        || (response.Headers.TryGetValues("x-ratelimit-remaining", out IEnumerable<string>? values)
            && string.Equals(values.FirstOrDefault()?.Trim(), "0", StringComparison.Ordinal));

    // The reset time is shown in the user's time zone, and only when it is within a day.
    private AdoThrottledException Throttled(HttpResponseMessage response, string operation, int status, CultureInfo culture)
    {
        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset? reset = null;
        if (response.Headers.TryGetValues("x-ratelimit-reset", out IEnumerable<string>? values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            && seconds is > 0 and < 253402300800)
            reset = DateTimeOffset.FromUnixTimeSeconds(seconds);
        else if (response.Headers.RetryAfter is { } after)
            reset = after.Date ?? (after.Delta is { } delta ? now + delta : null);
        if (reset is { } at && at > now && at - now <= TimeSpan.FromDays(1))
            return new AdoThrottledException(Messages.Get(AdoMessage.UpdateRateLimited, culture, TimeZoneInfo.ConvertTime(at, time.LocalTimeZone).DateTime))
            { Operation = operation, StatusCode = status, IsRetryable = true };
        return new AdoThrottledException(Messages.Get(AdoMessage.UpdateRateLimitedLater, culture)) { Operation = operation, StatusCode = status, IsRetryable = true };
    }

    // A proxy that refuses the CONNECT tunnel surfaces here, never as a response: a 407 means the
    // Windows sign-in was not accepted, any other status that the proxy blocks the host.
    private static AdoException Unreachable(HttpRequestException error, Uri uri, string operation, CultureInfo culture)
    {
        if (error.HttpRequestError == HttpRequestError.ProxyTunnelError && error.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
            return new AdoAuthenticationException(Messages.Get(AdoMessage.UpdateProxyAuthentication, culture, uri.IdnHost), error)
            { Operation = operation, StatusCode = 407 };
        if (error.HttpRequestError == HttpRequestError.ProxyTunnelError && error.StatusCode is { } status)
            return new AdoRequestException(Messages.Get(AdoMessage.UpdateProxyRefused, culture, uri.IdnHost,
                ((int)status).ToString(CultureInfo.InvariantCulture)), error) { Operation = operation, StatusCode = (int)status };
        return new AdoRequestException(Messages.Get(AdoMessage.UpdateUnreachable, culture, uri.IdnHost, error.HttpRequestError.ToString()), error)
        { Operation = operation };
    }

    private static AdoRequestException ConnectionLost(Uri uri, string operation, CultureInfo culture, Exception error) =>
        new(Messages.Get(AdoMessage.UpdateConnectionLost, culture, Describe(uri)), error) { Operation = operation };

    private static AdoResponseFormatException ReleaseFormat(CultureInfo culture) =>
        new(Messages.Get(AdoMessage.UpdateReleaseFormat, culture)) { Operation = UpdateHttp.LatestOperation };

    private static AdoResponseFormatException SizeMismatch(ReleaseReader.Asset asset, long length, CultureInfo culture) =>
        new(Messages.Get(AdoMessage.UpdateSizeMismatch, culture, asset.Name, length, asset.Size)) { Operation = UpdateHttp.DownloadOperation };

    // The host and path of a request; the query, which carries a signed token on the asset host, is
    // never shown or logged.
    internal static string Describe(Uri uri) => UpdateGuards.Printable(uri.IdnHost + uri.AbsolutePath, 256);

    private static string SchemeAndHost(Uri uri) =>
        uri.IsAbsoluteUri ? UpdateGuards.Printable(uri.Scheme + "://" + uri.IdnHost, 128) : "";
}
