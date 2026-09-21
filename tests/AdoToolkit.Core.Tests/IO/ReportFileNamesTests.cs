using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.IO;

public sealed class ReportFileNamesTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void MachineNamesAreInvariantAndDirectoryPathsAreLiteral(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.Equal("TestCase-1234567-Steps.html", ReportFileNames.TestCase(1234567, ReportFormat.Html));
            Assert.Equal("TestCase-1234567-Steps.md", ReportFileNames.TestCase(1234567, ReportFormat.Markdown));
            Assert.Equal("TestCase-1234567-Steps.json", ReportFileNames.TestCase(1234567, ReportFormat.Json));
            using TestDirectory directory = new();
            string redirected = Path.Combine(directory.Root, "Téléchargements [équipe]");
            Directory.CreateDirectory(redirected);
            string expected = Path.Combine(redirected, "TestCase-1234567-Steps.json");
            Assert.Equal(expected, ReportFileNames.Resolve(null, 1234567, ReportFormat.Json,
                CultureInfo.CurrentCulture, () => redirected));
            Assert.Equal(expected, ReportFileNames.Resolve(redirected, 1234567, ReportFormat.Json,
                CultureInfo.CurrentCulture, () => throw new InvalidOperationException("Must not resolve Downloads")));
            string explicitPath = Path.Combine(redirected, "report [1].json");
            Assert.Equal(explicitPath, ReportFileNames.Resolve(explicitPath, 1234567, ReportFormat.Json, CultureInfo.CurrentCulture));
            Assert.Empty(Directory.GetFiles(redirected));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [Trait("Acceptance", "S3-5")]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void MultiCaseNamesUseOneSuiteIdOrAnInvariantLocalTimestamp(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            DateTimeOffset local = new(2026, 9, 5, 7, 4, 9, TimeSpan.FromHours(-4));
            AdoToolkit.Core.TestManagement.AdoTestCase inFirst = Reporting.MultiCaseFixture.Occurrence(10, "direct", Reporting.MultiCaseFixture.First);
            AdoToolkit.Core.TestManagement.AdoTestCase alsoFirst = Reporting.MultiCaseFixture.Occurrence(11, "direct", Reporting.MultiCaseFixture.First);
            AdoToolkit.Core.TestManagement.AdoTestCase inSecond = Reporting.MultiCaseFixture.Occurrence(10, "direct", Reporting.MultiCaseFixture.Second);
            AdoToolkit.Core.TestManagement.AdoTestCase noSuite = Reporting.MultiCaseFixture.Occurrence(12, "direct", null);
            Assert.Equal("TestSuite-51-Steps.md", ReportFileNames.TestSuite(51, ReportFormat.Markdown));
            Assert.Equal("TestCases-20260905-070409.json", ReportFileNames.TestCases(local, ReportFormat.Json));
            Assert.Equal("TestCase-10-Steps.html", ReportFileNames.ForCases([inSecond], local, ReportFormat.Html));
            Assert.Equal("TestSuite-51-Steps.html", ReportFileNames.ForCases([inFirst, alsoFirst], local, ReportFormat.Html));
            Assert.Equal("TestCases-20260905-070409.html", ReportFileNames.ForCases([inFirst, inSecond], local, ReportFormat.Html));
            Assert.Equal("TestCases-20260905-070409.html", ReportFileNames.ForCases([inFirst, noSuite], local, ReportFormat.Html));
            Assert.Equal("TestCases-20260905-070409.html", ReportFileNames.ForCases([noSuite, noSuite], local, ReportFormat.Html));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.ForCases([], local, ReportFormat.Html));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.TestSuite(0, ReportFormat.Html));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    // §13.2 and §13.4: failed-test report, generation folder and attachment names use IDs and an
    // invariant UTC stamp only.
    [Theory]
    [Trait("Acceptance", "S5-6")]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void FailedTestReportFolderAndAttachmentNamesAreInvariant(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            DateTimeOffset local = new(2026, 12, 31, 21, 5, 9, 7, TimeSpan.FromHours(-5));
            Assert.Equal("Build-1234567-TestFailures.html", ReportFileNames.TestFailures(1234567));
            string stamp = ReportFileNames.GenerationStamp(local);
            Assert.Equal("20270101T020509007Z", stamp);
            Assert.Equal("Build-1234567-TestFailures.files-20270101T020509007Z", ReportFileNames.AttachmentFolder("Build-1234567-TestFailures", stamp));
            Assert.Equal("Rapport été.files-20270101T020509007Z", ReportFileNames.AttachmentFolder("Rapport été", stamp));
            Assert.Equal("r201-1234567-a5001.txt", ReportFileNames.Attachment(201, 1234567, null, 5001, ".txt"));
            Assert.Equal("r201-11-s301-a5101.bin", ReportFileNames.Attachment(201, 11, 301, 5101, ".bin"));
            Assert.Equal("r1-2-a3.json", ReportFileNames.Attachment(1, 2, null, 3, ".json"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [Trait("Acceptance", "S5-7")]
    [InlineData("Build-401-TestFailures.files-20260915T100000000Z", true)]
    [InlineData("build-401-testfailures.files-20260915T100000000Z", true)]
    [InlineData("Build-401-TestFailures.files-20260915t100000000Z", false)]
    [InlineData("Build-401-TestFailures.files-20260915T100000000z", false)]
    [InlineData("Build-401-TestFailures.FILES-20260915T100000000Z", false)]
    [InlineData("Build-401-TestFailures.files-20260915T10000000Z", false)]
    [InlineData("Build-401-TestFailures.files-20260915T1000000000Z", false)]
    [InlineData("Build-401-TestFailures.files-20261315T100000000Z", false)]
    [InlineData("Build-401-TestFailures.files-20260915T250000000Z", false)]
    [InlineData("Build-401-TestFailures.files-２0260915T100000000Z", false)]
    [InlineData("Build-401-TestFailures.files-20260915T100000000Z ", false)]
    [InlineData("Build-4010-TestFailures.files-20260915T100000000Z", false)]
    [InlineData("xBuild-401-TestFailures.files-20260915T100000000Z", false)]
    [InlineData("Build-401-TestFailures.files-", false)]
    [InlineData(".Build-401-TestFailures.files.0123.tmp", false)]
    public void OnlyExactGenerationFolderNamesMatch(string name, bool expected)
    {
        Assert.Equal(expected, ReportFileNames.IsAttachmentFolder(name, "Build-401-TestFailures"));
    }

    [Fact]
    public void InvalidFailedTestNameInputsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.TestFailures(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(0, 1, null, 1, ".json"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(1, 0, null, 1, ".json"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(1, 1, 0, 1, ".json"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(1, 1, null, 0, ".json"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(1, 1, null, 1, ".TXT"));
        // PNG and HTML attachments are never downloaded, so they never get a local name.
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(1, 1, null, 1, ".png"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(1, 1, null, 1, ".html"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.Attachment(1, 1, null, 1, ".exe"));
        Assert.Throws<ArgumentException>(() => ReportFileNames.AttachmentFolder("Build-1-TestFailures", "20260915T100000000"));
        Assert.Throws<ArgumentException>(() => ReportFileNames.AttachmentFolder("", "20260915T100000000Z"));
    }

    [Fact]
    public void MissingParentAndInvalidIdsOrFormatsAreRejected()
    {
        using TestDirectory directory = new();
        Assert.Throws<AdoFileOutputException>(() => ReportFileNames.Resolve(Path.Combine(directory.Root, "missing", "report.json"),
            1, ReportFormat.Json, CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.TestCase(0, ReportFormat.Json));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportFileNames.TestCase(1, (ReportFormat)999));
    }
}
