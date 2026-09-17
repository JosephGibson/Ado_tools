using System.Text.Json;

namespace AdoToolkit.Commands.Infrastructure;

internal static class ErrorRecordFactory
{
    // JSON paths are structural (property names and indexes), never response values; the cap keeps
    // a pathological key from flooding the console.
    private const int MaximumPathLength = 200;

    internal static ErrorRecord Create(AdoException error, CultureInfo culture)
    {
        string code = error.GetType().Name;
        if (code.EndsWith("Exception", StringComparison.Ordinal)) code = code[..^9];
        ErrorCategory category = error switch
        {
            AdoConfigurationException or AdoConnectionMismatchException => ErrorCategory.InvalidArgument,
            AdoRedirectException or AdoRequestException => ErrorCategory.InvalidOperation,
            AdoAuthenticationException => ErrorCategory.AuthenticationError,
            AdoAuthorizationException => ErrorCategory.PermissionDenied,
            AdoNotFoundException => ErrorCategory.ObjectNotFound,
            AdoResponseFormatException => ErrorCategory.InvalidResult,
            AdoTimeoutException => ErrorCategory.OperationTimeout,
            AdoFileOutputException => ErrorCategory.WriteError,
            _ => ErrorCategory.ConnectionError,
        };
        return new ErrorRecord(error, code, category, null) { ErrorDetails = new ErrorDetails(Describe(error, culture)) };
    }

    internal static bool IsTerminating(AdoException error) =>
        error is AdoConfigurationException or AdoAuthenticationException or AdoAuthorizationException;

    // A format error names the operation and, when the serializer reported one, the JSON path, so
    // a server shape difference is actionable without walking the inner exception chain.
    private static string Describe(AdoException error, CultureInfo culture)
    {
        if (error is not AdoResponseFormatException || string.IsNullOrEmpty(error.Operation)) return error.Message;
        string? path = error.InnerException is JsonException json ? Clean(json.Path) : null;
        return path is null
            ? Messages.Get(AdoMessage.ResponseFormatOperation, culture, error.Message, error.Operation)
            : Messages.Get(AdoMessage.ResponseFormatPath, culture, error.Message, error.Operation, path);
    }

    private static string? Clean(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string text = new(path.Where(static character => !char.IsControl(character)).ToArray());
        return text.Length <= MaximumPathLength ? text : text[..MaximumPathLength];
    }
}
