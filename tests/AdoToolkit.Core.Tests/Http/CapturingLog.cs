namespace AdoToolkit.Core.Tests.Http;

internal sealed class CapturingLog : IAdoLog
{
    internal List<string> Messages { get; } = [];
    internal List<AdoProgress> ProgressEvents { get; } = [];
    public void Verbose(string message) => Messages.Add(message);
    public void Debug(string message) => Messages.Add(message);
    public void Warning(string message) => Messages.Add(message);
    public void Progress(AdoProgress progress) => ProgressEvents.Add(progress);
}
