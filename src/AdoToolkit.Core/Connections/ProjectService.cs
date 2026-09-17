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

    // A project URL passed as the collection URL fails the projects call with 404. This lists the
    // projects of the parent URL once and returns a suggestion only when the last path segment names
    // one of them; any failure of that check means no suggestion.
    public static async Task<CollectionUrlSuggestion?> SuggestCollectionAsync(HttpClient client, AdoConnection connection,
        CultureInfo culture, CancellationToken cancellationToken, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(culture);
        string path = connection.CollectionUri.AbsolutePath.TrimEnd('/');
        int slash = path.LastIndexOf('/');
        if (slash <= 0 || slash == path.Length - 1) return null;
        string segment = Uri.UnescapeDataString(path[(slash + 1)..]);
        AdoConnection parent = new()
        {
            CollectionUri = new Uri(connection.CollectionUri.GetLeftPart(UriPartial.Authority) + path[..slash]),
            Authentication = connection.Authentication,
            RequestTimeoutSeconds = connection.RequestTimeoutSeconds,
        };
        IReadOnlyList<AdoProject> projects;
        try
        {
            projects = await new ProjectService(client, parent, log).GetProjectsAsync(culture, cancellationToken).ConfigureAwait(false);
        }
        catch (AdoException)
        {
            return null;
        }
        AdoProject? match = projects.FirstOrDefault(project => string.Equals(project.Name, segment, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : new CollectionUrlSuggestion { CollectionUri = parent.CollectionUri, Project = match.Name };
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
