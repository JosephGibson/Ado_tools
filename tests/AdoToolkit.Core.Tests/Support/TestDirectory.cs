namespace AdoToolkit.Core.Tests.Support;

internal sealed class TestDirectory : IDisposable
{
    private readonly bool pointsConfigurationPath;
    private readonly string? previousConfigPath;
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "AdoToolkitTests-" + Guid.NewGuid().ToString("N"));
    public string ConfigPath => Path.Combine(Root, "config.json");

    // A plain directory changes no process-wide state, so tests that use it run in parallel.
    public TestDirectory() : this(pointConfigurationPath: false) { }

    private TestDirectory(bool pointConfigurationPath)
    {
        Directory.CreateDirectory(Root);
        if (!pointConfigurationPath) return;
        previousConfigPath = Environment.GetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH");
        Environment.SetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH", ConfigPath);
        pointsConfigurationPath = true;
    }

    // Points ADOTOOLKIT_CONFIG_PATH, which the product reads, at ConfigPath until disposed. The
    // variable belongs to the process: only a class in the ProcessEnvironment collection may call this.
    public static TestDirectory WithConfigurationPath() => new(pointConfigurationPath: true);

    public void Dispose()
    {
        if (pointsConfigurationPath) Environment.SetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH", previousConfigPath);
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

    public static string RepositoryRoot
    {
        get
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "AdoToolkit.slnx")))
                current = current.Parent;
            return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
}
