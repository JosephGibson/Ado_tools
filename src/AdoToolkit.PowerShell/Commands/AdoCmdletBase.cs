using System.ComponentModel;
using AdoToolkit.Core.Builds;

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
        // -Connection accepts any property bag; one without a usable collection or timeout stops here.
        if (supplied is not null)
        {
            (string Type, string Member)? missing = InputGuard.FindMissing(supplied);
            if (missing is null && !(supplied.CollectionUri.IsAbsoluteUri && supplied.CollectionUri.Scheme is "http" or "https"))
                missing = (nameof(AdoConnection), nameof(AdoConnection.CollectionUri));
            if (missing is null && supplied.RequestTimeoutSeconds is < 1 or > AdoConnection.MaximumRequestTimeoutSeconds)
                missing = (nameof(AdoConnection), nameof(AdoConnection.RequestTimeoutSeconds));
            if (missing is { } invalid)
                throw new AdoConfigurationException(Messages.Get(AdoMessage.IncompleteInput, MessageCulture, invalid.Type, invalid.Member));
        }
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

    // An omitted -Definition falls back to the connected profile's default definition.
    private protected (int? Id, string? Name) ResolveDefinition(object? definition, AdoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        BuildDefinitionSelector selector = definition is null
            ? connection.DefaultBuildDefinition
                ?? throw new AdoConfigurationException(Messages.Get(AdoMessage.BuildDefinitionRequired, MessageCulture))
            : ToDefinitionSelector(definition);
        return (selector.Id, selector.Name);
    }

    // -Definition accepts a positive ID or a definition name. The ID may be any integer type:
    // ConvertFrom-Json, for one, reads every integer as Int64.
    private protected BuildDefinitionSelector ToDefinitionSelector(object definition)
    {
        object? value = definition is PSObject wrapped ? wrapped.BaseObject : definition;
        if (value is sbyte or byte or short or ushort or int or uint or long or ulong
            && Convert.ToDecimal(value, CultureInfo.InvariantCulture) is >= 1 and <= int.MaxValue and decimal number)
            return BuildDefinitionSelector.FromId((int)number);
        if (value is string text && !string.IsNullOrWhiteSpace(text)) return BuildDefinitionSelector.FromName(text);
        throw new AdoRequestException(Messages.Get(AdoMessage.InvalidBuildDefinition, MessageCulture));
    }

    // An omitted -PlanId falls back to the connected profile's plan. The profile's suite belongs to
    // that plan, so it applies only with it; an explicit plan keeps the suite as supplied.
    private protected (int PlanId, int? SuiteId) ResolveTestPlan(int? planId, int? suiteId, AdoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (planId is int explicitPlan) return (explicitPlan, suiteId);
        int plan = connection.DefaultTestPlanId
            ?? throw new AdoConfigurationException(Messages.Get(AdoMessage.TestPlanRequired, MessageCulture));
        return (plan, suiteId ?? connection.DefaultTestSuiteId);
    }

    protected string ResolveProject(string? supplied, AdoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        string? project = string.IsNullOrWhiteSpace(supplied) ? connection.DefaultProject : supplied;
        if (string.IsNullOrWhiteSpace(project))
            throw new AdoConfigurationException(Messages.Get(AdoMessage.ProjectRequired, MessageCulture));
        // A URL path collapses these names, so the request would leave the collection (Core refuses them too).
        if (project is "." or "..")
            throw new AdoConfigurationException(Messages.Get(AdoMessage.InvalidProjectName, MessageCulture, project));
        return project;
    }

    protected void EnsureSameCollection(Uri input, AdoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(connection);
        if (!input.Equals(connection.CollectionUri))
            throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, MessageCulture));
    }

    // A typed pipeline input must be a complete toolkit object. A hand-made one is reported for
    // that input, like an object from another collection, instead of failing on a null member.
    private protected void EnsureComplete(object input)
    {
        if (InputGuard.FindMissing(input) is { } missing)
            throw new AdoRequestException(Messages.Get(AdoMessage.IncompleteInput, MessageCulture, missing.Type, missing.Member));
    }

    private protected void EnsureInput(object input, Uri? collection, AdoConnection connection)
    {
        EnsureComplete(input);
        if (collection is null)
            throw new AdoRequestException(Messages.Get(AdoMessage.IncompleteInput, MessageCulture, input.GetType().Name, nameof(AdoConnection.CollectionUri)));
        EnsureSameCollection(collection, connection);
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
