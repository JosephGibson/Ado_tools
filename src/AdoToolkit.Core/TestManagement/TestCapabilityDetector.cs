using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

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
            {
                IReadOnlyList<string> found = await pipeline.ExecuteAsync(EndpointRegistry.WorkItemTypeCategory,
                    new Dictionary<string, string> { ["project"] = project, ["category"] = category }, null, null, culture,
                    async (response, token) =>
                    {
                        try
                        {
                            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false));
                            JsonElement values = document.RootElement.GetProperty("workItemTypes");
                            return (IReadOnlyList<string>)Array.AsReadOnly(values.EnumerateArray().Select(value =>
                                value.GetProperty("name").GetString() ?? throw new JsonException()).ToArray());
                        }
                        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
                        { throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = "WorkItemTypeCategory" }; }
                    }, cancellationToken).ConfigureAwait(false);
                members.AddRange(found);
            }
            types = Array.AsReadOnly(members.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            cache.Set(key, types);
        }
        return types.Contains(workItemType, StringComparer.OrdinalIgnoreCase);
    }
}
