namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoThrottledException : AdoException
{
    public AdoThrottledException() { }
    public AdoThrottledException(string? message) : base(message) { }
    public AdoThrottledException(string? message, Exception? innerException) : base(message, innerException) { }
}
