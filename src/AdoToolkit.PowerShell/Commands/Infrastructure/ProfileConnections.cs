namespace AdoToolkit.Commands.Infrastructure;

// Connect-Ado and implicit default-profile connections build the connection the same way.
internal static class ProfileConnections
{
    internal static AdoConnection Create(AdoProfile profile, string? project) => new()
    {
        CollectionUri = profile.CollectionUri, DefaultProject = project ?? profile.DefaultProject,
        DefaultBranch = profile.DefaultBranch, DefaultBuildDefinition = profile.DefaultBuildDefinition,
        DefaultTestPlanId = profile.DefaultTestPlanId, DefaultTestSuiteId = profile.DefaultTestSuiteId,
        Authentication = profile.Authentication, RequestTimeoutSeconds = profile.RequestTimeoutSeconds,
    };
}
