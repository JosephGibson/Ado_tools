namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoAuthenticationException : AdoException
{
    public AdoAuthenticationException() { }
    public AdoAuthenticationException(string? message) : base(message) { }
    public AdoAuthenticationException(string? message, Exception? innerException) : base(message, innerException) { }
}
