namespace AdoToolkit.Core.Tests.Http;

// A log may be called from several threads at once; the lists are read after the calls have ended.
internal sealed class CapturingLog : IAdoLog
{
    private readonly Lock gate = new();
    internal List<string> Messages { get; } = [];
    internal List<AdoProgress> ProgressEvents { get; } = [];
    public void Verbose(string message) => Add(message);
    public void Debug(string message) => Add(message);
    public void Warning(string message) => Add(message);
    public void Progress(AdoProgress progress)
    {
        lock (gate) ProgressEvents.Add(progress);
    }

    private void Add(string message)
    {
        lock (gate) Messages.Add(message);
    }
}
