using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.IO;

[Trait("Acceptance", "S2-3")]
public sealed class AtomicFileWriterTests
{
    private static readonly bool[] ExistingOptions = [false, true];
    public static TheoryData<int, bool> Failures => new(
        from stage in Enum.GetValues<AtomicWriteStage>()
        from existing in ExistingOptions
        select ((int)stage, existing));

    [Theory]
    [MemberData(nameof(Failures))]
    public void FailureAtEveryStagePreservesDestinationAndCleansTemporary(int stage, bool existing)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.json");
        byte[] original = [0, 1, 127, 128, 255];
        if (existing) File.WriteAllBytes(path, original);
        AtomicFileWriter writer = new((current, _) =>
        {
            if ((int)current == stage) throw new IOException("Injected failure");
        });
        AdoFileOutputException error = Assert.Throws<AdoFileOutputException>(() => writer.Write(path,
            output => output.Write("{\"schemaVersion\":1}"),
            temporary => ReportOutputValidator.ValidateJson(temporary, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        if (existing) Assert.Equal(original, File.ReadAllBytes(path));
        else Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public void ValidationFailureLeavesExistingBytesUntouched()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.json");
        byte[] original = Encoding.UTF8.GetBytes("existing report");
        File.WriteAllBytes(path, original);
        Assert.Throws<AdoFileOutputException>(() => new AtomicFileWriter().Write(path,
            output => output.Write("{\"schemaVersion\":1"),
            temporary => ReportOutputValidator.ValidateJson(temporary, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public void RendererExceptionAndCancellationCleanUpWithoutCommit()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.json");
        File.WriteAllText(path, "original");
        Assert.Throws<InvalidOperationException>(() => new AtomicFileWriter().Write(path, output =>
        {
            output.Write("partial");
            throw new InvalidOperationException("Renderer failed");
        }, _ => Assert.Fail("Must not validate"), CultureInfo.InvariantCulture,
            cancellationToken: TestContext.Current.CancellationToken));
        using CancellationTokenSource cancellation = new();
        Assert.Throws<OperationCanceledException>(() => new AtomicFileWriter().Write(path,
            output => output.Write("complete"), _ => cancellation.Cancel(), CultureInfo.InvariantCulture,
            cancellationToken: cancellation.Token));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoClobberRefusesExistingAndRacingFiles(bool race)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.json");
        if (!race) File.WriteAllText(path, "competitor");
        bool rendered = false;
        AtomicFileWriter writer = new((stage, _) =>
        {
            if (stage == AtomicWriteStage.BeforeCommit) File.WriteAllText(path, "competitor");
        });
        Assert.Throws<AdoFileOutputException>(() => writer.Write(path, output =>
        {
            rendered = true;
            output.Write("new report");
        }, _ => { }, CultureInfo.InvariantCulture, noClobber: true,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(race, rendered);
        Assert.Equal("competitor", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public void LockedDestinationNamesPathAndPreservesBytes()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.json");
        File.WriteAllText(path, "original");
        using (FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            AdoFileOutputException error = Assert.Throws<AdoFileOutputException>(() => new AtomicFileWriter().Write(path,
                output => output.Write("replacement"), _ => { }, CultureInfo.GetCultureInfo("fr-CA"),
                cancellationToken: TestContext.Current.CancellationToken));
            Assert.Contains(path, error.Message, StringComparison.Ordinal);
            // Windows may report a locked overwrite as access denied rather than a sharing violation.
            Assert.True(error.InnerException is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommitUsesExclusiveSiblingUtf8WithoutBomAndValidatesBeforeReplacing(bool existing)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report [1].txt");
        if (existing) File.WriteAllText(path, "original");
        bool validated = false;
        AtomicFileWriter writer = new((stage, temporary) =>
        {
            Assert.Equal(directory.Root, Path.GetDirectoryName(temporary));
            Assert.StartsWith(".report [1].txt.", Path.GetFileName(temporary), StringComparison.Ordinal);
            if (stage == AtomicWriteStage.TemporaryCreated)
                Assert.Throws<IOException>(() => { using FileStream competing = File.OpenRead(temporary); });
        });
        FileInfo result = writer.Write(path, output => output.WriteLine("« Été : » 😀"), temporary =>
        {
            Assert.Equal(Encoding.UTF8.GetBytes("« Été : » 😀\n"), File.ReadAllBytes(temporary));
            if (existing) Assert.Equal("original", File.ReadAllText(path));
            else Assert.False(File.Exists(path));
            validated = true;
        }, CultureInfo.InvariantCulture, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(validated);
        Assert.Equal(path, result.FullName);
        Assert.Equal(Encoding.UTF8.GetBytes("« Été : » 😀\n"), File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public void MissingParentIsNotCreated()
    {
        using TestDirectory directory = new();
        string parent = Path.Combine(directory.Root, "missing");
        Assert.Throws<AdoFileOutputException>(() => new AtomicFileWriter().Write(Path.Combine(parent, "report.txt"),
            _ => Assert.Fail("Must not render"), _ => Assert.Fail("Must not validate"), CultureInfo.InvariantCulture,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(parent));
    }
}
