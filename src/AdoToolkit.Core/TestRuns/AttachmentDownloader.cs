using System.Net.Http;
using AdoToolkit.Core.Configuration;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.TestRuns;

// §15.13: downloads in report order into the temporary generation folder, with per-file and
// total byte limits, content checks, and toolkit-generated names only. Only JSON and text
// attachments are ever requested, whatever the caller selects; PNG, HTML and other kinds stay
// links. The input failures are never modified; the result carries updated copies.
public sealed class AttachmentDownloader
{
    private const string PartialExtension = ".part";
    private readonly AdoHttpPipeline pipeline;
    private readonly TestResultOptions limits;
    private readonly IAdoLog log;

    public AttachmentDownloader(HttpClient client, AdoConnection connection, TestResultOptions limits, IAdoLog? log = null)
        : this(new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log),
            limits, log) { }

    internal AttachmentDownloader(AdoHttpPipeline pipeline, TestResultOptions limits, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaximumAttachmentBytes < 1 || limits.MaximumTotalAttachmentBytes < 1 || limits.MaximumInlineJsonBytes < 1
            || limits.MaximumInlineTotalBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(limits));
        this.pipeline = pipeline;
        this.limits = limits;
        this.log = log ?? new NullAdoLog();
    }

    internal long MaximumInlineJsonBytes => limits.MaximumInlineJsonBytes;
    internal long MaximumInlineTotalBytes => limits.MaximumInlineTotalBytes;

    internal async Task<AttachmentDownloadResult> DownloadAsync(IReadOnlyList<AdoTestFailure> failures, string project,
        string folder, string folderName, CultureInfo culture, CancellationToken cancellationToken, IReadOnlySet<int>? runIds = null)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        ArgumentNullException.ThrowIfNull(culture);
        Session session = new(this, project, folder, folderName, culture,
            failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments)
                .Where(a => Selected(a, runIds)).Select(Key).Distinct().Count());
        List<AdoTestFailure> updated = new(failures.Count);
        foreach (AdoTestFailure failure in failures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<AdoTestAttempt> attempts = new(failure.Attempts.Count);
            foreach (AdoTestAttempt attempt in failure.Attempts)
            {
                List<AdoTestAttachment> attachments = new(attempt.Attachments.Count);
                foreach (AdoTestAttachment attachment in attempt.Attachments)
                    attachments.Add(Selected(attachment, runIds)
                        ? await session.GetAsync(attachment, cancellationToken).ConfigureAwait(false)
                        : attachment);
                attempts.Add(attempt.WithAttachments(attachments.AsReadOnly()));
            }
            updated.Add(failure.WithAttempts(attempts.AsReadOnly()));
        }
        return new AttachmentDownloadResult(updated.AsReadOnly(), session.Diagnostics.AsReadOnly(), session.Files);
    }

    // Null selects every run; the kind rule applies either way.
    internal static bool Selected(AdoTestAttachment attachment, IReadOnlySet<int>? runIds) =>
        AttachmentKinds.IsDownloadable(attachment.Kind) && (runIds is null || runIds.Contains(attachment.RunId));

    private static (int, int, int?, int) Key(AdoTestAttachment attachment) =>
        (attachment.RunId, attachment.ResultId, attachment.SubResultId, attachment.Id);

    // Authentication, authorization, local file output and cancellation stay terminating (§8.3).
    private static bool IsRecoverable(Exception error, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && error is AdoException
            and not AdoAuthenticationException and not AdoAuthorizationException and not AdoFileOutputException;

    private sealed class Session(AttachmentDownloader owner, string project, string folder, string folderName,
        CultureInfo culture, int count)
    {
        private readonly Dictionary<(int, int, int?, int), AdoTestAttachment> done = [];
        private long total;
        private bool budgetReached;
        internal List<AdoDiagnostic> Diagnostics { get; } = [];
        internal Dictionary<string, long> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        // An attachment listed under several attempts is downloaded once.
        internal async Task<AdoTestAttachment> GetAsync(AdoTestAttachment attachment, CancellationToken cancellationToken)
        {
            if (done.TryGetValue(Key(attachment), out AdoTestAttachment? known)) return known;
            cancellationToken.ThrowIfCancellationRequested();
            AdoTestAttachment result = await DownloadAsync(attachment, cancellationToken).ConfigureAwait(false);
            done.Add(Key(attachment), result);
            owner.log.Progress(new AdoProgress { Phase = AdoProgressPhase.AttachmentDownload, Completed = done.Count, Total = count });
            return result;
        }

        private async Task<AdoTestAttachment> DownloadAsync(AdoTestAttachment attachment, CancellationToken cancellationToken)
        {
            TestResultOptions limits = owner.limits;
            long remaining = limits.MaximumTotalAttachmentBytes - total;
            if (budgetReached) return attachment.WithDownload(AdoTestAttachmentStatus.BudgetExceeded, null);
            if (attachment.Size > limits.MaximumAttachmentBytes) return TooLarge(attachment);
            if (attachment.Size > remaining || remaining < 1) return BudgetExceeded(attachment);
            string partial = Path.Combine(folder, Name(attachment, ".bin") + PartialExtension);
            long limit = Math.Min(limits.MaximumAttachmentBytes, remaining);
            Dictionary<string, string> routes = new(StringComparer.Ordinal)
            {
                ["project"] = project, ["runId"] = N(attachment.RunId), ["resultId"] = N(attachment.ResultId), ["attachmentId"] = N(attachment.Id),
            };
            Dictionary<string, string>? query = attachment.SubResultId is { } sub
                ? new(StringComparer.Ordinal) { ["testSubResultId"] = N(sub) } : null;
            long length;
            try
            {
                length = await owner.pipeline.DownloadStreamAsync(EndpointRegistry.TestResultAttachmentContent, routes, query, culture,
                    (response, body, token) => CopyAsync(response, body, partial, limit, token), cancellationToken).ConfigureAwait(false);
            }
            catch (AttachmentLimitException)
            {
                Delete(partial);
                return limits.MaximumAttachmentBytes <= remaining ? TooLarge(attachment) : BudgetExceeded(attachment);
            }
            catch (Exception error) when (IsRecoverable(error, cancellationToken))
            {
                Delete(partial);
                return Failed(attachment);
            }
            catch
            {
                DeleteQuietly(partial);
                throw;
            }
            // §13.4 links only non-empty files, so an empty body is listed without a local file.
            if (length == 0)
            {
                Delete(partial);
                return Failed(attachment);
            }
            bool matches = Local(() => AttachmentKinds.HasExpectedContent(attachment.Kind, partial), partial);
            string name = Name(attachment, matches ? AttachmentKinds.LocalExtension(attachment.Kind) : ".bin");
            Local(() => File.Move(partial, Path.Combine(folder, name)), partial);
            total += length;
            Files.Add(name, length);
            if (!matches)
            {
                Add(DiagnosticCodes.AttachmentContentMismatch, attachment,
                    attachment.Kind == AdoTestAttachmentKind.Text ? "TXT" : "JSON");
                return attachment.WithDownload(AdoTestAttachmentStatus.ContentMismatch, folderName + "/" + name);
            }
            return attachment.WithDownload(AdoTestAttachmentStatus.Downloaded, folderName + "/" + name);
        }

        // Runs once per HTTP attempt; FileMode.Create restarts the file for a retry (§6.5).
        private async Task<long> CopyAsync(HttpResponseMessage response, Stream body, string path, long limit, CancellationToken token)
        {
            if (response.Content.Headers.ContentLength > limit) throw new AttachmentLimitException();
            FileStream output = Local(() => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan), path);
            await using (output.ConfigureAwait(false))
            {
                byte[] buffer = new byte[81920];
                long written = 0;
                while (true)
                {
                    int read = await body.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (read == 0) break;
                    written += read;
                    if (written > limit) throw new AttachmentLimitException();
                    try { await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw FileOutput(path, error); }
                }
                try
                {
                    await output.FlushAsync(token).ConfigureAwait(false);
                    output.Flush(flushToDisk: true);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw FileOutput(path, error); }
                return written;
            }
        }

        private AdoTestAttachment TooLarge(AdoTestAttachment attachment)
        {
            Add(DiagnosticCodes.AttachmentTooLarge, attachment, owner.limits.MaximumAttachmentBytes.ToString(CultureInfo.InvariantCulture));
            return attachment.WithDownload(AdoTestAttachmentStatus.TooLarge, null);
        }

        private AdoTestAttachment BudgetExceeded(AdoTestAttachment attachment)
        {
            budgetReached = true;
            Diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.AttachmentBudgetExceeded, culture,
                arguments: [owner.limits.MaximumTotalAttachmentBytes.ToString(CultureInfo.InvariantCulture), N(attachment.Id)]));
            return attachment.WithDownload(AdoTestAttachmentStatus.BudgetExceeded, null);
        }

        private AdoTestAttachment Failed(AdoTestAttachment attachment)
        {
            Add(DiagnosticCodes.AttachmentDownloadFailed, attachment);
            return attachment.WithDownload(AdoTestAttachmentStatus.Failed, null);
        }

        private void Add(string code, AdoTestAttachment attachment, params string[] extra) =>
            Diagnostics.Add(DiagnosticMessageRenderer.Create(code, culture,
                arguments: [N(attachment.Id), N(attachment.RunId), N(attachment.ResultId), .. extra]));

        private void Delete(string path) => Local(() => File.Delete(path), path);

        private static void DeleteQuietly(string path)
        {
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }

        private T Local<T>(Func<T> action, string path)
        {
            try { return action(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw FileOutput(path, error); }
        }

        private void Local(Action action, string path) => Local(() => { action(); return true; }, path);

        private AdoFileOutputException FileOutput(string path, Exception error) =>
            new(Messages.Get(AdoMessage.FileOutput, culture, path), error);

        private static string Name(AdoTestAttachment attachment, string extension) =>
            ReportFileNames.Attachment(attachment.RunId, attachment.ResultId, attachment.SubResultId, attachment.Id, extension);

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
