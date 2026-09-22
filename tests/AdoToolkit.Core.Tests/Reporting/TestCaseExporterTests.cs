using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-4")]
public sealed class TestCaseExporterTests
{
    [Theory]
    [InlineData("en-US", "fr-CA", "fr-CA", "en-US", 0)]
    [InlineData(null, "fr-CA", "en-US", "fr-CA", 0)]
    [InlineData(null, null, "fr-CA", "fr-CA", 0)]
    [InlineData("de-DE", "fr-CA", "fr-CA", "en", 1)]
    public void ExportResolvesCultureInDocumentedOrder(string? explicitCulture, string? configuredCulture, string session, string expected, int warningCount)
    {
        using TestDirectory directory = new();
        List<string> warnings = [];
        TestCaseExportOptions options = new()
        {
            Culture = explicitCulture, ConfiguredCulture = configuredCulture, SessionCulture = CultureInfo.GetCultureInfo(session),
            Path = directory.Root, GeneratedAt = ReportFixture.Timestamp, ToolkitVersion = "2.1.0-test",
        };
        FileInfo file = new TestCaseExporter(new RecordingLauncher()).Export([ReportFixture.Case("partial")], Connection(), options,
            _ => true, warnings.Add, TestContext.Current.CancellationToken)!;
        Assert.Equal(warningCount, warnings.Count);
        string html = File.ReadAllText(file.FullName);
        Assert.Contains("<html lang=\"" + expected + "\"", html, StringComparison.Ordinal);
        Assert.Contains(expected.StartsWith("fr", StringComparison.Ordinal) ? "Étapes partagées" : "Shared Steps", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.Json)]
    public void LauncherSeesExactlyOneValidatedCommittedFile(ReportFormat format)
    {
        using TestDirectory directory = new();
        RecordingLauncher launcher = new();
        TestCaseExporter exporter = new(launcher, downloads: () => directory.Root);
        string? target = null;
        FileInfo? file = exporter.Export([ReportFixture.Case("nested")], Connection(), Options(null, format),
            path => { target = path; Assert.False(File.Exists(path)); return true; }, _ => Assert.Fail("Unexpected warning"), TestContext.Current.CancellationToken);
        Assert.NotNull(file);
        Assert.Equal(target, file.FullName);
        Assert.Equal(file.FullName, Assert.Single(launcher.Paths));
        TestCaseExporter.Validate(file.FullName, ReportFixture.Model("nested"), format);
        if (format == ReportFormat.Json) ReportFixture.AssertValid(File.ReadAllText(file.FullName));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Theory]
    [Trait("Acceptance", "S3-5")]
    [InlineData(ReportFormat.Html, "all", "TestCases-20260915-103000.html")]
    [InlineData(ReportFormat.Markdown, "first-suite", "TestSuite-51-Steps.md")]
    [InlineData(ReportFormat.Json, "all", "TestCases-20260915-103000.json")]
    public void AnyNumberOfCasesExportAsOneValidatedFileThatIsOpenedOnce(ReportFormat format, string selection, string expectedName)
    {
        using TestDirectory directory = new();
        RecordingLauncher launcher = new();
        IReadOnlyList<AdoToolkit.Core.TestManagement.AdoTestCase> cases = selection == "all" ? MultiCaseFixture.Cases()
            : [MultiCaseFixture.Occurrence(10, "nested", MultiCaseFixture.First), MultiCaseFixture.Occurrence(11, "partial", MultiCaseFixture.First)];
        List<string> confirmed = [];
        FileInfo? file = new TestCaseExporter(launcher, downloads: () => directory.Root).Export(cases, Connection(), Options(null, format),
            path => { confirmed.Add(path); return true; }, _ => Assert.Fail("Unexpected warning"), TestContext.Current.CancellationToken);
        Assert.NotNull(file);
        Assert.Equal(expectedName, file.Name);
        Assert.Equal(file.FullName, Assert.Single(confirmed));
        Assert.Equal(file.FullName, Assert.Single(launcher.Paths));
        Assert.Equal(file.FullName, Assert.Single(Directory.GetFiles(directory.Root, "*", SearchOption.AllDirectories)));
        TestCaseExporter.Validate(file.FullName, MultiCaseFixture.Model("en-US", cases), format);
        string text = File.ReadAllText(file.FullName);
        if (format == ReportFormat.Html) Assert.Contains("data-case-count=\"" + cases.Count.ToString(CultureInfo.InvariantCulture) + "\"", text, StringComparison.Ordinal);
        if (format == ReportFormat.Json) ReportFixture.AssertValid(text);
    }

