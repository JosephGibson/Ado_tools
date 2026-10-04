using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Architecture;

// xUnit runs test classes in parallel. A class that changes state the whole process shares runs in
// the ProcessEnvironment collection, alone. CultureInfo.CurrentCulture is not such state: a test that
// sets it changes only its own execution context.
public sealed partial class ProcessStateTests
{
    private static readonly string Root = Path.Combine(TestDirectory.RepositoryRoot, "tests", "AdoToolkit.Core.Tests");

    // The module initializer runs before any test; TestDirectory hands out the configuration path
    // through WithConfigurationPath, which the pattern names; this file names the calls.
    private static readonly string[] Exempt = ["TestCultureInitialization.cs", "Support/TestDirectory.cs", "Architecture/ProcessStateTests.cs"];

    [Fact]
    public void ClassesThatChangeProcessStateRunInTheProcessEnvironmentCollection()
    {
        TestSources.Source[] changing = [.. TestSources.Sources(Root)
            .Where(static source => !Exempt.Contains(source.Path, StringComparer.Ordinal))
            .Where(static source => ProcessChange().IsMatch(source.Text))];
        string[] outside = [.. changing.Where(static source => !InCollection().IsMatch(source.Text)).Select(static source => source.Path)];

        Assert.Contains(changing, static source => source.Path == "Configuration/ConfigurationStoreTests.cs");
        Assert.Empty(outside);
    }

    [GeneratedRegex(@"SetEnvironmentVariable|WithConfigurationPath\(|DefaultThreadCurrent(UI)?Culture\s*=|SetCurrentDirectory|Environment\.CurrentDirectory\s*=|Console\.Set(In|Out|Error)\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProcessChange();

    // The attribute on the class itself: before its declaration, among its other attributes.
    [GeneratedRegex(@"^\[Collection\(ProcessEnvironment\.Name\)\]\s*(?:^\[[^\]\r\n]*\]\s*)*^public (?:sealed )?(?:partial )?class ",
        RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex InCollection();
}
