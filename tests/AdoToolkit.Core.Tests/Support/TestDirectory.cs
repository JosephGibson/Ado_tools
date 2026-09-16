namespace AdoToolkit.Core.Tests.Support;

internal sealed class TestDirectory : IDisposable
{
    private readonly string? previousConfigPath = Environment.GetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH");
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "AdoToolkitTests-" + Guid.NewGuid().ToString("N"));
    public string ConfigPath => Path.Combine(Root, "config.json");

    public TestDirectory()
    {
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH", ConfigPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ADOTOOLKIT_CONFIG_PATH", previousConfigPath);
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
