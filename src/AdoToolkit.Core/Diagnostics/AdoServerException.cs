namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoServerException : AdoException
{
    public AdoServerException() { }
    public AdoServerException(string? message) : base(message) { }
    public AdoServerException(string? message, Exception? innerException) : base(message, innerException) { }
}
