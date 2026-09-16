using System.Net.Http;
using System.Text;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Builds;

public sealed class BuildDefinitionService
{
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public BuildDefinitionService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        this.connection = connection;
        pipeline = new(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<IReadOnlyList<AdoBuildDefinition>> GetDefinitionsAsync(string project, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        IReadOnlyList<BuildDefinitionDto> values = await pipeline.GetPagesAsync(EndpointRegistry.BuildDefinitionsList,
            AdoJsonContext.Default.BuildDefinitionPageDto, static page => page.Value, static item => item.Id.ToString(CultureInfo.InvariantCulture),
            culture, cancellationToken, new Dictionary<string, string> { ["project"] = project }).ConfigureAwait(false);
        List<AdoBuildDefinition> result = [];
        HashSet<int> seen = [];
        foreach (BuildDefinitionDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Id < 1 || string.IsNullOrEmpty(value.Name) || !seen.Add(value.Id))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "BuildDefinitionsList" };
            result.Add(new AdoBuildDefinition
            {
                Id = value.Id, Name = value.Name, Path = value.Path, Revision = value.Revision, QueueStatus = value.QueueStatus,
                TeamProject = project, CollectionUri = connection.CollectionUri,
                WebUrl = AdoWebLinks.BuildDefinition(connection.CollectionUri, project, value.Id),
            });
        }
        return result.AsReadOnly();
    }

    internal async Task<int> ResolveAsync(string project, string name, CultureInfo culture, CancellationToken cancellationToken)
    {
        IReadOnlyList<AdoBuildDefinition> definitions = await GetDefinitionsAsync(project, culture, cancellationToken).ConfigureAwait(false);
        AdoBuildDefinition[] matches = definitions.Where(item => string.Equals(item.Name.Normalize(NormalizationForm.FormC),
            name.Normalize(NormalizationForm.FormC), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            throw new AdoRequestException(Messages.Get(AdoMessage.BuildDefinitionMatches, culture, matches.Length.ToString(CultureInfo.InvariantCulture)))
            { Operation = "BuildDefinitionsList", Project = project };
        return matches[0].Id;
    }
}
