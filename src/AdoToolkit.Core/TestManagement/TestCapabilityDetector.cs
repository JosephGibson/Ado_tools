using System.Net.Http;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestManagement;

public sealed class TestCapabilityDetector
{
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;
    private readonly SessionCache<IReadOnlyList<string>> cache;

    public TestCapabilityDetector(HttpClient client, AdoConnection connection,
        SessionCache<IReadOnlyList<string>> cache, IAdoLog? log = null)
    {
        this.connection = connection;
        this.cache = cache;
        pipeline = new(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<bool> IsStepContainerAsync(bool hasStepsField, string project, string workItemType,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        if (hasStepsField) return true;
        string key = connection.CollectionUri.AbsoluteUri.TrimEnd('/') + "|" + connection.Authentication + "|" + project;
        if (!cache.TryGet(key, out IReadOnlyList<string>? types))
        {
            List<string> members = [];
            foreach (string category in new[] { "Microsoft.TestCaseCategory", "Microsoft.SharedStepCategory" })
                members.AddRange(await WorkItemTypeCategories.ReadAsync(pipeline, project, category, culture, cancellationToken)
                    .ConfigureAwait(false));
            types = Array.AsReadOnly(members.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            cache.Set(key, types);
        }
        return types.Contains(workItemType, StringComparer.OrdinalIgnoreCase);
    }
}
