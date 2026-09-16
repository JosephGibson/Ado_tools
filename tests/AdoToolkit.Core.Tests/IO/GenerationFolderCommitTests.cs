using System.Diagnostics;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.Tests.IO;

[Trait("Acceptance", "S5-7")]
public sealed class GenerationFolderCommitTests
{
    private const string Report = "Build-401-TestFailures.html";
    private const string PreviousFolder = "Build-401-TestFailures.files-20260915T100000000Z";
    private const string NewFile = "r201-11-a51.png";
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTimeOffset Generated = new(2026, 9, 16, 9, 30, 0, 123, TimeSpan.FromHours(-4));
    // Look-alikes that step 8 must never touch.
    private static readonly string[] Unrelated =
    [
        "Build-401-TestFailures.files-notastamp", "Build-4010-TestFailures.files-20260915T100000000Z",
        "Other.files-20260915T100000000Z", "Build-401-TestFailures.files-20261332T250000000Z",
        "Build-401-TestFailures.files-20260915T100000000Z-copy", "Build-401-TestFailures.files-2026091T1000000000Z",
        ".Build-401-TestFailures.files.0123.keep",
    ];

    private static readonly bool[] Booleans = [true, false];

    public static TheoryData<int, bool> Failures => new(
        from step in Enum.GetValues<GenerationCommitStep>()
        where step != GenerationCommitStep.Cleanup
        from attachments in Booleans
        where attachments || step is not (GenerationCommitStep.Download or GenerationCommitStep.Rename)
        select ((int)step, attachments));

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailureAtEachStepLeavesThePreviousGenerationByteIdenticalWithNoTemporaryItems(int step, bool attachments)
    {
        GenerationCommitStep failing = (GenerationCommitStep)step;
        using TestDirectory directory = new();
        string destination = Previous(directory);
        Dictionary<string, string> before = Snapshot(destination);
        bool downloaded = false, rendered = false;
        GenerationFolderCommit commit = new((current, _) => { if (current == failing) throw new IOException("Injected " + current); });
        AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(async () =>
        {
            GenerationFolderPlan plan = commit.Plan(Path.Combine(destination, Report), Generated, noClobber: false, Culture);
            await commit.CommitAsync(plan, false, attachments, (folder, _) => { downloaded = true; return Download(folder); },
                writer => { rendered = true; writer.Write("<html>new " + plan.FolderName + "</html>"); },
                (report, folder) => Validate(report, folder, attachments), _ => Assert.Fail("No step-8 warning expected"),
                Culture, TestContext.Current.CancellationToken);
        });
        Assert.Contains(Path.Combine(destination, Report), error.Message, StringComparison.Ordinal);
        Assert.IsType<IOException>(error.InnerException);
        Assert.Equal(before, Snapshot(destination));
        Assert.Equal(failing >= GenerationCommitStep.Download && attachments, downloaded);
        Assert.Equal(failing >= GenerationCommitStep.Render, rendered);
    }

