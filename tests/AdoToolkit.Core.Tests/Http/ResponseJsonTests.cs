using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Http;

// The decoding policy of every JSON response: UTF-8 is parsed as it arrives, any other charset or a
// UTF-16 or UTF-32 byte order mark is decoded first, and no body may pass the size limit.
public sealed class ResponseJsonTests
{
    private const string Json = "{\"value\":[{\"id\":1,\"name\":\"Équipe\"}]}";

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("utf-8", false)]
    [InlineData("utf-8", true)]
    [InlineData("\"UTF-8\"", false)]
    [InlineData("UTF-8", true)]
    public async Task Utf8BodiesParseWithOrWithoutCharsetAndByteOrderMark(string? charset, bool mark)
    {
        byte[] body = Encoding.UTF8.GetBytes(Json);
        using HttpResponseMessage response = Response(mark ? [0xEF, 0xBB, 0xBF, .. body] : body, charset);
        Assert.Equal("Équipe", Name(await Deserialize(response)));
    }

    // A byte order mark names the encoding whatever the declared charset, as before streaming.
    [Theory]
    [InlineData("utf-16", null)]
    [InlineData("utf-16", "utf-8")]
    [InlineData("utf-16BE", null)]
    [InlineData("utf-32", null)]
    [InlineData("utf-32BE", "utf-8")]
    public async Task UnicodeByteOrderMarksDecodeEvenWhenTheCharsetSaysUtf8(string encoding, string? charset)
    {
        Encoding marked = Encoding.GetEncoding(encoding);
        using HttpResponseMessage response = Response([.. marked.GetPreamble(), .. marked.GetBytes(Json)], charset);
        Assert.Equal("Équipe", Name(await Deserialize(response)));
    }

    [Theory]
    [InlineData("iso-8859-1")]
    [InlineData("utf-16")]
    public async Task OtherCharsetsAreDecodedWhole(string charset)
    {
        using HttpResponseMessage response = Response(Encoding.GetEncoding(charset).GetBytes(Json), charset);
        Assert.Equal("Équipe", Name(await Deserialize(response)));
    }

    [Theory]
    [InlineData(new byte[] { 0x7B, 0x22, 0x76, 0x61, 0x6C, 0x75, 0x65, 0x22, 0x3A, 0x5B, 0x7B, 0x22, 0x6E, 0x61, 0x6D, 0x65, 0x22, 0x3A, 0x22, 0xC3, 0x28, 0x22, 0x7D, 0x5D, 0x7D })]
    [InlineData(new byte[] { 0x7B, 0x22, 0x76, 0x61, 0x6C, 0x75, 0x65, 0x22, 0x3A, 0x5B, 0x5D, 0x7D, 0x7D })]
    [InlineData(new byte[] { 0x7B, 0x22, 0x76, 0x61, 0x6C, 0x75, 0x65, 0x22, 0x3A })]
    public async Task InvalidUtf8TrailingDataAndTruncationAreJsonErrors(byte[] body)
    {
        using HttpResponseMessage response = Response(body, null);
        await Assert.ThrowsAnyAsync<JsonException>(() => Deserialize(response));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("utf-16")]
    public async Task ABodyLongerThanTheLimitFailsWithItsOperationOnceTheLimitIsPassed(string? charset)
    {
        Encoding encoding = charset is null ? new UTF8Encoding(false) : Encoding.GetEncoding(charset);
        byte[] body = encoding.GetBytes("{\"value\":[" + new string(' ', 200) + "]}");
        using HttpResponseMessage exact = Response(body, charset, seekable: false);
        Assert.Empty((await Deserialize(exact, limit: body.Length))!.Value!);
        using HttpResponseMessage longer = Response(body, charset, seekable: false);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => Deserialize(longer, limit: body.Length - 1));
        Assert.Equal("TestRunsList", error.Operation);
        Assert.Equal(Messages.Get(AdoMessage.ResponseTooLarge, CultureInfo.InvariantCulture, 0), error.Message);
    }

    // A declared length over the limit fails before the body is read.
    [Fact]
    public async Task ADeclaredLengthOverTheLimitFailsBeforeReading()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(new UnreadableStream()) };
        response.Content.Headers.ContentLength = ResponseJson.MaximumBytes + 1;
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => Deserialize(response, ResponseJson.MaximumBytes));
        Assert.Equal(Messages.Get(AdoMessage.ResponseTooLarge, CultureInfo.InvariantCulture, 256), error.Message);
    }

    [Fact]
    public void TheLimitIs256MiB() => Assert.Equal(256L * 1024 * 1024, ResponseJson.MaximumBytes);

    [Theory]
    [InlineData(null, true)]
    [InlineData("utf-16", true)]
    [InlineData("utf-8", false)]
    public async Task DocumentsFollowTheSamePolicy(string? encoding, bool mark)
    {
        Encoding chosen = encoding is null ? new UTF8Encoding(false) : Encoding.GetEncoding(encoding);
        byte[] body = chosen.GetBytes(Json);
        using HttpResponseMessage response = Response(mark ? [.. (encoding is null ? new UTF8Encoding(true) : chosen).GetPreamble(), .. body] : body, encoding);
        using JsonDocument document = await ResponseJson.ParseAsync(response, EndpointRegistry.WorkItemTypeCategory, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        Assert.Equal("Équipe", document.RootElement.GetProperty("value")[0].GetProperty("name").GetString());
    }

    private static Task<TestRunPageDto?> Deserialize(HttpResponseMessage response, long limit = ResponseJson.MaximumBytes) =>
        ResponseJson.DeserializeAsync(response, AdoJsonContext.Default.TestRunPageDto, EndpointRegistry.TestRunsList, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken, limit);

    private static string? Name(TestRunPageDto? page) => Assert.Single(page!.Value!).Name;

    // Without seeking, the content has no length, as a compressed response once decoded.
    private static HttpResponseMessage Response(byte[] body, string? charset, bool seekable = true)
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = seekable ? new ByteArrayContent(body) : new StreamContent(new ForwardOnlyStream(body)),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = charset };
        return response;
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override bool CanSeek => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The body was read.");
    }
}
