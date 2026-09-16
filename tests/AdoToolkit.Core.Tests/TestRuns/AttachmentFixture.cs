using System.Net;
using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// Attachment metadata comes from the list-shaped fixtures; content is served per attachment ID by
// a fake handler. Every unrouted content request fails the test.
internal sealed class AttachmentFixture
{
    internal const string Project = "Équipe Web";
    internal const string FolderName = "Build-401-TestFailures.files-20260916T133000000Z";
    private readonly Dictionary<int, Queue<Func<HttpResponseMessage>>> content = [];

    internal FakeHttpMessageHandler Handler { get; } = new();
    internal FakeClock Clock { get; } = new();

    internal AttachmentFixture()
    {
        Handler.Fallback = (request, _) =>
        {
            string path = request.RequestUri!.AbsolutePath;
            int id = int.Parse(path[(path.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture);
            if (!content.TryGetValue(id, out Queue<Func<HttpResponseMessage>>? responses) || responses.Count == 0)
                throw new InvalidOperationException("No content route for " + path);
            Func<HttpResponseMessage> next = responses.Count > 1 ? responses.Dequeue() : responses.Peek();
            return Task.FromResult(next());
        };
    }

    internal static byte[] Bytes(string name) =>
        File.ReadAllBytes(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Attachments", name));

    internal static IReadOnlyList<AdoTestAttachment> Metadata(string fixture, int runId, int resultId, int? subResultId = null)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", fixture)));
        return document.RootElement.GetProperty("value").EnumerateArray().Select(item => new AdoTestAttachment
        {
            Id = item.GetProperty("id").GetInt32(), RunId = runId, ResultId = resultId, SubResultId = subResultId,
            FileName = item.GetProperty("fileName").GetString()!,
            Size = item.TryGetProperty("size", out JsonElement size) ? size.GetInt64() : null,
            AttachmentType = item.GetProperty("attachmentType").GetString(),
            Comment = item.TryGetProperty("comment", out JsonElement comment) ? comment.GetString() : null,
            Kind = AttachmentKinds.FromFileName(item.GetProperty("fileName").GetString()),
        }).ToArray();
    }

    // One detailed failure whose attempts hold the given attachments, in report order.
    internal static AdoTestFailure Failure(params IReadOnlyList<AdoTestAttachment>[] attempts) => new()
    {
        Ordinal = 1, ShortName = "SubmitOrder", TestName = "Synthetic.CheckoutTests.SubmitOrder",
        CollectionUri = TestRunFixture.Connection.CollectionUri,
        Attempts = attempts.Select((attachments, index) => new AdoTestAttempt
        {
            Number = index + 1, RunId = 201, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure,
            Attachments = attachments,
        }).ToArray(),
    };

    internal AttachmentFixture Serve(int id, byte[] bytes) => Serve(id, () => Ok(bytes));

    internal AttachmentFixture Serve(int id, params Func<HttpResponseMessage>[] responses)
    {
        content[id] = new Queue<Func<HttpResponseMessage>>(responses);
        return this;
    }

    internal static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    internal static HttpResponseMessage Status(int status) =>
        FakeHttpMessageHandler.Response("{\"message\":\"Synthetic failure.\"}", status);

    internal AttachmentDownloader Downloader(HttpClient client, TestResultOptions limits, IAdoLog? log = null) =>
        new(new AdoHttpPipeline(client, TestRunFixture.Connection.CollectionUri, TimeSpan.FromSeconds(100), log, Clock), limits, log);

    internal IEnumerable<int> RequestedIds() => Handler.Requests.Select(request =>
        int.Parse(request.Uri.AbsolutePath[(request.Uri.AbsolutePath.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture));
}
