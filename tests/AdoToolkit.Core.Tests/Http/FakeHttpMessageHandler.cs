using System.Net;
using System.Net.Http;

namespace AdoToolkit.Core.Tests.Http;

// Requests may arrive from several threads at once: the queue and the request list are locked.
// Requests is read by tests after the calls have ended.
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Lock gate = new();
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responses = new();
    private int inFlight;
    internal List<RequestSnapshot> Requests { get; } = [];
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Fallback { get; set; }
    // The highest number of requests that were being answered at the same time.
    internal int PeakInFlight { get; private set; }

    internal void Enqueue(HttpResponseMessage response) => Enqueue((_, _) => Task.FromResult(response));
    internal void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
    {
        lock (gate) responses.Enqueue(response);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
        lock (gate)
        {
            Requests.Add(new RequestSnapshot(request, request.RequestUri!, request.Method.Method, request.Headers.AcceptLanguage.ToString(), body));
            respond = responses.Count > 0 ? responses.Dequeue() : Fallback ?? throw new InvalidOperationException("Unexpected request.");
            PeakInFlight = Math.Max(PeakInFlight, ++inFlight);
        }
        try { return await respond(request, cancellationToken); }
        finally
        {
            lock (gate) inFlight--;
        }
    }

    internal static HttpResponseMessage Fixture(string name, int status = 200)
    {
        string body = File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Rest", name));
        return Response(body, status, name.Contains("html", StringComparison.Ordinal) ? "text/html" : "application/json");
    }

    internal static HttpResponseMessage Response(string body, int status = 200, string media = "application/json") =>
        new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, media) };
}
