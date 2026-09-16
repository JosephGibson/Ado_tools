namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoConfigurationException : AdoException
{
    public AdoConfigurationException() { }
    public AdoConfigurationException(string? message) : base(message) { }
    public AdoConfigurationException(string? message, Exception? innerException) : base(message, innerException) { }
}
