using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.Http;

internal sealed class AdoHttpPipeline
{
    internal const int MaximumPages = 10000;
    private readonly HttpClient client;
    private readonly Uri collection;
    private readonly TimeSpan requestTimeout;
    private readonly TimeSpan downloadTimeout;
    private readonly TimeSpan inactivityTimeout;
    private readonly ISystemClock clock;
    private readonly IAdoLog log;
    private readonly RetryPolicy retry;
    private readonly RequestCounter? counter;

    internal AdoHttpPipeline(HttpClient client, Uri collection, TimeSpan requestTimeout,
        IAdoLog? log = null, ISystemClock? clock = null, TimeSpan? downloadTimeout = null, TimeSpan? inactivityTimeout = null,
        RequestCounter? counter = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(collection);
        if (requestTimeout <= TimeSpan.Zero
            || requestTimeout > TimeSpan.FromSeconds(Connections.AdoConnection.MaximumRequestTimeoutSeconds))
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        this.counter = counter;
        this.client = client;
        if (this.client.Timeout != Timeout.InfiniteTimeSpan) this.client.Timeout = Timeout.InfiniteTimeSpan;
        this.collection = collection;
        this.requestTimeout = requestTimeout;
        this.log = log ?? new NullAdoLog();
        this.clock = clock ?? new SystemClock();
        this.downloadTimeout = downloadTimeout ?? TimeSpan.FromMinutes(10);
        this.inactivityTimeout = inactivityTimeout ?? TimeSpan.FromSeconds(60);
        retry = new RetryPolicy(this.clock);
    }

    internal async Task<IReadOnlyList<TItem>> GetPagesAsync<TPage, TItem>(
        EndpointDefinition endpoint, JsonTypeInfo<TPage> jsonType, Func<TPage, IReadOnlyList<TItem>?> selectItems,
        Func<TItem, string> identity, CultureInfo culture, CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? routes = null, int? top = null, int pageSize = 100,
        IReadOnlyDictionary<string, string>? parameters = null) where TPage : class
    {
        if (top < 0) throw new ArgumentOutOfRangeException(nameof(top));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        if (endpoint.Paging == PagingStrategy.IdChunks) throw new NotSupportedException(nameof(PagingStrategy.IdChunks));
        List<TItem> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        string? continuation = null;
        int offset = 0;
        if (top == 0) return result;
        for (int page = 1; page <= MaximumPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, string> query = parameters is null ? [] : new(parameters);
            if (endpoint.Paging == PagingStrategy.TopSkip)
            {
                query["$top"] = pageSize.ToString(CultureInfo.InvariantCulture);
                query["$skip"] = offset.ToString(CultureInfo.InvariantCulture);
            }
            else if (endpoint.Paging == PagingStrategy.ContinuationHeader && continuation is not null)
                query["continuationToken"] = continuation;
            (TPage Data, string? Token) received = await ExecuteAsync(endpoint, routes, query, null, culture,
                async (response, token) =>
                {
                    try
                    {
                        string body = await ResponseJson.ReadAsync(response, token).ConfigureAwait(false);
                        TPage data = JsonSerializer.Deserialize(body, jsonType) ?? throw new JsonException();
                        string? next = response.Headers.TryGetValues("x-ms-continuationtoken", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
                        return (data, next);
                    }
                    catch (JsonException error)
                    {
                        throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = endpoint.Name };
                    }
                }, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<TItem> items = selectItems(received.Data)
                ?? throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = endpoint.Name };
            if (items.Any(static item => item is null))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = endpoint.Name };
            if (endpoint.Paging == PagingStrategy.TopSkip && items.Count > 0)
            {
                StringBuilder signature = new();
                foreach (TItem item in items)
                {
                    string key = identity(item);
                    signature.Append(key.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(key);
                }
                if (!seen.Add(signature.ToString())) throw PagingError(endpoint, culture);
            }
            foreach (TItem item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(item);
                if (result.Count == top) return result;
            }
            log.Debug(Messages.Get(AdoMessage.PageLog, culture, endpoint.Name,
                page.ToString(CultureInfo.InvariantCulture), items.Count.ToString(CultureInfo.InvariantCulture)));
            if (endpoint.Paging == PagingStrategy.None) return result;
            if (endpoint.Paging == PagingStrategy.TopSkip)
            {
                if (items.Count == 0) return result;
                offset = checked(offset + items.Count);
            }
            else
            {
                continuation = received.Token;
                if (string.IsNullOrEmpty(continuation)) return result;
                if (!seen.Add(continuation)) throw PagingError(endpoint, culture);
            }
        }
        throw PagingError(endpoint, culture);
    }

