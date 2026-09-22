using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;
using AdoToolkit.Resources;

namespace AdoToolkit;

[Cmdlet(VerbsCommon.Set, "AdoProfile", SupportsShouldProcess = true, DefaultParameterSetName = "Default")]
[OutputType(typeof(AdoProfile))]
public sealed class SetAdoProfileCommand : AdoCmdletBase
{
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrWhiteSpace]
    [ArgumentCompleter(typeof(ProfileNameCompleter))]
    public string Name { get; set; } = "";

    [Parameter]
    public string? CollectionUrl { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    public string? DefaultProject { get; set; }

    // The four defaults below are removed from the profile with $null; the text ones also with a blank value.
    [Parameter]
    public string? DefaultBranch { get; set; }

    [Parameter]
    public object? DefaultBuildDefinition { get; set; }

    [Parameter]
    [ValidateOptionalId(AdoMessage.ProfileIdRange)]
    public int? DefaultTestPlanId { get; set; }

    [Parameter]
    [ValidateOptionalId(AdoMessage.ProfileIdRange)]
    public int? DefaultTestSuiteId { get; set; }

    [Parameter]
    [ValidateSet("WindowsIntegrated")]
    public string Authentication { get; set; } = "WindowsIntegrated";

    [Parameter]
    [ValidateRange(1, AdoConnection.MaximumRequestTimeoutSeconds)]
    public int RequestTimeoutSeconds { get; set; } = 100;

    [Parameter]
    public SwitchParameter DefaultProfile { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        ConfigurationStore store = new();
        AdoConfiguration configuration = store.Load(MessageCulture);
        foreach (string warning in configuration.Warnings) WriteWarning(warning);
        configuration.Profiles.TryGetValue(Name, out AdoProfile? previous);
        CollectionUrlNormalizationResult normalized = CollectionUrlNormalizer.Normalize(
            CollectionUrl ?? previous?.CollectionUrl ?? "", MessageCulture);
        foreach (string warning in normalized.Warnings) WriteWarning(warning);
        AdoProfile profile = new()
        {
            Name = Name, CollectionUri = normalized.CollectionUri,
            DefaultProject = MyInvocation.BoundParameters.ContainsKey(nameof(DefaultProject)) ? DefaultProject : previous?.DefaultProject,
            DefaultBranch = MyInvocation.BoundParameters.ContainsKey(nameof(DefaultBranch))
                ? (string.IsNullOrWhiteSpace(DefaultBranch) ? null : DefaultBranch) : previous?.DefaultBranch,
            DefaultBuildDefinition = MyInvocation.BoundParameters.ContainsKey(nameof(DefaultBuildDefinition))
                ? SuppliedDefinition() : previous?.DefaultBuildDefinition,
            DefaultTestPlanId = MyInvocation.BoundParameters.ContainsKey(nameof(DefaultTestPlanId)) ? DefaultTestPlanId : previous?.DefaultTestPlanId,
            DefaultTestSuiteId = MyInvocation.BoundParameters.ContainsKey(nameof(DefaultTestSuiteId)) ? DefaultTestSuiteId : previous?.DefaultTestSuiteId,
            Authentication = MyInvocation.BoundParameters.ContainsKey(nameof(Authentication)) ? Authentication : previous?.Authentication ?? Authentication,
            RequestTimeoutSeconds = MyInvocation.BoundParameters.ContainsKey(nameof(RequestTimeoutSeconds)) ? RequestTimeoutSeconds : previous?.RequestTimeoutSeconds ?? RequestTimeoutSeconds,
        };
        if (ShouldProcess(Name, ShellMessages.Get(AdoMessage.SetProfile, MessageCulture)))
            WriteObject(store.SetProfile(profile, DefaultProfile.IsPresent, MessageCulture));
    });

    private BuildDefinitionSelector? SuppliedDefinition() =>
        (DefaultBuildDefinition is PSObject wrapped ? wrapped.BaseObject : DefaultBuildDefinition) switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            _ => ToDefinitionSelector(DefaultBuildDefinition!),
        };
}
