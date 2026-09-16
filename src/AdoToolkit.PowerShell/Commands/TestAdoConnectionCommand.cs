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
            Report(error);
        }
        WriteObject(new AdoConnectionTestResult
        {
            Success = success, Elapsed = Stopwatch.GetElapsedTime(started), ApiVersion = ProjectService.ApiVersion,
            CollectionUri = connection.CollectionUri, Hint = hint,
        });
    });
}
