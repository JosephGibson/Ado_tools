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

    // Without a supplied or current connection, the configured default profile connects the
    // runspace as Connect-Ado would. No request is sent; a missing default keeps the error.
    protected AdoConnection ResolveConnection(AdoConnection? supplied)
    {
        AdoConnection? connection = supplied ?? SessionStateRegistry.Current.Connection;
        if (connection is not null) return connection;
        AdoConfiguration configuration = new ConfigurationStore().Load(MessageCulture);
        if (configuration.DefaultProfile is not { } name || !configuration.Profiles.TryGetValue(name, out AdoProfile? profile))
            throw new AdoConfigurationException(Messages.Get(AdoMessage.NoConnection, MessageCulture));
        connection = ProfileConnections.Create(profile, null);
        SessionStateRegistry.Current.Connect(connection);
        WriteVerbose(Messages.Get(AdoMessage.AutoConnectProfile, MessageCulture, profile.Name));
        return connection;
    }

    // -Definition accepts a positive ID or a definition name.
    private protected (int? Id, string? Name) ResolveDefinition(object? definition)
    {
        object? value = definition is PSObject wrapped ? wrapped.BaseObject : definition;
        if (value is int number && number > 0) return (number, null);
        if (value is string text && !string.IsNullOrWhiteSpace(text)) return (null, text);
        throw new AdoRequestException(Messages.Get(AdoMessage.InvalidBuildDefinition, MessageCulture));
    }

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
        ErrorRecord record = ErrorRecordFactory.Create(error, MessageCulture);
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
