namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoResponseFormatException : AdoException
{
    public AdoResponseFormatException() { }
    public AdoResponseFormatException(string? message) : base(message) { }
    public AdoResponseFormatException(string? message, Exception? innerException) : base(message, innerException) { }
}
