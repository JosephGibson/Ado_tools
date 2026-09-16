using System.Net.Http;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestManagement;

public sealed class TestPlanService
{
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public TestPlanService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<IReadOnlyList<AdoTestPlan>> GetPlansAsync(string project, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentNullException.ThrowIfNull(culture);
        EndpointDefinition endpoint = EndpointRegistry.TestPlansList;
        IReadOnlyList<TestPlanDto> values = await pipeline.GetPagesAsync(endpoint, AdoJsonContext.Default.TestPlanPageDto,
            static page => page.Value, static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            new Dictionary<string, string> { ["project"] = project }).ConfigureAwait(false);
        List<AdoTestPlan> plans = [];
        HashSet<int> seen = [];
        foreach (TestPlanDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Id < 1 || string.IsNullOrEmpty(value.Name) || value.RootSuite is null || value.RootSuite.Id < 1 || !seen.Add(value.Id))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = endpoint.Name };
            plans.Add(new AdoTestPlan
            {
                Id = value.Id, Name = value.Name, RootSuiteId = value.RootSuite.Id, TeamProject = project,
                CollectionUri = connection.CollectionUri, WebUrl = AdoWebLinks.TestPlan(connection.CollectionUri, project, value.Id),
            });
        }
        return plans.AsReadOnly();
    }
}
