namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoRequestException : AdoException
{
    public AdoRequestException() { }
    public AdoRequestException(string? message) : base(message) { }
    public AdoRequestException(string? message, Exception? innerException) : base(message, innerException) { }
}
