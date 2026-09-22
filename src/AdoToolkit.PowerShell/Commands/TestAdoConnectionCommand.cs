using System.Diagnostics;

namespace AdoToolkit;

[Cmdlet(VerbsDiagnostic.Test, "AdoConnection", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoConnectionTestResult))]
public sealed class TestAdoConnectionCommand : AdoCmdletBase
{
    [Parameter(ValueFromPipeline = true, Position = 0)]
    public AdoConnection? Connection { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = ResolveConnection(Connection);
        using ClientLease lease = SessionStateRegistry.Current.Acquire(connection);
        long started = Stopwatch.GetTimestamp();
        bool success = true;
        string? hint = null;
        try
        {
            _ = RunWorker((log, token) => new ProjectService(lease.Client, connection, log).GetProjectsAsync(MessageCulture, token, top: 1));
        }
        catch (AdoException error) when (!ErrorRecordFactory.IsTerminating(error))
        {
            success = false;
            hint = Messages.Get(AdoMessage.ConnectionHint, MessageCulture);
            if (error is AdoNotFoundException)
            {
                CollectionUrlSuggestion? suggestion = RunWorker((log, token) =>
                    ProjectService.SuggestCollectionAsync(lease.Client, connection, MessageCulture, token, log));
                if (suggestion is not null)
                    hint = Messages.Get(AdoMessage.ConnectionProjectHint, MessageCulture,
                        Quote(suggestion.CollectionUri.AbsoluteUri), Quote(suggestion.Project));
            }
            Report(error);
        }
        WriteObject(new AdoConnectionTestResult
        {
            Success = success, Elapsed = Stopwatch.GetElapsedTime(started), ApiVersion = ProjectService.ApiVersion,
            CollectionUri = connection.CollectionUri, Hint = hint,
        });
    });

    // PowerShell single-quoted literal, so the suggested command can be pasted as shown. The escaper
    // also doubles typographic quotes, which PowerShell reads as single quotes.
    private static string Quote(string value) =>
        "'" + System.Management.Automation.Language.CodeGeneration.EscapeSingleQuotedStringContent(value) + "'";
}
