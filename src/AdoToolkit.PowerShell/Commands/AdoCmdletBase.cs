using System.ComponentModel;

namespace AdoToolkit;

// Public because exported cmdlets cannot inherit a less-accessible base in C#.
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AdoCmdletBase : PSCmdlet
{
    private CancellationTokenSource? active;
    private bool stopped;
    protected CultureInfo MessageCulture { get; private set; } = CultureInfo.InvariantCulture;

    protected override void BeginProcessing() => MessageCulture = CultureCapture.Capture(SessionState);

    protected override void StopProcessing()
    {
        Volatile.Write(ref stopped, true);
        try { Volatile.Read(ref active)?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    protected void RunLocal(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try { action(); }
        catch (PipelineStoppedException) { throw; }
        catch (OperationCanceledException) { throw new PipelineStoppedException(); }
        catch (AdoException error) { Report(error); }
    }

    protected T RunWorker<T>(Func<IAdoLog, CancellationToken, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Volatile.Read(ref stopped)) throw new PipelineStoppedException();
        using CancellationTokenSource cancellation = new();
        Volatile.Write(ref active, cancellation);
        // StopProcessing may have raced with publishing the invocation source.
        if (Volatile.Read(ref stopped)) cancellation.Cancel();
        try { return PipelinePump.Run(cancellation, operation, Dispatch); }
        catch (OperationCanceledException) { throw new PipelineStoppedException(); }
        finally { Volatile.Write(ref active, null); }
    }

    protected AdoConnection ResolveConnection(AdoConnection? supplied) =>
        supplied ?? SessionStateRegistry.Current.Connection
        ?? throw new AdoConfigurationException(Messages.Get(AdoMessage.NoConnection, MessageCulture));

    protected string ResolveProject(string? supplied, AdoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        string? project = string.IsNullOrWhiteSpace(supplied) ? connection.DefaultProject : supplied;
        return string.IsNullOrWhiteSpace(project)
            ? throw new AdoConfigurationException(Messages.Get(AdoMessage.ProjectRequired, MessageCulture))
            : project;
    }

    protected void EnsureSameCollection(Uri input, AdoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(connection);
        if (!input.Equals(connection.CollectionUri))
            throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, MessageCulture));
    }

    protected void Report(AdoException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        ErrorRecord record = ErrorRecordFactory.Create(error);
        if (ErrorRecordFactory.IsTerminating(error)) ThrowTerminatingError(record);
        else WriteError(record);
    }

    // Called on the pipeline thread; commands that report progress localize and write it.
    private protected virtual void OnProgress(AdoProgress progress) { }

    private void Dispatch(PipelineEvent message)
    {
        switch (message.Kind)
        {
            case PipelineEventKind.Verbose: WriteVerbose(message.Message); break;
            case PipelineEventKind.Debug: WriteDebug(message.Message); break;
            case PipelineEventKind.Warning: WriteWarning(message.Message); break;
            case PipelineEventKind.Progress: OnProgress(message.Progress!); break;
        }
    }
}
