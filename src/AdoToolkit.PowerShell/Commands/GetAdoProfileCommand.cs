using AdoToolkit.Completion;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Get, "AdoProfile", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoProfile))]
public sealed class GetAdoProfileCommand : AdoCmdletBase
{
    [Parameter(Position = 0)]
    [SupportsWildcards]
    [ArgumentCompleter(typeof(ProfileNameCompleter))]
    public string? Name { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConfiguration configuration = new ConfigurationStore().Load(MessageCulture);
        foreach (string warning in configuration.Warnings) WriteWarning(warning);
        NameFilter filter = new(Name);
        foreach (AdoProfile profile in configuration.Profiles.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
            if (filter.IsMatch(profile.Name)) WriteObject(profile);
    });
}
