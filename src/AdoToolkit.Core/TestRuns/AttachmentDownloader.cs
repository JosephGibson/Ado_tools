using System.Net.Http;
using AdoToolkit.Core.Configuration;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.TestRuns;

// §15.13: downloads into the temporary generation folder, with per-file and total byte limits,
// content checks, and toolkit-generated names only. Only JSON and text attachments are ever
// requested, whatever the caller selects; PNG, HTML and other kinds stay links. The input
// failures are never modified; the result carries updated copies.
//
// §15.13 fixed the order as report order. That still holds without a selection. With a selection
// that names the latest run, that run's files come first and the other runs' files after them,
// each part in report order, so the total budget is never spent on older runs first.
//
// Files are decided in that order and fetched together: up to MaximumConcurrentRequests bodies
// are read at once, and each file's outcome is settled only when every file before it is settled,
// by the rules that applied when files were read one at a time. Statuses, diagnostics and files
// are therefore the same whatever order the bodies arrive in, and with truthfully declared sizes
// so are the requests. With a bound of one, files are read one after another.
public sealed class AttachmentDownloader
{
    private const string PartialExtension = ".part";
    private readonly AdoHttpPipeline pipeline;
    private readonly TestResultOptions limits;
    private readonly IAdoLog log;
    private readonly RequestCounter? requests;

    public AttachmentDownloader(HttpClient client, AdoConnection connection, TestResultOptions limits, IAdoLog? log = null)
        : this(client, connection, limits, log, new RequestCounter()) { }