    internal Task<byte[]> DownloadAsync(EndpointDefinition endpoint, CultureInfo culture, CancellationToken cancellationToken) =>
        ExecuteAsync(endpoint, null, null, null, culture, async (response, token) =>
        {
            using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using MemoryStream output = new();
            byte[] buffer = new byte[81920];
            while (true)
            {
                using CancellationTokenSource inactivity = CancellationTokenSource.CreateLinkedTokenSource(token);
                inactivity.CancelAfter(inactivityTimeout);
                int read;
                try { read = await source.ReadAsync(buffer, inactivity.Token).ConfigureAwait(false); }
                catch (OperationCanceledException error) when (!token.IsCancellationRequested)
                {
                    throw new AdoTimeoutException(Messages.Get(AdoMessage.Timeout, culture), error) { Operation = endpoint.Name };
                }
                if (read == 0) break;
                await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            }
            return output.ToArray();
        }, cancellationToken);

    internal Task<FileInfo> DownloadFileAsync(EndpointDefinition endpoint, IReadOnlyDictionary<string, string> routes,
        IReadOnlyDictionary<string, string>? query, string destination, AtomicFileWriter writer, Action<string> validate,
        CultureInfo culture, CancellationToken cancellationToken) =>
        ExecuteAsync(endpoint, routes, query, null, culture, async (response, token) =>
        {
            using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using InactivityReadStream timed = new(source, inactivityTimeout, endpoint.Name, culture);
            // ExecuteAsync invokes this consumer anew for every attempt. WriteAsync owns and
            // removes each attempt's temporary before a body failure reaches the retry policy.
            return await writer.WriteAsync(destination, timed, validate, culture, cancellationToken: token).ConfigureAwait(false);
        }, cancellationToken);

    // The consumer runs once per attempt with a fresh body; it must restart its output each time.
    internal Task<T> DownloadStreamAsync<T>(EndpointDefinition endpoint, IReadOnlyDictionary<string, string> routes,
        IReadOnlyDictionary<string, string>? query, CultureInfo culture,
        Func<HttpResponseMessage, Stream, CancellationToken, Task<T>> consume, CancellationToken cancellationToken) =>
        ExecuteAsync(endpoint, routes, query, null, culture, async (response, token) =>
        {
            using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using InactivityReadStream timed = new(source, inactivityTimeout, endpoint.Name, culture);
            return await consume(response, timed, token).ConfigureAwait(false);
        }, cancellationToken);

    internal async Task<T> ExecuteAsync<T>(EndpointDefinition endpoint, IReadOnlyDictionary<string, string>? routes,
        IReadOnlyDictionary<string, string>? query, byte[]? body, CultureInfo culture,
        Func<HttpResponseMessage, CancellationToken, Task<T>> consume, CancellationToken callerToken)
    {
        using CancellationTokenSource operation = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        operation.CancelAfter(endpoint.Timeout == TimeoutClass.Download ? downloadTimeout : requestTimeout);
        CancellationToken token = operation.Token;
        try
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                token.ThrowIfCancellationRequested();
                TimeSpan delay;
                string reason;
                try
                {
                    using HttpRequestMessage request = RequestBuilder.Create(collection, endpoint, culture, routes, query, body);
                    counter?.Increment();
                    long started = Stopwatch.GetTimestamp();
                    using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    log.Verbose(Messages.Get(AdoMessage.RequestLog, culture, endpoint.Method.Method, endpoint.Name,
                        ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                        attempt.ToString(CultureInfo.InvariantCulture)));
                    if (response.IsSuccessStatusCode)
                    {
                        T result = await consume(response, token).ConfigureAwait(false);
                        callerToken.ThrowIfCancellationRequested();
                        return result;
                    }
                    if (!endpoint.IsSafeToRetry || !RetryPolicy.IsRetryable(response.StatusCode) || attempt == 3)
                        throw await ErrorTranslator.TranslateAsync(response, endpoint, culture, token).ConfigureAwait(false);
                    delay = retry.Delay(response, attempt);
                    reason = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                }
                catch (Exception error) when (error is HttpRequestException or IOException)
                {
                    callerToken.ThrowIfCancellationRequested();
                    if (!RetryPolicy.IsTransient(error) || !endpoint.IsSafeToRetry || attempt == 3)
                        throw new AdoRequestException(Messages.Get(AdoMessage.Request, culture), error)
                        { Operation = endpoint.Name, IsRetryable = endpoint.IsSafeToRetry && RetryPolicy.IsTransient(error) };
                    delay = retry.Delay(null, attempt);
                    reason = error.GetType().Name;
                }
                log.Verbose(Messages.Get(AdoMessage.Retry, culture, endpoint.Name,
                    (attempt + 1).ToString(CultureInfo.InvariantCulture), reason));
                await clock.DelayAsync(delay, token).ConfigureAwait(false);
            }
            throw new InvalidOperationException(nameof(ExecuteAsync));
        }
        catch (OperationCanceledException error)
        {
            callerToken.ThrowIfCancellationRequested();
            throw new AdoTimeoutException(Messages.Get(AdoMessage.Timeout, culture), error) { Operation = endpoint.Name };
        }
        catch (AdoTimeoutException)
        {
            callerToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private static AdoResponseFormatException PagingError(EndpointDefinition endpoint, CultureInfo culture) =>
        new(Messages.Get(AdoMessage.PagingGuard, culture)) { Operation = endpoint.Name };
}
