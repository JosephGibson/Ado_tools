using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.WorkItems;

public sealed class WiqlService
{
    public const int MaximumResults = 20000;
    // Language-neutral server code for the result size limit; the error shape is V-06.
    private const string ServerLimitCode = "VS402337";
    private readonly HttpClient client;
    private readonly AdoConnection connection;
    private readonly IAdoLog? log;
    private readonly AdoHttpPipeline pipeline;

    public WiqlService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.client = client;
        this.connection = connection;
        this.log = log;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    /// <summary>
    /// Runs a flat query. Without <paramref name="top"/>, a result of <see cref="MaximumResults"/> or more IDs
    /// is rejected as potentially incomplete; nothing is truncated. Link and tree queries raise
    /// <see cref="NotSupportedException"/>. The query text is never logged.
    /// </summary>
    public async Task<AdoWiqlResult> QueryAsync(string project, string query, int? top, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(culture);
        if (top is < 1 or > MaximumResults) throw new ArgumentOutOfRangeException(nameof(top));
        EndpointDefinition endpoint = EndpointRegistry.Wiql;
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new WiqlRequestDto { Query = query }, AdoJsonContext.Default.WiqlRequestDto);
        Dictionary<string, string>? parameters = top.HasValue
            ? new Dictionary<string, string> { ["$top"] = top.Value.ToString(CultureInfo.InvariantCulture) }
            : null;
        WiqlResponseDto response;
        try
        {
            response = await pipeline.ExecuteAsync(endpoint, new Dictionary<string, string> { ["project"] = project }, parameters, body, culture,
                async (message, token) =>
                {
                    try
                    {
                        string bytes = await ResponseJson.ReadAsync(message, token).ConfigureAwait(false);
                        return JsonSerializer.Deserialize(bytes, AdoJsonContext.Default.WiqlResponseDto) ?? throw new JsonException();
                    }
                    catch (JsonException error) { throw FormatError(culture, error); }
                }, cancellationToken).ConfigureAwait(false);
        }
        catch (AdoRequestException error) when (error.StatusCode == 400 && error.Message.Contains(ServerLimitCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new AdoRequestException(string.Join(' ', error.Message, Messages.Get(AdoMessage.WiqlLimitHint, culture)), error)
            { Operation = endpoint.Name, StatusCode = error.StatusCode, CorrelationId = error.CorrelationId, Project = project };
        }
        bool linked = response.WorkItemRelations is { ValueKind: not JsonValueKind.Null }
            || (response.QueryType is not null && !response.QueryType.Equals("flat", StringComparison.OrdinalIgnoreCase))
            || (response.QueryResultType is not null && !response.QueryResultType.Equals("workItem", StringComparison.OrdinalIgnoreCase));
        if (linked) throw new NotSupportedException(Messages.Get(AdoMessage.WiqlNotFlat, culture));
        if (response.WorkItems is null || response.AsOf is null) throw FormatError(culture, null);
        List<int> ids = new(Math.Min(response.WorkItems.Length, MaximumResults));
        HashSet<int> seen = [];
        foreach (WiqlWorkItemReferenceDto item in response.WorkItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null || item.Id < 1 || !seen.Add(item.Id)) throw FormatError(culture, null);
            ids.Add(item.Id);
        }
        if (!top.HasValue && ids.Count >= MaximumResults)
        {
            string limit = MaximumResults.ToString("N0", culture);
            throw new AdoRequestException(string.Join(' ', Messages.Get(AdoMessage.WiqlResultLimit, culture, limit), Messages.Get(AdoMessage.WiqlLimitHint, culture)))
            { Operation = endpoint.Name, Project = project };
        }
        // An explicit limit is intentional and recorded in LimitApplied.
        if (top.HasValue && ids.Count > top.Value) ids.RemoveRange(top.Value, ids.Count - top.Value);
        List<string> columns = [];
        foreach (WiqlColumnDto column in response.Columns ?? [])
        {
            if (column is null || string.IsNullOrWhiteSpace(column.ReferenceName)) throw FormatError(culture, null);
            columns.Add(column.ReferenceName);
        }
        return new AdoWiqlResult
        {
            Ids = ids.AsReadOnly(), Columns = columns.AsReadOnly(), AsOf = response.AsOf.Value.ToUniversalTime(),
            TeamProject = project, CollectionUri = connection.CollectionUri, LimitApplied = top,
        };
    }

    /// <summary>
    /// Retrieves the work items of a result in query order, requesting the returned columns
    /// together with the convenience fields. IDs no longer returned by the server are omitted.
    /// </summary>
    public Task<IReadOnlyList<AdoWorkItem>> HydrateAsync(AdoWiqlResult result, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(culture);
        if (!result.CollectionUri.Equals(connection.CollectionUri))
            throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, culture));
        return new WorkItemService(client, connection, log).GetWorkItemsAsync(result.Ids, culture, cancellationToken, result.Columns);
    }

    private static AdoResponseFormatException FormatError(CultureInfo culture, Exception? error) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = "Wiql" };
}
