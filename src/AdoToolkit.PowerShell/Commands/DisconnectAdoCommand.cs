namespace AdoToolkit;

[Cmdlet(VerbsCommunications.Disconnect, "Ado", DefaultParameterSetName = "Default")]
[OutputType(typeof(void))]
public sealed class DisconnectAdoCommand : AdoCmdletBase
{
    protected override void ProcessRecord() => RunLocal(() =>
    {
        SessionStateRegistry.Current.Disconnect();
        WriteVerbose(Messages.Get(AdoMessage.Disconnect, MessageCulture));
    });
}
