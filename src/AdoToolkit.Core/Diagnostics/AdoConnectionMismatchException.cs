namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoConnectionMismatchException : AdoException
{
    public AdoConnectionMismatchException() { }
    public AdoConnectionMismatchException(string? message) : base(message) { }
    public AdoConnectionMismatchException(string? message, Exception? innerException) : base(message, innerException) { }
}
