namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoNotFoundException : AdoException
{
    public AdoNotFoundException() { }
    public AdoNotFoundException(string? message) : base(message) { }
    public AdoNotFoundException(string? message, Exception? innerException) : base(message, innerException) { }
}
