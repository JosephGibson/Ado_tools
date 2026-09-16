using System.Net;
using System.Net.Http;

namespace AdoToolkit.Core.Tests.Http;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responses = new();
    internal List<RequestSnapshot> Requests { get; } = [];
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Fallback { get; set; }

    internal void Enqueue(HttpResponseMessage response) => responses.Enqueue((_, _) => Task.FromResult(response));
    internal void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => responses.Enqueue(response);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RequestSnapshot(request, request.RequestUri!, request.Method.Method, request.Headers.AcceptLanguage.ToString(), body));
        return await (responses.Count > 0 ? responses.Dequeue() : Fallback ?? throw new InvalidOperationException("Unexpected request."))(request, cancellationToken);
    }

    internal static HttpResponseMessage Fixture(string name, int status = 200)
    {
        string body = File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Rest", name));
        return Response(body, status, name.Contains("html", StringComparison.Ordinal) ? "text/html" : "application/json");
    }

    internal static HttpResponseMessage Response(string body, int status = 200, string media = "application/json") =>
        new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, media) };
}