    // §13.4: a stop right after step 6 leaves the previous report and the folder it links to intact.
    [Fact]
    public async Task StopAfterTheRenameLeavesThePreviousReportConsistent()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        Dictionary<string, string> before = Snapshot(destination);
        bool observed = false;
        GenerationFolderPlan? plan = null;
        GenerationFolderCommit commit = new((step, _) =>
        {
            if (step != GenerationCommitStep.Move) return;
            // The state a crash would leave: new folder under its final name, temporary report beside it.
            Dictionary<string, string> now = Snapshot(destination);
            foreach ((string path, string hash) in before) Assert.Equal(hash, now[path]);
            string previous = File.ReadAllText(Path.Combine(destination, Report));
            Assert.Contains(PreviousFolder + "/r1-1-a1.png", previous, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(destination, PreviousFolder, "r1-1-a1.png")));
            Assert.True(File.Exists(Path.Combine(plan!.FolderPath, NewFile)));
            Assert.Single(Directory.GetFiles(destination, "." + Report + ".*.tmp"));
            observed = true;
            throw new OperationCanceledException("Simulated stop");
        });
        plan = commit.Plan(Path.Combine(destination, Report), Generated, false, Culture);
        await Assert.ThrowsAsync<OperationCanceledException>(() => commit.CommitAsync(plan, false, true,
            (folder, _) => Download(folder), writer => writer.Write("new"), (_, _) => { }, _ => { }, Culture,
            TestContext.Current.CancellationToken));
        Assert.True(observed);
        Assert.Equal(before, Snapshot(destination));
    }

    [Fact]
    public async Task ReExportReplacesTheReportAndRemovesOnlyMatchingEarlierGenerations()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        Directory.CreateDirectory(Path.Combine(destination, "build-401-testfailures.files-20260914T100000000Z", "nested"));
        File.WriteAllText(Path.Combine(destination, "build-401-testfailures.files-20260914T100000000Z", "nested", "old.bin"), "old");
        File.WriteAllText(Path.Combine(destination, "Build-401-TestFailures.files-20260913T100000000Z"), "a file, not a folder");
        List<string> warnings = [];
        GenerationFolderCommit commit = new();
        GenerationFolderPlan plan = commit.Plan(Path.Combine(destination, Report), Generated, false, Culture);
        Assert.Equal("Build-401-TestFailures.files-20260916T133000123Z", plan.FolderName);
        Assert.Equal("20260916T133000123Z", plan.Stamp);
        GenerationCommitResult result = await commit.CommitAsync(plan, false, true, (folder, _) => Download(folder),
            writer => writer.Write("new report"), (report, folder) => Validate(report, folder, true), warnings.Add, Culture,
            TestContext.Current.CancellationToken);
        Assert.Empty(warnings);
        Assert.Equal(Path.Combine(destination, Report), result.Report.FullName);
        Assert.Equal(plan.FolderPath, result.AttachmentDirectory!.FullName);
        Assert.Equal("new report", File.ReadAllText(result.Report.FullName));
        Assert.Equal([NewFile], Directory.GetFiles(plan.FolderPath).Select(path => Path.GetFileName(path)));
        string[] expected = [.. Unrelated, Report, plan.FolderName, "Build-401-TestFailures.files-20260913T100000000Z"];
        Assert.Equal(expected.Order(StringComparer.Ordinal),
            Directory.GetFileSystemEntries(destination).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NoAttachmentsCreatesNoFolderAndStillRemovesEarlierGenerations()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        GenerationFolderCommit commit = new();
        foreach (bool downloadRequested in Booleans)
        {
            GenerationFolderPlan plan = commit.Plan(Path.Combine(destination, Report), Generated.AddSeconds(downloadRequested ? 1 : 0), false, Culture);
            // A download pass that writes nothing leaves no folder either.
            GenerationCommitResult result = await commit.CommitAsync(plan, false, downloadRequested, (_, _) => Task.FromResult(false),
                writer => writer.Write("no attachments"), (_, folder) => Assert.Null(folder), _ => Assert.Fail("No warning expected"), Culture,
                TestContext.Current.CancellationToken);
            Assert.Null(result.AttachmentDirectory);
            Assert.False(Directory.Exists(plan.FolderPath));
            Assert.False(Directory.Exists(Path.Combine(destination, PreviousFolder)));
            Assert.Equal(Unrelated.Append(Report).Order(StringComparer.Ordinal),
                Directory.GetFileSystemEntries(destination).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task JunctionsAreSkippedWithAWarningAndNeverFollowed()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        string outside = Directory.CreateDirectory(Path.Combine(directory.Root, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "sentinel.txt"), "must survive");
        string nestedTarget = Directory.CreateDirectory(Path.Combine(directory.Root, "nested-target")).FullName;
        File.WriteAllText(Path.Combine(nestedTarget, "sentinel.txt"), "must survive too");
        string topLink = Path.Combine(destination, "Build-401-TestFailures.files-20260101T000000000Z");
        string holder = Directory.CreateDirectory(Path.Combine(destination, "Build-401-TestFailures.files-20260102T000000000Z")).FullName;
        File.WriteAllText(Path.Combine(holder, "r1-1-a2.bin"), "old");
        string nestedLink = Path.Combine(holder, "nested");
        CreateJunction(topLink, outside);
        CreateJunction(nestedLink, nestedTarget);
        try
        {
            List<string> warnings = [];
            GenerationFolderCommit commit = new();
            GenerationFolderPlan plan = commit.Plan(Path.Combine(destination, Report), Generated, false, Culture);
            await commit.CommitAsync(plan, false, true, (folder, _) => Download(folder), writer => writer.Write("new"),
                (report, folder) => Validate(report, folder, true), warnings.Add, Culture, TestContext.Current.CancellationToken);
            Assert.Equal(3, warnings.Count);
            Assert.Contains(warnings, w => w.Contains(topLink, StringComparison.Ordinal) && w.Contains("reparse point", StringComparison.Ordinal));
            Assert.Contains(warnings, w => w.Contains(nestedLink, StringComparison.Ordinal) && w.Contains("reparse point", StringComparison.Ordinal));
            Assert.Contains(warnings, w => w.Contains(holder, StringComparison.Ordinal) && w.Contains("delete it manually", StringComparison.Ordinal));
            Assert.True(new DirectoryInfo(topLink).Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(new DirectoryInfo(nestedLink).Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.Equal("must survive", File.ReadAllText(Path.Combine(outside, "sentinel.txt")));
            Assert.Equal("must survive too", File.ReadAllText(Path.Combine(nestedTarget, "sentinel.txt")));
            // The regular file beside the nested link is gone; the ordinary earlier generation is removed.
            Assert.False(File.Exists(Path.Combine(holder, "r1-1-a2.bin")));
            Assert.False(Directory.Exists(Path.Combine(destination, PreviousFolder)));
        }
        finally
        {
            // Remove the links themselves before the test directory is deleted.
            if (Directory.Exists(nestedLink)) Directory.Delete(nestedLink);
            if (Directory.Exists(topLink)) Directory.Delete(topLink);
        }
    }

    [Fact]
    public async Task ExistingFinalFolderFailsTheRenameAndIsLeftAlone()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        GenerationFolderCommit commit = new();
        GenerationFolderPlan plan = commit.Plan(Path.Combine(destination, Report), Generated, false, Culture);
        Directory.CreateDirectory(plan.FolderPath);
        File.WriteAllText(Path.Combine(plan.FolderPath, "keep.txt"), "someone else's folder");
        Dictionary<string, string> before = Snapshot(destination);
        await Assert.ThrowsAsync<AdoFileOutputException>(() => commit.CommitAsync(plan, false, true, (folder, _) => Download(folder),
            writer => writer.Write("new"), (_, _) => { }, _ => { }, Culture, TestContext.Current.CancellationToken));
        Assert.Equal(before, Snapshot(destination));
    }

    [Fact]
    public async Task LockedReportFailsTheMoveAndRemovesTheNewFolder()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        Dictionary<string, string> before = Snapshot(destination);
        GenerationFolderCommit commit = new();
        GenerationFolderPlan plan = commit.Plan(Path.Combine(destination, Report), Generated, false, Culture);
        using (new FileStream(Path.Combine(destination, Report), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<AdoFileOutputException>(() => commit.CommitAsync(plan, false, true, (folder, _) => Download(folder),
                writer => writer.Write("new"), (_, _) => { }, _ => { }, Culture, TestContext.Current.CancellationToken));
        }
        Assert.Equal(before, Snapshot(destination));
    }

    [Fact]
    public async Task NoClobberRefusesBeforeAnyDownloadAndOtherwiseMovesWithoutReplacing()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        Dictionary<string, string> before = Snapshot(destination);
        GenerationFolderCommit commit = new();
        Assert.Throws<AdoFileOutputException>(() => commit.Plan(Path.Combine(destination, Report), Generated, true, Culture));
        Assert.Equal(before, Snapshot(destination));
        string fresh = Path.Combine(destination, "Fresh.html");
        GenerationFolderPlan plan = commit.Plan(fresh, Generated, true, Culture);
        Assert.Equal("Fresh.files-20260916T133000123Z", plan.FolderName);
        GenerationCommitResult result = await commit.CommitAsync(plan, true, true, (folder, _) => Download(folder), writer => writer.Write("fresh"),
            (report, folder) => Validate(report, folder, true), _ => { }, Culture, TestContext.Current.CancellationToken);
        Assert.Equal("fresh", File.ReadAllText(result.Report.FullName));
        // A report created by someone else after planning is never replaced.
        GenerationFolderPlan raced = commit.Plan(Path.Combine(destination, "Raced.html"), Generated, true, Culture);
        await Assert.ThrowsAsync<AdoFileOutputException>(() => commit.CommitAsync(raced, true, true, (folder, _) => Download(folder),
            writer => { writer.Write("mine"); File.WriteAllText(raced.ReportPath, "theirs"); }, (_, _) => { }, _ => { }, Culture,
            TestContext.Current.CancellationToken));
        Assert.Equal("theirs", File.ReadAllText(raced.ReportPath));
        Assert.False(Directory.Exists(raced.FolderPath));
        Assert.Empty(Directory.GetFileSystemEntries(destination, ".*.tmp"));
    }

    [Fact]
    public async Task CleanupFailuresAreWarningsAfterTheCommit()
    {
        using TestDirectory directory = new();
        string destination = Previous(directory);
        foreach (bool injected in Booleans)
        {
            List<string> warnings = [];
            GenerationFolderCommit commit = new((step, path) =>
            {
                if (injected && step == GenerationCommitStep.Cleanup) throw new IOException("Injected " + path);
            });
            GenerationFolderPlan plan = commit.Plan(Path.Combine(destination, Report), Generated.AddSeconds(injected ? 0 : 1), false, Culture);
            string locked = Path.Combine(destination, PreviousFolder, "r1-1-a1.png");
            using (new FileStream(locked, FileMode.Open, FileAccess.Read, injected ? FileShare.ReadWrite | FileShare.Delete : FileShare.None))
            {
                GenerationCommitResult result = await commit.CommitAsync(plan, false, true, (folder, _) => Download(folder),
                    writer => writer.Write("committed"), (report, folder) => Validate(report, folder, true), warnings.Add, Culture,
                    TestContext.Current.CancellationToken);
                Assert.Equal("committed", File.ReadAllText(result.Report.FullName));
            }
            string warning = Assert.Single(warnings, w => w.Contains(PreviousFolder, StringComparison.Ordinal));
            Assert.Contains("delete it manually", warning, StringComparison.Ordinal);
            Assert.True(Directory.Exists(Path.Combine(destination, PreviousFolder)));
            Assert.Empty(Directory.GetFileSystemEntries(destination, ".*.tmp"));
        }
    }

    [Fact]
    public void PlanRejectsReportNamesWithoutABaseName()
    {
        using TestDirectory directory = new();
        Assert.Throws<AdoFileOutputException>(() => new GenerationFolderCommit().Plan(Path.Combine(directory.Root, ".html"), Generated, false, Culture));
    }

    private static string Previous(TestDirectory directory)
    {
        string destination = Directory.CreateDirectory(Path.Combine(directory.Root, "out")).FullName;
        File.WriteAllText(Path.Combine(destination, Report), "<a href=\"" + PreviousFolder + "/r1-1-a1.png\">previous</a>");
        Directory.CreateDirectory(Path.Combine(destination, PreviousFolder));
        File.WriteAllBytes(Path.Combine(destination, PreviousFolder, "r1-1-a1.png"), [0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);
        foreach (string name in Unrelated)
        {
            Directory.CreateDirectory(Path.Combine(destination, name));
            File.WriteAllText(Path.Combine(destination, name, "keep.txt"), name);
        }
        return destination;
    }

    private static Task<bool> Download(string folder)
    {
        File.WriteAllBytes(Path.Combine(folder, NewFile), [0x89, 0x50, 0x4E, 0x47, 9]);
        return Task.FromResult(true);
    }

    private static void Validate(string report, string? folder, bool attachments)
    {
        Assert.True(File.Exists(report));
        if (!attachments) return;
        Assert.NotNull(folder);
        Assert.True(File.Exists(Path.Combine(folder, NewFile)));
    }

    // Relative path → content hash for every file, and "<dir>" for every directory, hidden items included.
    private static Dictionary<string, string> Snapshot(string root) =>
        Directory.EnumerateFileSystemEntries(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
            .ToDictionary(path => Path.GetRelativePath(root, path), path => Directory.Exists(path) ? "<dir>"
                : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    // DD-024 / plan: junctions need no elevation; a failure here fails the test loudly.
    private static void CreateJunction(string link, string target)
    {
        ProcessStartInfo start = new("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0 && Directory.Exists(link) && new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint),
            "mklink /J failed (exit " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + "): " + output);
    }
}