    [Fact]
    public void WhatIfNeverWritesOrLaunches()
    {
        using TestDirectory directory = new();
        RecordingLauncher launcher = new();
        FileInfo? file = new TestCaseExporter(launcher).Export([ReportFixture.Case()], Connection(), Options(directory.Root),
            _ => false, _ => Assert.Fail("Unexpected warning"), TestContext.Current.CancellationToken);
        Assert.Null(file);
        Assert.Empty(launcher.Paths);
        Assert.Empty(Directory.GetFiles(directory.Root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoClobberIncludingCommitRacePreservesWinnerAndNeverLaunches(bool race)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.html");
        if (!race) File.WriteAllText(path, "winner");
        AtomicFileWriter writer = new((stage, _) => { if (race && stage == AtomicWriteStage.BeforeCommit) File.WriteAllText(path, "winner"); });
        RecordingLauncher launcher = new();
        Assert.Throws<AdoFileOutputException>(() => new TestCaseExporter(launcher, writer).Export([ReportFixture.Case()], Connection(), Options(path),
            _ => true, _ => { }, TestContext.Current.CancellationToken));
        Assert.Equal("winner", File.ReadAllText(path));
        Assert.Empty(launcher.Paths);
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public void ValidationFailureNeverLaunchesAndLeavesExistingFile()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.html");
        File.WriteAllText(path, "original");
        AtomicFileWriter writer = new((stage, temporary) => { if (stage == AtomicWriteStage.Flushed) File.WriteAllText(temporary, "invalid"); });
        RecordingLauncher launcher = new();
        TestCaseExportOptions options = new() { SessionCulture = CultureInfo.GetCultureInfo("en-US"), Path = path, Open = true, GeneratedAt = ReportFixture.Timestamp, ToolkitVersion = "2.1.0-test" };
        Assert.Throws<AdoFileOutputException>(() => new TestCaseExporter(launcher, writer).Export([ReportFixture.Case()], Connection(), options,
            _ => true, _ => { }, TestContext.Current.CancellationToken));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(launcher.Paths);
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public void InvalidInputsFailBeforeShouldProcessOrAnyFile()
    {
        using TestDirectory directory = new();
        RecordingLauncher launcher = new();
        TestCaseExporter exporter = new(launcher);
        Assert.Throws<ArgumentException>(() => exporter.Export([], Connection(), Options(directory.Root),
            _ => throw new InvalidOperationException("Must not confirm"), _ => { }, TestContext.Current.CancellationToken));
        Assert.Throws<AdoConnectionMismatchException>(() => exporter.Export([ReportFixture.Case(), MultiCaseFixture.Occurrence(11, "direct", new AdoToolkit.Core.TestManagement.AdoTestSuiteRef
            {
                PlanId = 1, SuiteId = 2, PlanName = "P", SuiteName = "S", TeamProject = "T", CollectionUri = new Uri("https://other.example.test/Collection"),
            })], Connection(), Options(directory.Root), _ => throw new InvalidOperationException("Must not confirm"), _ => { }, TestContext.Current.CancellationToken));
        AdoConnection foreign = new() { CollectionUri = new Uri("https://other.example.test/Collection") };
        Assert.Throws<AdoConnectionMismatchException>(() => exporter.Export([ReportFixture.Case()], foreign, Options(directory.Root),
            _ => throw new InvalidOperationException("Must not confirm"), _ => { }, TestContext.Current.CancellationToken));
        TestCaseExportOptions invalid = new() { SessionCulture = CultureInfo.GetCultureInfo("en-US"), IncludeSource = true, Path = directory.Root, GeneratedAt = ReportFixture.Timestamp, ToolkitVersion = "2.1.0-test" };
        Assert.Throws<ArgumentException>(() => exporter.Export([ReportFixture.Case()], Connection(), invalid,
            _ => throw new InvalidOperationException("Must not confirm"), _ => { }, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory.Root));
        Assert.Empty(launcher.Paths);
    }

    // -Open hands the file to the shell, which would run a .cmd, .bat, .js, .vbs or .hta file whose
    // text comes from Test Cases. Only document extensions open; any other name is still written,
    // with a warning instead.
    [Theory]
    [InlineData("report.cmd", false)]
    [InlineData("report.hta", false)]
    [InlineData("report.js", false)]
    [InlineData("report", false)]
    [InlineData("report.HTM", true)]
    [InlineData("report.json", true)]
    [InlineData("notes.txt", true)]
    public void OpenLaunchesOnlyDocumentExtensions(string name, bool opened)
    {
        using TestDirectory directory = new();
        RecordingLauncher launcher = new();
        List<string> warnings = [];
        string path = Path.Combine(directory.Root, name);
        FileInfo? file = new TestCaseExporter(launcher).Export([ReportFixture.Case("nested")], Connection(), Options(path, ReportFormat.Json),
            _ => true, warnings.Add, TestContext.Current.CancellationToken);
        Assert.NotNull(file);
        Assert.True(File.Exists(path));
        if (opened)
        {
            Assert.Equal(path, Assert.Single(launcher.Paths));
            Assert.Empty(warnings);
        }
        else
        {
            Assert.Empty(launcher.Paths);
            Assert.Contains(path, Assert.Single(warnings), StringComparison.Ordinal);
        }
        // The shell launcher refuses such a name itself; this path does not exist, so nothing can run.
        Assert.Equal(opened, ShellDocumentLauncher.CanOpen(path));
        if (!opened) Assert.Throws<ArgumentException>(() => new ShellDocumentLauncher().Open(Path.Combine(directory.Root, "absent-" + name)));
    }

    private static AdoConnection Connection() => new() { CollectionUri = ReportFixture.Collection };
    private static TestCaseExportOptions Options(string? path, ReportFormat format = ReportFormat.Html) => new()
    {
        Path = path, Format = format, SessionCulture = CultureInfo.GetCultureInfo("en-US"), Open = true, NoClobber = true,
        GeneratedAt = ReportFixture.Timestamp, ToolkitVersion = "2.1.0-test",
    };

    private sealed class RecordingLauncher : IDocumentLauncher
    {
        internal List<string> Paths { get; } = [];
        public void Open(string path)
        {
            Assert.True(File.Exists(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, ".*.tmp"));
            Paths.Add(path);
        }
    }
}
