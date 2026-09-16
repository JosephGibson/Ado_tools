using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Builds;

public sealed class BuildService
{
    private readonly AdoConnection connection;
    private readonly HttpClient client;
    private readonly IAdoLog? log;
    private readonly AdoHttpPipeline pipeline;

    public BuildService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        this.connection = connection;
        this.client = client;
        this.log = log;
        pipeline = new(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<IReadOnlyList<AdoBuild>> GetBuildsAsync(string project, BuildQuery query, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        int? definition = query.DefinitionId;
        if (query.DefinitionName is not null)
            definition = await new BuildDefinitionService(client, connection, log).ResolveAsync(project, query.DefinitionName, culture, cancellationToken).ConfigureAwait(false);
        return await ListAsync(project, query.Parameters(definition), query.Latest ? 1 : query.Top, culture, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdoBuild> GetByIdAsync(string project, int buildId, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        IReadOnlyList<AdoBuild> builds = await ListAsync(project, new Dictionary<string, string>
            { ["buildIds"] = buildId.ToString(CultureInfo.InvariantCulture) }, null, culture, cancellationToken).ConfigureAwait(false);
        if (builds.Count == 0)
            throw new AdoNotFoundException(Messages.Get(AdoMessage.BuildNotFound, culture, buildId.ToString(CultureInfo.InvariantCulture)))
            { Operation = "BuildsList", Project = project };
        if (builds.Count != 1 || builds[0].Id != buildId)
            throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "BuildsList" };
        return builds[0];
    }

    // BuildGet: the single-build route used by the failed-test report (§15.9 step 1) [Verify V-25].
    public async Task<AdoBuild> GetAsync(string project, int buildId, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        ArgumentNullException.ThrowIfNull(culture);
        BuildDto value = await pipeline.ExecuteAsync(EndpointRegistry.BuildGet, new Dictionary<string, string>
        {
            ["project"] = project,
            ["buildId"] = buildId.ToString(CultureInfo.InvariantCulture),
        }, null, null, culture, async (response, token) =>
        {
            try
            {
                byte[] body = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                return JsonSerializer.Deserialize(body, AdoJsonContext.Default.BuildDto) ?? throw new JsonException();
            }
            catch (JsonException error)
            {
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error)
                { Operation = "BuildGet" };
            }
        }, cancellationToken).ConfigureAwait(false);
        if (value.Id != buildId) throw FormatError(culture, "BuildGet");
        return Map(value, project, culture, "BuildGet");
    }

    private async Task<IReadOnlyList<AdoBuild>> ListAsync(string project, Dictionary<string, string> parameters, int? top,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        IReadOnlyList<BuildDto> values = await pipeline.GetPagesAsync(EndpointRegistry.BuildsList, AdoJsonContext.Default.BuildPageDto,
            static page => page.Value, static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            new Dictionary<string, string> { ["project"] = project }, top, parameters: parameters).ConfigureAwait(false);
        List<AdoBuild> result = [];
        HashSet<int> seen = [];
        foreach (BuildDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(value.Id)) throw FormatError(culture, "BuildsList");
            result.Add(Map(value, project, culture, "BuildsList"));
        }
        return result.AsReadOnly();
    }

    private AdoBuild Map(BuildDto value, string project, CultureInfo culture, string operation)
    {
        if (value.Id < 1 || string.IsNullOrEmpty(value.BuildNumber) || value.Definition is null || value.Definition.Id < 1
            || string.IsNullOrEmpty(value.Definition.Name))
            throw FormatError(culture, operation);
        return new AdoBuild
        {
            Id = value.Id, BuildNumber = value.BuildNumber,
            Definition = new() { Id = value.Definition.Id, Name = value.Definition.Name },
            SourceBranch = value.SourceBranch, SourceVersion = value.SourceVersion, RepositoryId = value.Repository?.Id,
            RepositoryType = value.Repository?.Type, Status = value.Status, Result = value.Result, Reason = value.Reason,
            RequestedFor = value.RequestedFor?.ToDomain(), QueueTime = value.QueueTime?.ToUniversalTime(),
            StartTime = value.StartTime?.ToUniversalTime(), FinishTime = value.FinishTime?.ToUniversalTime(), Uri = value.Uri,
            TeamProject = project, CollectionUri = connection.CollectionUri,
            WebUrl = AdoWebLinks.Build(connection.CollectionUri, project, value.Id),
        };
    }

    private static AdoResponseFormatException FormatError(CultureInfo culture, string operation) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = operation };
}
