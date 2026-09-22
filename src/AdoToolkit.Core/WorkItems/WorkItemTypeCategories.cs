using System.Text.Json;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.WorkItems;

// The work item type names of one category of a project, such as Microsoft.BugCategory.
internal static class WorkItemTypeCategories
{
    internal static Task<IReadOnlyList<string>> ReadAsync(AdoHttpPipeline pipeline, string project, string category,
        CultureInfo culture, CancellationToken cancellationToken) =>
        pipeline.ExecuteAsync(EndpointRegistry.WorkItemTypeCategory,
            new Dictionary<string, string> { ["project"] = project, ["category"] = category }, null, null, culture,
            async (response, token) =>
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(await ResponseJson.ReadAsync(response, token).ConfigureAwait(false));
                    JsonElement values = document.RootElement.GetProperty("workItemTypes");
                    return (IReadOnlyList<string>)Array.AsReadOnly(values.EnumerateArray().Select(value =>
                        value.GetProperty("name").GetString() ?? throw new JsonException()).ToArray());
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
                { throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = "WorkItemTypeCategory" }; }
            }, cancellationToken);
}
