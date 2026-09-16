namespace AdoToolkit.Core.Diagnostics;

internal sealed class NullAdoLog : IAdoLog
{
    public void Verbose(string message) { }
    public void Debug(string message) { }
    public void Warning(string message) { }
    public void Progress(AdoProgress progress) { }
}
