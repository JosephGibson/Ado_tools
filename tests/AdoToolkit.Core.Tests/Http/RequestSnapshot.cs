using System.Net.Http;

namespace AdoToolkit.Core.Tests.Http;

internal sealed record RequestSnapshot(HttpRequestMessage Request, Uri Uri, string Method, string Language, string? Body);
