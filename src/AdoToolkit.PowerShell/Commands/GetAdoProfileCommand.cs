using AdoToolkit.Completion;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoProfile", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoProfile))]
public sealed class GetAdoProfileCommand : AdoCmdletBase
{
    [Parameter(Position = 0)]
    [ArgumentCompleter(typeof(ProfileNameCompleter))]
    public string? Name { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConfiguration configuration = new ConfigurationStore().Load(MessageCulture);
        foreach (string warning in configuration.Warnings) WriteWarning(warning);
        WildcardPattern? pattern = Name is null ? null : new(Name, WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
        foreach (AdoProfile profile in configuration.Profiles.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
            if (pattern is null || pattern.IsMatch(profile.Name)) WriteObject(profile);
    });
}
