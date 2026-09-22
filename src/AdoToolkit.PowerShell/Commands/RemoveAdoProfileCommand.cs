using AdoToolkit.Completion;
using AdoToolkit.Resources;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Remove, "AdoProfile", SupportsShouldProcess = true, DefaultParameterSetName = "ByName")]
[OutputType(typeof(void))]
public sealed class RemoveAdoProfileCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByName")]
    [ValidateNotNullOrWhiteSpace]
    [ArgumentCompleter(typeof(ProfileNameCompleter))]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "ByProfile")]
    public AdoProfile? InputObject { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        string name = InputObject?.Name ?? Name;
        if (ShouldProcess(name, ShellMessages.Get(AdoMessage.RemoveProfile, MessageCulture)))
            new ConfigurationStore().RemoveProfile(name, MessageCulture);
    });
}
