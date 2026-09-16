namespace AdoToolkit.Core.Diagnostics;

public interface IAdoLog
{
    void Verbose(string message);
    void Debug(string message);
    void Warning(string message);
    void Progress(AdoProgress progress);
}
