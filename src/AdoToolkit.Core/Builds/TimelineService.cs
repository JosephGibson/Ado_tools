using System.Net.Http;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Builds;

public sealed class TimelineService
{
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public TimelineService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        this.connection = connection;
        pipeline = new(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<IReadOnlyList<AdoTimelineRecord>> GetTimelineAsync(string project, int buildId, CultureInfo culture, CancellationToken cancellationToken)
    {
        IReadOnlyList<TimelineRecordDto> values = await pipeline.GetPagesAsync(EndpointRegistry.BuildTimeline, AdoJsonContext.Default.TimelineDto,
            static page => page.Records, static item => item.Id.ToString(), culture, cancellationToken, Routes(project, buildId)).ConfigureAwait(false);
        List<AdoTimelineRecord> records = [];
        foreach (TimelineRecordDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(value.Name) || string.IsNullOrEmpty(value.Type) || value.Attempt < 1 || value.Log?.Id < 1
                || value.ErrorCount < 0 || value.WarningCount < 0
                || value.Issues?.Any(issue => issue is null || string.IsNullOrEmpty(issue.Type) || issue.Message is null) == true
                || value.PreviousAttempts?.Any(attempt => attempt is null || attempt.Attempt < 1) == true) throw TimelineTree.FormatError(culture);
            records.Add(new AdoTimelineRecord
            {
                Id = value.Id, ParentId = value.ParentId, Type = value.Type, Name = value.Name, Order = value.Order,
                State = value.State, Result = value.Result, StartTime = value.StartTime?.ToUniversalTime(), FinishTime = value.FinishTime?.ToUniversalTime(),
                Attempt = value.Attempt, Identifier = value.Identifier, PreviousAttempts = (value.PreviousAttempts ?? []).AsReadOnly(),
                ErrorCount = value.ErrorCount, WarningCount = value.WarningCount, Issues = (value.Issues ?? []).AsReadOnly(),
                LogId = value.Log?.Id, BuildId = buildId, CollectionUri = connection.CollectionUri,
            });
        }
        return new TimelineTree(records, culture, cancellationToken).Ordered;
    }

    public async Task<IReadOnlyList<AdoBuildFailure>> GetFailuresAsync(AdoBuild build, bool includeWarnings, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (!build.CollectionUri.Equals(connection.CollectionUri))
            throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, culture));
        IReadOnlyList<AdoTimelineRecord> records = await GetTimelineAsync(build.TeamProject, build.Id, culture, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<BuildLogDto> logs = await pipeline.GetPagesAsync(EndpointRegistry.BuildLogsList, AdoJsonContext.Default.BuildLogPageDto,
            static page => page.Value, static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            Routes(build.TeamProject, build.Id)).ConfigureAwait(false);
        Dictionary<int, long?> counts = [];
        foreach (BuildLogDto item in logs)
        {
            if (item.Id < 1 || item.LineCount < 0 || !counts.TryAdd(item.Id, item.LineCount))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "BuildLogsList" };
        }
        return BuildFailureDeriver.Derive(build, records, counts, includeWarnings, culture, cancellationToken);
    }

    private static Dictionary<string, string> Routes(string project, int buildId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        return new() { ["project"] = project, ["buildId"] = buildId.ToString(CultureInfo.InvariantCulture) };
    }
}
