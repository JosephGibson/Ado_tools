namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoAuthorizationException : AdoException
{
    public AdoAuthorizationException() { }
    public AdoAuthorizationException(string? message) : base(message) { }
    public AdoAuthorizationException(string? message, Exception? innerException) : base(message, innerException) { }
}