    private AttachmentDownloader(HttpClient client, AdoConnection connection, TestResultOptions limits, IAdoLog? log, RequestCounter requests)
        : this(new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log,
            counter: requests), limits, log, requests) { }

    // requests is the counter the pipeline counts with, if any, so that RequestCount can report it.
    internal AttachmentDownloader(AdoHttpPipeline pipeline, TestResultOptions limits, IAdoLog? log = null, RequestCounter? requests = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaximumAttachmentBytes < 1 || limits.MaximumTotalAttachmentBytes < 1 || limits.MaximumInlineJsonBytes < 1
            || limits.MaximumInlineTotalBytes < 1 || limits.MaximumConcurrentRequests < 1
            || limits.MaximumConcurrentRequests > TestResultOptions.MaximumConcurrentRequestsLimit)
            throw new ArgumentOutOfRangeException(nameof(limits));
        this.pipeline = pipeline;
        this.limits = limits;
        this.log = log ?? new NullAdoLog();
        this.requests = requests;
    }

    // The requests sent so far, retries included.
    internal int RequestCount => requests?.Count ?? 0;

    internal long MaximumInlineJsonBytes => limits.MaximumInlineJsonBytes;
    internal long MaximumInlineTotalBytes => limits.MaximumInlineTotalBytes;

    internal async Task<AttachmentDownloadResult> DownloadAsync(IReadOnlyList<AdoTestFailure> failures, string project,
        string folder, string folderName, CultureInfo culture, CancellationToken cancellationToken, AttachmentSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        ArgumentNullException.ThrowIfNull(culture);
        // Each selected attachment once, in report order: one listed under several attempts is downloaded once.
        List<AdoTestAttachment> order = [];
        HashSet<(int, int, int?, int)> seen = [];
        foreach (AdoTestAttachment attachment in failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments))
            if (Selected(attachment, selection) && seen.Add(Key(attachment))) order.Add(attachment);
        if (selection?.LatestRunId is int latest)
            order = [.. order.Where(a => a.RunId == latest), .. order.Where(a => a.RunId != latest)];
        Session session = new(this, project, folder, folderName, culture, selection);
        await session.RunAsync(order, cancellationToken).ConfigureAwait(false);
        List<AdoTestFailure> updated = new(failures.Count);
        foreach (AdoTestFailure failure in failures)
        {
            List<AdoTestAttempt> attempts = new(failure.Attempts.Count);
            foreach (AdoTestAttempt attempt in failure.Attempts)
                attempts.Add(attempt.WithAttachments(Array.AsReadOnly(attempt.Attachments.Select(session.Result).ToArray())));
            updated.Add(failure.WithAttempts(attempts.AsReadOnly()));
        }
        return new AttachmentDownloadResult(updated.AsReadOnly(), session.Diagnostics.AsReadOnly(), session.Files);
    }

    // Without a selection every run and every size is selected; the kind rule applies either way.
    internal static bool Selected(AdoTestAttachment attachment, AttachmentSelection? selection) =>
        selection is null ? AttachmentKinds.IsDownloadable(attachment.Kind) : selection.Selects(attachment);

    private static (int, int, int?, int) Key(AdoTestAttachment attachment) =>
        (attachment.RunId, attachment.ResultId, attachment.SubResultId, attachment.Id);

    // Authentication, authorization, local file output and cancellation stay terminating (§8.3).
    private static bool IsRecoverable(Exception error, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && error is AdoException
            and not AdoAuthenticationException and not AdoAuthorizationException and not AdoFileOutputException;

    private enum FetchKind { Read, LimitReached, Failed }

    // What reading one body gave: its length, or that it passed its cap, or that it failed in a way
    // that only loses this file. A terminating error is the fetch task's exception instead.
    private readonly record struct Fetch(FetchKind Kind, long Length);

    // A file that has been started and not yet settled. Body is null when no request was needed to
    // know that the file will not be downloaded.
    private sealed class Pending(string partial, long weight, Task<Fetch>? body, CancellationTokenSource? stop)
    {
        internal string Partial { get; } = partial;
        // What the file may add to the total while it is unsettled: its declared size, or its cap.
        internal long Weight { get; } = weight;
        internal Task<Fetch>? Body { get; } = body;
        internal CancellationTokenSource? Stop { get; } = stop;
    }

    private sealed class Session(AttachmentDownloader owner, string project, string folder, string folderName,
        CultureInfo culture, AttachmentSelection? selection)
    {
        private readonly Dictionary<(int, int, int?, int), AdoTestAttachment> done = [];
        private long total;
        private bool budgetReached;
        internal List<AdoDiagnostic> Diagnostics { get; } = [];
        internal Dictionary<string, long> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        // The outcome of an attachment that was selected; any other attachment as it was.
        internal AdoTestAttachment Result(AdoTestAttachment attachment) => done.GetValueOrDefault(Key(attachment), attachment);

        // Only this method changes the total, the diagnostics and the recorded files. It starts files
        // in order while the bound allows and settles them strictly in order, waiting for the first
        // unsettled one.
        internal async Task RunAsync(List<AdoTestAttachment> order, CancellationToken cancellationToken)
        {
            TestResultOptions limits = owner.limits;
            Pending?[] pending = new Pending?[order.Count];
            using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int next = 0, settled = 0, reading = 0;
            long reserved = 0;
            try
            {
                while (settled < order.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    while (next < order.Count)
                    {
                        AdoTestAttachment attachment = order[next];
                        Pending started;
                        if (next == settled)
                        {
                            // Nothing before this file is unsettled, so everything about it can be decided now,
                            // exactly as when files were read one at a time.
                            if (Refused(attachment) is { } refused)
                            {
                                Settle(attachment, refused, order.Count);
                                settled = ++next;
                                continue;
                            }
                            long cap = Cap(attachment, limits.MaximumTotalAttachmentBytes - total);
                            started = Start(attachment, cap, attachment.Size ?? cap, stop.Token);
                        }
                        else
                        {
                            if (reading >= limits.MaximumConcurrentRequests) break;
                            // A file known not to be downloaded needs no request; which status it gets is decided in its turn.
                            if (attachment.Size > limits.MaximumAttachmentBytes || budgetReached)
                                started = new Pending(PartialPath(attachment), 0, null, null);
                            else
                            {
                                // Read ahead only what certainly fits once every file being read has been
                                // counted at its declared size, so a truthful server gets the same requests.
                                long cap = Cap(attachment, limits.MaximumAttachmentBytes);
                                long weight = attachment.Size ?? cap;
                                long free = limits.MaximumTotalAttachmentBytes - total - reserved;
                                if (free < 1 || weight > free) break;
                                started = Start(attachment, cap, weight, stop.Token);
                            }
                        }
                        pending[next++] = started;
                        if (started.Body is not null)
                        {
                            reading++;
                            reserved += started.Weight;
                        }
                    }
                    if (settled == order.Count) break;
                    Pending head = pending[settled]!;
                    AdoTestAttachment result = await SettleAsync(order[settled], head).ConfigureAwait(false);
                    pending[settled] = null;
                    if (head.Body is not null)
                    {
                        reading--;
                        reserved -= head.Weight;
                    }
                    head.Stop?.Dispose();
                    Settle(order[settled], result, order.Count);
                    settled++;
                }
            }
            catch
            {
                // A terminating error stops every body still being read and leaves no partial file.
                await stop.CancelAsync().ConfigureAwait(false);
                foreach (Pending? item in pending)
                {
                    if (item?.Body is null) continue;
                    await ((Task)item.Body).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    DeleteQuietly(item.Partial);
                    item.Stop?.Dispose();
                }
                throw;
            }
        }

        private void Settle(AdoTestAttachment attachment, AdoTestAttachment result, int count)
        {
            done.Add(Key(attachment), result);
            owner.log.Progress(new AdoProgress { Phase = AdoProgressPhase.AttachmentDownload, Completed = done.Count, Total = count });
        }

        // The outcome of a file that is not downloaded because of what is already known: the total was
        // reached earlier, or its declared size is over a limit. Null when it has to be read.
        private AdoTestAttachment? Refused(AdoTestAttachment attachment)
        {
            long remaining = owner.limits.MaximumTotalAttachmentBytes - total;
            if (budgetReached) return attachment.WithDownload(AdoTestAttachmentStatus.BudgetExceeded, null);
            if (attachment.Size > owner.limits.MaximumAttachmentBytes) return TooLarge(attachment);
            if (attachment.Size > remaining || remaining < 1) return BudgetExceeded(attachment);
            return null;
        }

        // How much of the body is read: the per-file limit or what the total leaves, and for a file
        // selected for its size alone no more than that size.
        private long Cap(AdoTestAttachment attachment, long remaining)
        {
            long cap = Math.Min(owner.limits.MaximumAttachmentBytes, remaining);
            return selection is not null && selection.IsSmallOnly(attachment) ? Math.Min(cap, selection.SmallLimit) : cap;
        }

        private string PartialPath(AdoTestAttachment attachment) => Path.Combine(folder, Name(attachment, ".bin") + PartialExtension);

        private Pending Start(AdoTestAttachment attachment, long cap, long weight, CancellationToken stopToken)
        {
            string partial = PartialPath(attachment);
            CancellationTokenSource own = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
            return new Pending(partial, weight, ReadAsync(attachment, partial, cap, own.Token), own);
        }

        private async Task<Fetch> ReadAsync(AdoTestAttachment attachment, string partial, long cap, CancellationToken cancellationToken)
        {
            Dictionary<string, string> routes = new(StringComparer.Ordinal)
            {
                ["project"] = project, ["runId"] = N(attachment.RunId), ["resultId"] = N(attachment.ResultId), ["attachmentId"] = N(attachment.Id),
            };
            Dictionary<string, string>? query = attachment.SubResultId is { } sub
                ? new(StringComparer.Ordinal) { ["testSubResultId"] = N(sub) } : null;
            try
            {
                long length = await owner.pipeline.DownloadStreamAsync(EndpointRegistry.TestResultAttachmentContent, routes, query, culture,
                    (response, body, token) => CopyAsync(response, body, partial, cap, token), cancellationToken).ConfigureAwait(false);
                return new Fetch(FetchKind.Read, length);
            }
            catch (AttachmentLimitException)
            {
                Delete(partial);
                return new Fetch(FetchKind.LimitReached, 0);
            }
            catch (Exception error) when (IsRecoverable(error, cancellationToken))
            {
                Delete(partial);
                return new Fetch(FetchKind.Failed, 0);
            }
            catch
            {
                DeleteQuietly(partial);
                throw;
            }
        }

        // Settles the first unsettled file. Every file before it is settled, so the total and the
        // budget state are what they were when files were read one at a time, and the same rules give
        // the same outcome. A body that was read ahead and turns out not to be wanted is dropped.
        private async Task<AdoTestAttachment> SettleAsync(AdoTestAttachment attachment, Pending started)
        {
            TestResultOptions limits = owner.limits;
            if (Refused(attachment) is { } refused)
            {
                if (started.Body is not null)
                {
                    await started.Stop!.CancelAsync().ConfigureAwait(false);
                    await ((Task)started.Body).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    DeleteQuietly(started.Partial);
                }
                return refused;
            }
            long remaining = limits.MaximumTotalAttachmentBytes - total;
            long limit = Math.Min(limits.MaximumAttachmentBytes, remaining);
            // A file selected for its size alone is read up to that size and no further. When it turns
            // out larger, or had no declared size and is larger, it stays a link without a warning,
            // exactly like a large file of an older run that was never selected.
            bool smallCap = selection is not null && selection.IsSmallOnly(attachment) && selection.SmallLimit < limit;
            if (smallCap) limit = selection!.SmallLimit;
            Fetch fetch = await started.Body!.ConfigureAwait(false);
            string partial = started.Partial;
            // The cap used for a body read ahead can be above what the total leaves now; a body that
            // passed either one is treated as the cap being reached.
            if (fetch.Kind == FetchKind.LimitReached || (fetch.Kind == FetchKind.Read && fetch.Length > limit))
            {
                Delete(partial);
                if (smallCap) return attachment;
                return limits.MaximumAttachmentBytes <= remaining ? TooLarge(attachment) : BudgetExceeded(attachment);
            }
            if (fetch.Kind == FetchKind.Failed) return Failed(attachment);
            // §13.4 links only non-empty files, so an empty body is listed without a local file.
            if (fetch.Length == 0)
            {
                Delete(partial);
                return Failed(attachment);
            }
            bool matches = Local(() => AttachmentKinds.HasExpectedContent(attachment.Kind, partial), partial);
            string name = Name(attachment, matches ? AttachmentKinds.LocalExtension(attachment.Kind) : ".bin");
            Local(() => File.Move(partial, Path.Combine(folder, name)), partial);
            total += fetch.Length;
            Files.Add(name, fetch.Length);
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
