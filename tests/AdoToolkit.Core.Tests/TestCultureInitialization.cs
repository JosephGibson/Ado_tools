using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AdoToolkit.Core.Tests;

internal static class TestCultureInitialization
{
#pragma warning disable CA2255 // Test assembly initialization deliberately sets the suite's requested culture.
    [ModuleInitializer]
    internal static void Initialize()
#pragma warning restore CA2255
    {
        string name = Environment.GetEnvironmentVariable("ADOTOOLKIT_TEST_CULTURE") ?? "en-US";
        CultureInfo culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        // Every Core test process starts with an isolated config path, never the user's profile.
        Environment.SetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH",
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AdoToolkitTests-" + Guid.NewGuid().ToString("N"), "config.json"));
    }
}
