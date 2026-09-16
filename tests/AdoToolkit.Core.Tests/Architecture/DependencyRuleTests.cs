using System;
using System.Reflection;
using Xunit;

namespace AdoToolkit.Core.Tests.Architecture;

public sealed class DependencyRuleTests
{
    [Fact]
    [Trait("Acceptance", "S0-1")]
    public void CoreDoesNotReferencePowerShell()
    {
        // Check compiled references independently of the project-file declaration.
        Assembly core = Assembly.Load("AdoToolkit.Core");

        Assert.DoesNotContain(core.GetReferencedAssemblies(), reference =>
            string.Equals(reference.Name, "System.Management.Automation", StringComparison.Ordinal));
    }
}
