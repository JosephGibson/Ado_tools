namespace AdoToolkit.Core.Diagnostics;

public class AdoException : Exception
{
    public AdoException() : this(Messages.Get(AdoMessage.OperationFailed, CultureInfo.InvariantCulture)) { }
    public AdoException(string? message) : base(message) { }
    public AdoException(string? message, Exception? innerException) : base(message, innerException) { }
    public string? Operation { get; init; }
    public int? StatusCode { get; init; }
    public IReadOnlyList<string> ResourceIdentifiers { get; init; } = [];
    public string? Project { get; init; }
    public string? CorrelationId { get; init; }
    public bool IsRetryable { get; init; }
}
