using System.Net.Http;

namespace AdoToolkit.Core.Http;

internal sealed record EndpointDefinition(
    string Name, HttpMethod Method, string RouteTemplate, string ApiVersion,
    PagingStrategy Paging, bool IsSafeToRetry, TimeoutClass Timeout, int ChunkSize = 0, string? Accept = null);
