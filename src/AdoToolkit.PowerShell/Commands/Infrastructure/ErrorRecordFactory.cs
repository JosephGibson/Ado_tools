namespace AdoToolkit.Commands.Infrastructure;

internal static class ErrorRecordFactory
{
    internal static ErrorRecord Create(AdoException error)
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
        return new ErrorRecord(error, code, category, null) { ErrorDetails = new ErrorDetails(error.Message) };
    }

    internal static bool IsTerminating(AdoException error) =>
        error is AdoConfigurationException or AdoAuthenticationException or AdoAuthorizationException;
}
