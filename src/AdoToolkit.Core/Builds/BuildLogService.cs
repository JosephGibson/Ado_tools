using System.Net.Http;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.Builds;

public sealed class BuildLogService
{
    private readonly AdoHttpPipeline pipeline;
    private readonly AtomicFileWriter writer;
    private readonly IAdoLog log;

    public BuildLogService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
        : this(new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log),
            new AtomicFileWriter(), log) { }

    internal BuildLogService(AdoHttpPipeline pipeline, AtomicFileWriter writer, IAdoLog? log = null)
    {
        this.pipeline = pipeline;
        this.writer = writer;
        this.log = log ?? new NullAdoLog();
    }

    // V-14 assumption: zero-based, inclusive endpoints. All range arithmetic lives here.
    internal static (long StartLine, long EndLine)? TailRange(long lineCount, int tail)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tail);
        return lineCount == 0 ? null : (Math.Max(0, lineCount - tail), lineCount - 1);
    }

    public async Task<FileInfo> SaveAsync(string project, int buildId, int logId, string destination,
        int? tail, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logId);
        if (tail <= 0) throw new ArgumentOutOfRangeException(nameof(tail));
        Dictionary<string, string> routes = new()
        {
            ["project"] = project, ["buildId"] = buildId.ToString(CultureInfo.InvariantCulture),
            ["logId"] = logId.ToString(CultureInfo.InvariantCulture),
        };
        IReadOnlyList<BuildLogDto> logs = await pipeline.GetPagesAsync(EndpointRegistry.BuildLogsList, AdoJsonContext.Default.BuildLogPageDto,
            static page => page.Value, static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken, routes).ConfigureAwait(false);
        HashSet<int> ids = [];
        foreach (BuildLogDto item in logs)
            if (item.Id < 1 || item.LineCount < 0 || !ids.Add(item.Id))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "BuildLogsList" };
        long? lineCount = logs.FirstOrDefault(item => item.Id == logId)?.LineCount;
        Dictionary<string, string>? query = null;
        if (tail.HasValue)
        {
            if (!lineCount.HasValue) log.Warning(Messages.Get(AdoMessage.BuildLogTailUnknown, culture,
                buildId.ToString(CultureInfo.InvariantCulture), logId.ToString(CultureInfo.InvariantCulture)));
            else if (TailRange(lineCount.Value, tail.Value) is { } range)
                query = new() { ["startLine"] = range.StartLine.ToString(CultureInfo.InvariantCulture), ["endLine"] = range.EndLine.ToString(CultureInfo.InvariantCulture) };
        }
        return await pipeline.DownloadFileAsync(EndpointRegistry.BuildLog, routes, query, destination, writer, temporary =>
        {
            if (new FileInfo(temporary).Length == 0 && lineCount != 0)
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "BuildLog" };
        }, culture, cancellationToken).ConfigureAwait(false);
    }
}
