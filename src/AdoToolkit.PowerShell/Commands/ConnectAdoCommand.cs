using AdoToolkit.Completion;
using AdoToolkit.Resources;

namespace AdoToolkit;

[Cmdlet(VerbsCommunications.Connect, "Ado", DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoConnection))]
public sealed class ConnectAdoCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, ParameterSetName = "ByUrl")]
    [ValidateNotNullOrEmpty]
    public string CollectionUrl { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = "ByProfile")]
    [ArgumentCompleter(typeof(ProfileNameCompleter))]
    public string? Profile { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    public string? Project { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection;
        if (ParameterSetName == "ByUrl")
        {
            CollectionUrlNormalizationResult normalized = CollectionUrlNormalizer.Normalize(CollectionUrl, MessageCulture);
            foreach (string warning in normalized.Warnings) WriteWarning(warning);
            connection = new AdoConnection { CollectionUri = normalized.CollectionUri, DefaultProject = Project };
        }
        else
        {
            AdoConfiguration configuration = new ConfigurationStore().Load(MessageCulture);
            foreach (string warning in configuration.Warnings) WriteWarning(warning);
            string name = Profile ?? configuration.DefaultProfile
                ?? throw new AdoConfigurationException(Messages.Get(AdoMessage.NoTarget, MessageCulture));
            if (!configuration.Profiles.TryGetValue(name, out AdoProfile? profile))
                throw new AdoConfigurationException(Messages.Get(AdoMessage.MissingProfile, MessageCulture, name));
            connection = new AdoConnection
            {
                CollectionUri = profile.CollectionUri, DefaultProject = Project ?? profile.DefaultProject,
                Authentication = profile.Authentication, RequestTimeoutSeconds = profile.RequestTimeoutSeconds,
            };
        }
        SessionStateRegistry.Current.Connect(connection);
        WriteVerbose(ShellMessages.Get(AdoMessage.Connect, MessageCulture));
        WriteObject(connection);
    });
}
