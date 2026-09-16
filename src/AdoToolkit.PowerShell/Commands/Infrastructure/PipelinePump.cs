using System.Collections.Concurrent;

namespace AdoToolkit.Commands.Infrastructure;

internal static class PipelinePump
{
    internal static T Run<T>(CancellationTokenSource cancellation,
        Func<IAdoLog, CancellationToken, Task<T>> operation, Action<PipelineEvent> dispatch)
    {
        using BlockingCollection<PipelineEvent> events = new(128);
        QueuedLog log = new(events, cancellation.Token);
        Task<T> worker = Task.Run(async () =>
        {
            try { return await operation(log, cancellation.Token).ConfigureAwait(false); }
            finally { events.CompleteAdding(); }
        });
        try
        {
            foreach (PipelineEvent message in events.GetConsumingEnumerable(cancellation.Token)) dispatch(message);
            return worker.GetAwaiter().GetResult();
        }
        finally
        {
            cancellation.Cancel();
            // Join only on the pipeline callback; cancellation releases producers and HTTP.
            // Preserve the initiating pipeline exception during cleanup.
            try { worker.GetAwaiter().GetResult(); }
            catch (Exception) when (cancellation.IsCancellationRequested) { }
        }
    }

    private sealed class QueuedLog(BlockingCollection<PipelineEvent> events, CancellationToken token) : IAdoLog
    {
        private const long ThrottleMilliseconds = 100;
        private readonly Lock gate = new();
        private AdoProgressPhase? phase;
        private int percent = -1;
        private long sent;

        public void Verbose(string message) => events.Add(new(PipelineEventKind.Verbose, message), token);
        public void Debug(string message) => events.Add(new(PipelineEventKind.Debug, message), token);
        public void Warning(string message) => events.Add(new(PipelineEventKind.Warning, message), token);

        // Phase changes and finished phases always pass. Otherwise at most one event per interval,
        // and with a known total only when the whole percentage advances (at most 101 per phase).
        public void Progress(AdoProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);
            int current = progress.Total is int total && total > 0 ? (int)Math.Min(100, progress.Completed * 100L / total) : -1;
            bool finished = progress.Total.HasValue && progress.Completed >= progress.Total.Value;
            long now = Environment.TickCount64;
            lock (gate)
            {
                if (phase == progress.Phase && !finished
                    && (now - sent < ThrottleMilliseconds || (current >= 0 && current <= percent))) return;
                phase = progress.Phase;
                percent = current;
                sent = now;
            }
            events.Add(new(PipelineEventKind.Progress, string.Empty, progress), token);
        }
    }
}
