namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoConnection", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoConnection))]
public sealed class GetAdoConnectionCommand : AdoCmdletBase
{
    protected override void ProcessRecord() => RunLocal(() =>
    {
        if (SessionStateRegistry.Current.Connection is AdoConnection connection) WriteObject(connection);
    });
}
