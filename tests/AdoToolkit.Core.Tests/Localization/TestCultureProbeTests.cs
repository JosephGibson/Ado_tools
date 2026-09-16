using System;
using System.Globalization;
using Xunit;

namespace AdoToolkit.Core.Tests.Localization;

public sealed class TestCultureProbeTests
{
    [Fact]
    [Trait("Acceptance", "S0-8")]
    public void SuiteUsesRequestedCulture()
    {
        string expected = Environment.GetEnvironmentVariable("ADOTOOLKIT_TEST_CULTURE") ?? "en-US";

        Assert.Equal(expected, CultureInfo.CurrentCulture.Name);
        Assert.Equal(expected, CultureInfo.CurrentUICulture.Name);
    }
}
