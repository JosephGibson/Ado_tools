namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoTimeoutException : AdoException
{
    public AdoTimeoutException() { }
    public AdoTimeoutException(string? message) : base(message) { }
    public AdoTimeoutException(string? message, Exception? innerException) : base(message, innerException) { }
}
