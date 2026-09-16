using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Connections;

public sealed class ProjectService
{
    public static string ApiVersion => EndpointRegistry.ProjectsList.ApiVersion;
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public ProjectService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<IReadOnlyList<AdoProject>> GetProjectsAsync(CultureInfo culture, CancellationToken cancellationToken, int? top = null)
    {
        ArgumentNullException.ThrowIfNull(culture);
        IReadOnlyList<ProjectDto> values = await pipeline.GetPagesAsync(EndpointRegistry.ProjectsList, AdoJsonContext.Default.ProjectPageDto,
            static page => page.Value, static item => item.Id ?? "", culture, cancellationToken, top: top).ConfigureAwait(false);
        List<AdoProject> projects = [];
        foreach (ProjectDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(value.Id, out Guid id) || string.IsNullOrEmpty(value.Name))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "ProjectsList" };
            projects.Add(new AdoProject { Id = id, Name = value.Name, Description = value.Description, State = value.State, CollectionUri = connection.CollectionUri });
        }
        return projects.AsReadOnly();
    }
}
