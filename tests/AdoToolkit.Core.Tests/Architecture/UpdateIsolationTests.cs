using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Architecture;

// Update-AdoToolkit calls GitHub, so it stays apart from the Azure DevOps side in both directions:
// the updater and its cmdlet use none of the pipeline, credentials, sessions, profiles or
// configuration, and nothing else uses the updater. A source scan, because the cmdlet lives in an
// assembly that these tests do not reference.
[Trait("Culture", "Invariant")]
public sealed partial class UpdateIsolationTests
{
    private const string UpdateFolder = "Update/";
    private const string Cmdlet = "Commands/UpdateAdoToolkitCommand.cs";
    private const string ManifestReader = "Commands/Infrastructure/ModuleManifestReader.cs";

    private static readonly string[] AzureDevOpsSide =
    [
        "AdoHttpPipeline", "EndpointRegistry", "AdoHttpHandlerFactory", "IAdoCredentialProvider", "WindowsIntegratedCredentialProvider",
        "ConfigurationStore", "AdoConfiguration", "AdoProfile",
    ];

    private static readonly string[] Session = ["SessionStateRegistry", "SessionStateHolder", "ClientLease", "ProfileConnections", "ResolveConnection"];

    private static readonly string[] UpdaterTypes =
    [
        "ToolkitUpdater", "ToolkitUpdatePlan", "ToolkitInstallContext", "ToolkitInstallation", "ModuleManifestFacts", "AdoToolkitUpdate",
        "AdoToolkitUpdateStatus", "AdoToolkitInstallMode", "UpdateHttp", "UpdateGuards", "GitHubClient", "ReleaseReader", "ChecksumFile",
        "ModuleArchive", "PortableArchive", "UpdateFolderCommit", "UpdateCommitStep",
    ];

    [Fact]
    public void TheUpdaterUsesOnlyDiagnosticsAndResourcesOfCore()
    {
        List<TestSources.Source> sources = [.. Core().Where(static source => source.Path.StartsWith(UpdateFolder, StringComparison.Ordinal))];
        Assert.NotEmpty(sources);
        foreach (TestSources.Source source in sources)
        {
            string code = Code(source.Text);
            foreach (Match match in Namespace().Matches(code))
                Assert.True(match.Groups[1].Value is "Diagnostics" or "Resources" or "Update", source.Path + " uses AdoToolkit.Core." + match.Groups[1].Value);
            foreach (string name in AzureDevOpsSide) Assert.DoesNotMatch(Word(name), code);
        }
    }

    [Fact]
    public void TheCmdletUsesNoConnectionSessionOrConfiguration()
    {
        foreach (string path in new[] { Cmdlet, ManifestReader })
        {
            string code = Code(Assert.Single(PowerShell(), source => source.Path == path).Text);
            foreach (string name in AzureDevOpsSide.Concat(Session)) Assert.DoesNotMatch(Word(name), code);
        }
    }

    [Fact]
    public void NothingElseUsesTheUpdater()
    {
        IEnumerable<TestSources.Source> others = Core().Where(static source => !source.Path.StartsWith(UpdateFolder, StringComparison.Ordinal))
            .Concat(PowerShell().Where(static source => source.Path is not (Cmdlet or ManifestReader)));
        foreach (TestSources.Source source in others)
        {
            string code = Code(source.Text);
            Assert.DoesNotContain("AdoToolkit.Core.Update", code, StringComparison.Ordinal);
            foreach (string name in UpdaterTypes) Assert.DoesNotMatch(Word(name), code);
        }
    }

    // Only the Pester tests set the seam, through reflection.
    [Fact]
    public void NoSourceAssignsTheTestSeam()
    {
        foreach (TestSources.Source source in Core().Concat(PowerShell()))
            Assert.DoesNotMatch(@"\bTestTransport\s*(?:=(?!=)|\?\?=)", Code(source.Text));
    }

    [GeneratedRegex(@"\bAdoToolkit\.Core\.(\w+)")]
    private static partial Regex Namespace();

    // Comments, strings and character literals, which name files and explain decisions.
    [GeneratedRegex(@"//[^\n]*|/\*[\s\S]*?\*/|@""(?:[^""]|"""")*""|""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])+'")]
    private static partial Regex CommentOrLiteral();

    private static string Code(string text) => CommentOrLiteral().Replace(text, " ");

    private static string Word(string name) => @"\b" + name + @"\b";

    private static IEnumerable<TestSources.Source> Core() => TestSources.Sources(Path.Combine(TestDirectory.RepositoryRoot, "src", "AdoToolkit.Core"));

    private static IEnumerable<TestSources.Source> PowerShell() => TestSources.Sources(Path.Combine(TestDirectory.RepositoryRoot, "src", "AdoToolkit.PowerShell"));
}
