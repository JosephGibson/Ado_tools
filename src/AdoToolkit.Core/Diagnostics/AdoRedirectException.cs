namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoRedirectException : AdoException
{
    public AdoRedirectException() { }
    public AdoRedirectException(string? message) : base(message) { }
    public AdoRedirectException(string? message, Exception? innerException) : base(message, innerException) { }
}
