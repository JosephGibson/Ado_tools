namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoFileOutputException : AdoException
{
    public AdoFileOutputException() { }
    public AdoFileOutputException(string? message) : base(message) { }
    public AdoFileOutputException(string? message, Exception? innerException) : base(message, innerException) { }
}
