using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Tests.Builds;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Tests.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-6")]
public sealed partial class AttachmentDownloaderTests
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
    private static readonly TestResultOptions Limits = new()
    { MaximumAttachmentBytes = 2048, MaximumTotalAttachmentBytes = 1_000_000, MaximumInlineJsonBytes = 256 };
    private static readonly byte[] Png = AttachmentFixture.Bytes("pattern.png");
    private static readonly byte[] Other = AttachmentFixture.Bytes("other-bytes.txt");
    private static readonly byte[] LargeJson = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("\"synthetic value\"", 66)) + "]");
    private static readonly byte[] DeepJson = Encoding.UTF8.GetBytes(new string('[', 65) + new string(']', 65));

    // Attachments fixtures 1–5, 7 and 8 in one report-ordered pass.
    [Fact]
    public async Task DownloadsEveryKindWithToolkitNamesContentChecksAndLimits()
    {
        using TestDirectory directory = new();
        string folder = Directory.CreateDirectory(Path.Combine(directory.Root, "work")).FullName;
        IReadOnlyList<AdoTestAttachment> result = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11);
        AdoTestAttachment[] attachments = [.. result.Take(13), With(result[13], 301)];
        AttachmentFixture fixture = new AttachmentFixture()
            .Serve(6001, Png).Serve(6002, AttachmentFixture.Bytes("png-without-signature.txt"))
            .Serve(6003, AttachmentFixture.Bytes("valid.json")).Serve(6004, AttachmentFixture.Bytes("malformed.json.txt"))
            .Serve(6005, LargeJson).Serve(6006, DeepJson).Serve(6007, AttachmentFixture.Bytes("test-output.html"))
            .Serve(6008, Other).Serve(6009, Other)
            .Serve(6011, () => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new UnknownLengthContent(new byte[3000]) })
            .Serve(6012, () => AttachmentFixture.Status(503))
            .Serve(6013, () => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(new ChunkedLogStream(Other, failAfterPrefix: true)) },
                () => AttachmentFixture.Ok(Other))
            .Serve(6014, Png);
        AdoTestFailure failure = AttachmentFixture.Failure(attachments[..7], attachments[7..]);
        CapturingLog log = new();
        using HttpClient client = new(fixture.Handler);
        AttachmentDownloadResult downloaded = await fixture.Downloader(client, Limits, log).DownloadAsync([failure], AttachmentFixture.Project,
            folder, AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken);

        AdoTestAttachment[] output = [.. downloaded.Failures.Single().Attempts.SelectMany(a => a.Attachments)];
        Assert.Equal(attachments.Select(a => a.Id), output.Select(a => a.Id));
        Assert.Equal([Downloaded, Mismatch, Downloaded, Mismatch, Downloaded, Mismatch, Downloaded, Downloaded, Downloaded,
            AdoTestAttachmentStatus.TooLarge, AdoTestAttachmentStatus.TooLarge, AdoTestAttachmentStatus.Failed, Downloaded, Downloaded],
            output.Select(a => a.DownloadStatus));
        string?[] expectedNames = ["r201-11-a6001.png", "r201-11-a6002.bin", "r201-11-a6003.json", "r201-11-a6004.bin", "r201-11-a6005.json",
            "r201-11-a6006.bin", "r201-11-a6007.html", "r201-11-a6008.bin", "r201-11-a6009.bin", null, null, null, "r201-11-a6013.bin",
            "r201-11-s301-a6014.png"];
        Assert.Equal(expectedNames.Select(name => name is null ? null : AttachmentFixture.FolderName + "/" + name), output.Select(a => a.LocalRelativePath));
        // Kinds still come from the remote extension; a mismatch changes only the local name and status.
        Assert.Equal(AdoTestAttachmentKind.Png, output[1].Kind);

        // Files are byte-identical, named by the toolkit, and no partial file remains.
        Dictionary<string, byte[]> expectedBytes = new(StringComparer.Ordinal)
        {
            ["r201-11-a6001.png"] = Png, ["r201-11-a6002.bin"] = AttachmentFixture.Bytes("png-without-signature.txt"),
            ["r201-11-a6003.json"] = AttachmentFixture.Bytes("valid.json"), ["r201-11-a6004.bin"] = AttachmentFixture.Bytes("malformed.json.txt"),
            ["r201-11-a6005.json"] = LargeJson, ["r201-11-a6006.bin"] = DeepJson, ["r201-11-a6007.html"] = AttachmentFixture.Bytes("test-output.html"),
            ["r201-11-a6008.bin"] = Other, ["r201-11-a6009.bin"] = Other, ["r201-11-a6013.bin"] = Other, ["r201-11-s301-a6014.png"] = Png,
        };
        Assert.Equal(expectedBytes.Keys.Order(StringComparer.Ordinal), Names(folder).Order(StringComparer.Ordinal));
        foreach ((string name, byte[] bytes) in expectedBytes)
        {
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(folder, name)));
            Assert.Equal(bytes.LongLength, downloaded.Files[name]);
        }
        Assert.Equal(expectedBytes.Count, downloaded.Files.Count);
        Assert.Empty(Directory.GetDirectories(folder));

        // Report order; the declared oversize file is never requested; failures retry three times;
        // the partial body restarts the file; the sub-result is selected by its parameter.
        Assert.Equal([6001, 6002, 6003, 6004, 6005, 6006, 6007, 6008, 6009, 6011, 6012, 6012, 6012, 6013, 6013, 6014], fixture.RequestedIds());
        foreach (RequestSnapshot request in fixture.Handler.Requests)
        {
            Assert.StartsWith("/Collection/%C3%89quipe%20Web/_apis/test/Runs/201/Results/11/attachments/", request.Uri.AbsolutePath, StringComparison.Ordinal);
            Assert.Equal("application/octet-stream", request.Request.Headers.Accept.ToString());
            Assert.Contains("api-version=6.0-preview.1", request.Uri.Query, StringComparison.Ordinal);
        }
        Assert.Equal("?testSubResultId=301&api-version=6.0-preview.1", fixture.Handler.Requests[^1].Uri.Query);
        Assert.Equal("?api-version=6.0-preview.1", fixture.Handler.Requests[0].Uri.Query);

        // Diagnostics name IDs only, never remote text.
        Assert.Equal(new (string, string)[] {
            (DiagnosticCodes.AttachmentContentMismatch, "6002|201|11|PNG"), (DiagnosticCodes.AttachmentContentMismatch, "6004|201|11|JSON"),
            (DiagnosticCodes.AttachmentContentMismatch, "6006|201|11|JSON"), (DiagnosticCodes.AttachmentTooLarge, "6010|201|11|2048"),
            (DiagnosticCodes.AttachmentTooLarge, "6011|201|11|2048"), (DiagnosticCodes.AttachmentDownloadFailed, "6012|201|11"),
        }, downloaded.Diagnostics.Select(d => (d.Code, string.Join('|', d.Arguments))));
        Assert.All(downloaded.Diagnostics, d => Assert.Equal(AdoDiagnosticSeverity.Warning, d.Severity));
        Assert.Contains("larger than the 2048-byte limit", downloaded.Diagnostics[3].Message, StringComparison.Ordinal);
        Assert.Equal(14, log.ProgressEvents.Count);
        Assert.All(log.ProgressEvents, p => Assert.Equal((AdoProgressPhase.AttachmentDownload, 14), (p.Phase, p.Total!.Value)));
        Assert.Equal(14, log.ProgressEvents[^1].Completed);

        // The input set is never modified.
        Assert.All(failure.Attempts.SelectMany(a => a.Attachments), a => Assert.Equal((AdoTestAttachmentStatus.NotRequested, (string?)null),
            (a.DownloadStatus, a.LocalRelativePath)));
    }

    // Attachments fixture 6: a known size past the remaining budget stops downloads once.
    [Fact]
    public async Task TotalBudgetIsReportedOnceAndMarksEveryRemainingAttachment()
    {
        using TestDirectory directory = new();
        IReadOnlyList<AdoTestAttachment> all = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11);
        AdoTestAttachment[] attachments = [all[0], all[2], all[7], all[8], all[9]];
        AttachmentFixture fixture = new AttachmentFixture().Serve(6001, Png);
        using HttpClient client = new(fixture.Handler);
        AttachmentDownloadResult downloaded = await fixture.Downloader(client, new TestResultOptions { MaximumTotalAttachmentBytes = 250 })
            .DownloadAsync([AttachmentFixture.Failure(attachments)], AttachmentFixture.Project, directory.Root, AttachmentFixture.FolderName,
                Culture, TestContext.Current.CancellationToken);
        Assert.Equal([Downloaded, Budget, Budget, Budget, Budget], downloaded.Failures[0].Attempts[0].Attachments.Select(a => a.DownloadStatus));
        AdoDiagnostic diagnostic = Assert.Single(downloaded.Diagnostics);
        Assert.Equal((DiagnosticCodes.AttachmentBudgetExceeded, "250|6003"), (diagnostic.Code, string.Join('|', diagnostic.Arguments)));
        Assert.Equal([6001], fixture.RequestedIds());
        Assert.Equal(["r201-11-a6001.png"], downloaded.Files.Keys);
    }

    // An unknown size that passes the remaining budget while streaming is a budget stop, not TooLarge.
    [Fact]
    public async Task StreamingPastTheRemainingBudgetStopsAndDeletesThePartialFile()
    {
        using TestDirectory directory = new();
        IReadOnlyList<AdoTestAttachment> all = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11);
        AttachmentFixture fixture = new AttachmentFixture().Serve(6001, Png).Serve(6009, Other);
        using HttpClient client = new(fixture.Handler);
        AttachmentDownloadResult downloaded = await fixture.Downloader(client, new TestResultOptions { MaximumTotalAttachmentBytes = 150 })
            .DownloadAsync([AttachmentFixture.Failure([all[0], all[8], all[7]])], AttachmentFixture.Project, directory.Root,
                AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken);
        Assert.Equal([Downloaded, Budget, Budget], downloaded.Failures[0].Attempts[0].Attachments.Select(a => a.DownloadStatus));
        Assert.Equal([DiagnosticCodes.AttachmentBudgetExceeded], downloaded.Diagnostics.Select(d => d.Code));
        Assert.Equal(["r201-11-a6001.png"], Names(directory.Root));
        Assert.Equal([6001, 6009], fixture.RequestedIds());
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task AuthenticationAndAuthorizationFailuresRemainTerminating(int status)
    {
        using TestDirectory directory = new();
        IReadOnlyList<AdoTestAttachment> all = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11);
        AttachmentFixture fixture = new AttachmentFixture().Serve(6001, Png).Serve(6008, () => AttachmentFixture.Status(status));
        using HttpClient client = new(fixture.Handler);
        AdoException error = await Assert.ThrowsAnyAsync<AdoException>(() => fixture.Downloader(client, Limits)
            .DownloadAsync([AttachmentFixture.Failure([all[0], all[7], all[8]])], AttachmentFixture.Project, directory.Root,
                AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken));
        Assert.IsType(status == 401 ? typeof(AdoAuthenticationException) : typeof(AdoAuthorizationException), error);
        Assert.Equal([6001, 6008], fixture.RequestedIds());
        Assert.Equal(["r201-11-a6001.png"], Names(directory.Root));
    }

    [Fact]
    public async Task CancellationDuringABodyPropagatesAndRemovesThePartialFile()
    {
        using TestDirectory directory = new();
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IReadOnlyList<AdoTestAttachment> all = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11);
        AttachmentFixture fixture = new AttachmentFixture().Serve(6008, () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StreamContent(new ChunkedLogStream(Other, onRead: caller.Cancel)) });
        using HttpClient client = new(fixture.Handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Downloader(client, Limits)
            .DownloadAsync([AttachmentFixture.Failure([all[7], all[8]])], AttachmentFixture.Project, directory.Root,
                AttachmentFixture.FolderName, Culture, caller.Token));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Root));
        Assert.Equal([6008], fixture.RequestedIds());
    }

    [Fact]
    public async Task LocalWriteFailuresAreTerminatingFileOutputErrors()
    {
        using TestDirectory directory = new();
        IReadOnlyList<AdoTestAttachment> all = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11);
        AttachmentFixture fixture = new AttachmentFixture().Serve(6008, Other);
        using HttpClient client = new(fixture.Handler);
        string missing = Path.Combine(directory.Root, "missing");
        AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() => fixture.Downloader(client, Limits)
            .DownloadAsync([AttachmentFixture.Failure([all[7], all[8]])], AttachmentFixture.Project, missing,
                AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken));
        Assert.Contains(missing, error.Message, StringComparison.Ordinal);
        // A local failure is never retried as a transient HTTP failure.
        Assert.Equal([6008], fixture.RequestedIds());
    }

    [Fact]
    public async Task EmptyBodyIsListedWithoutALocalFileAndRepeatedEntriesDownloadOnce()
    {
        using TestDirectory directory = new();
        IReadOnlyList<AdoTestAttachment> all = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11);
        AttachmentFixture fixture = new AttachmentFixture().Serve(6008, Array.Empty<byte>()).Serve(6001, Png);
        using HttpClient client = new(fixture.Handler);
        AttachmentDownloadResult downloaded = await fixture.Downloader(client, Limits).DownloadAsync(
            [AttachmentFixture.Failure([all[7], all[0]], [all[0]])], AttachmentFixture.Project, directory.Root,
            AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken);
        Assert.Equal([AdoTestAttachmentStatus.Failed, Downloaded, Downloaded],
            downloaded.Failures[0].Attempts.SelectMany(a => a.Attachments).Select(a => a.DownloadStatus));
        Assert.Same(downloaded.Failures[0].Attempts[0].Attachments[1], downloaded.Failures[0].Attempts[1].Attachments[0]);
        Assert.Equal([DiagnosticCodes.AttachmentDownloadFailed], downloaded.Diagnostics.Select(d => d.Code));
        Assert.Equal([6008, 6001], fixture.RequestedIds());
        Assert.Equal(["r201-11-a6001.png"], Names(directory.Root));
    }

    // Test results 16: hostile remote names stay display-only.
    [Fact]
    public async Task HostileRemoteNamesNeverReachAPath()
    {
        using TestDirectory directory = new();
        string folder = Directory.CreateDirectory(Path.Combine(directory.Root, "a", "work")).FullName;
        IReadOnlyList<AdoTestAttachment> hostile = AttachmentFixture.Metadata("TestRuns/attachments-hostile.json", 201, 11);
        AttachmentFixture fixture = new();
        foreach (AdoTestAttachment attachment in hostile) fixture.Serve(attachment.Id, Other);
        using HttpClient client = new(fixture.Handler);
        AttachmentDownloadResult downloaded = await fixture.Downloader(client, Limits).DownloadAsync([AttachmentFixture.Failure(hostile)],
            AttachmentFixture.Project, folder, AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken);
        Assert.Equal(7, Directory.GetFiles(folder).Length);
        Assert.All(Directory.GetFiles(folder), path => Assert.Matches(ToolkitName(), Path.GetFileName(path)));
        Assert.Equal([folder], Directory.GetFileSystemEntries(Path.GetDirectoryName(folder)!));
        Assert.Equal([Path.Combine(directory.Root, "a")], Directory.GetFileSystemEntries(directory.Root));
        foreach (AdoTestAttachment attachment in downloaded.Failures[0].Attempts[0].Attachments)
        {
            Assert.Matches("^" + Regex.Escape(AttachmentFixture.FolderName) + "/r201-11-a70[0-9]{2}\\.(png|json|html|bin)$", attachment.LocalRelativePath!);
            Assert.Equal(hostile.Single(h => h.Id == attachment.Id).FileName, attachment.FileName);
        }
        Assert.All(downloaded.Diagnostics, d => Assert.DoesNotContain("script", d.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JsonCheckFollowsTheDocumentedParserRules()
    {
        using TestDirectory directory = new();
        bool Check(byte[] bytes)
        {
            string path = Path.Combine(directory.Root, Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, bytes);
            return AttachmentKinds.IsJson(path);
        }
        Assert.True(Check(AttachmentFixture.Bytes("valid.json")));
        Assert.True(Check([0xEF, 0xBB, 0xBF, .. "{\"a\":1}"u8]));
        Assert.True(Check(Encoding.UTF8.GetBytes(new string('[', 64) + new string(']', 64))));
        Assert.False(Check(DeepJson));
        Assert.False(Check(AttachmentFixture.Bytes("malformed.json.txt")));
        Assert.False(Check([]));
        Assert.False(Check("   "u8.ToArray()));
        Assert.False(Check("{\"a\":1} {\"b\":2}"u8.ToArray()));
        Assert.False(Check("{\"a\":1,}"u8.ToArray()));
        Assert.False(Check("{\"a\":1 /* note */}"u8.ToArray()));
        Assert.False(Check([.. "{\"a\":\""u8, 0xC3, 0x28, .. "\"}"u8]));
        Assert.False(Check([.. "{\""u8, 0xFF, .. "\":1}"u8]));
        // A token longer than the read buffer grows it; memory stays bounded by that token.
        byte[] longToken = Encoding.UTF8.GetBytes("{\"value\":\"" + new string('x', 200_000) + "\"}");
        Assert.True(Check(longToken));
        Assert.False(Check(longToken[..^1]));
        Assert.True(AttachmentKinds.HasPngSignature(Write(directory, Png)));
        Assert.False(AttachmentKinds.HasPngSignature(Write(directory, Png[..7])));
        Assert.False(AttachmentKinds.HasPngSignature(Write(directory, AttachmentFixture.Bytes("png-without-signature.txt"))));
    }

    // Export copies must carry every property; only the replaced one differs.
    [Fact]
    public void ExportCopiesKeepEveryOtherProperty()
    {
        AdoTestAttachment attachment = AttachmentFixture.Metadata("Attachments/attachments-download.json", 201, 11)[0];
        AdoTestFailure failure = TestFailureReportFixture.Set("failed").Failures[0];
        AdoTestAttempt attempt = failure.Attempts[0];
        AssertCopied(attachment, attachment.WithDownload(Downloaded, "x/y.png"), nameof(AdoTestAttachment.DownloadStatus), nameof(AdoTestAttachment.LocalRelativePath));
        AssertCopied(attempt, attempt.WithAttachments([attachment]), nameof(AdoTestAttempt.Attachments));
        AssertCopied(failure, failure.WithAttempts([attempt]), nameof(AdoTestFailure.Attempts));
    }

    private static void AssertCopied<T>(T original, T copy, params string[] replaced)
    {
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (replaced.Contains(property.Name)) continue;
            object? value = property.GetValue(original);
            Assert.True(value is not null && !(value is string text && text.Length == 0) && !(value is System.Collections.ICollection { Count: 0 })
                || property.PropertyType.IsValueType, property.Name + " needs a non-default value in the copy test");
            Assert.Equal(value, property.GetValue(copy));
        }
    }

    private static string[] Names(string folder) => [.. Directory.GetFiles(folder).Select(path => Path.GetFileName(path))];

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private static string Write(TestDirectory directory, byte[] bytes)
    {
        string path = Path.Combine(directory.Root, Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static AdoTestAttachment With(AdoTestAttachment source, int subResultId) => new()
    {
        Id = source.Id, RunId = source.RunId, ResultId = source.ResultId, SubResultId = subResultId, FileName = source.FileName,
        Size = source.Size, AttachmentType = source.AttachmentType, Comment = source.Comment, Kind = source.Kind,
    };

    private const AdoTestAttachmentStatus Downloaded = AdoTestAttachmentStatus.Downloaded;
    private const AdoTestAttachmentStatus Mismatch = AdoTestAttachmentStatus.ContentMismatch;
    private const AdoTestAttachmentStatus Budget = AdoTestAttachmentStatus.BudgetExceeded;

    [GeneratedRegex("^r[1-9][0-9]*-[1-9][0-9]*(-s[1-9][0-9]*)?-a[1-9][0-9]*\\.(png|json|html|bin)$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolkitName();
}
