using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

public sealed class ReportOutputValidatorTests
{
    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    public void ValidationMemoryIsBoundedForLargeLinesAndContentCannotSpoofMarkers(ReportFormat format)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "large.txt");
        AdoTestCase testCase = new()
        {
            Id = 10, Rev = 3, Title = "Large synthetic report", WorkItemType = "Test Case", State = "Ready",
            CollectionUri = ReportFixture.Collection, TeamProject = ReportFixture.Project, WebUrl = ReportFixture.Untrusted,
            Steps = [new() { Number = "1", Sequence = 1, Action = new string('é', 1_500_000) + "\n# forged\n### forged\n<div class=\"step-card\">" }],
        };
        ReportDocumentModel model = ReportModelBuilder.Build(testCase, new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options());
        new AtomicFileWriter().Write(path, writer => TestCaseExporter.Render(model, writer, format),
            temporary => TestCaseExporter.Validate(temporary, model, format), model.Culture,
            cancellationToken: TestContext.Current.CancellationToken);
        long before = GC.GetAllocatedBytesForCurrentThread();
        TestCaseExporter.Validate(path, model, format);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 256_000, "Validation should retain bounded line prefixes, not document content.");
    }

    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    public void TruncationCountCorruptionAndMissingMarkersAreRejected(ReportFormat format)
    {
        using TestDirectory directory = new();
        ReportDocumentModel model = ReportFixture.Model("nested");
        string original = GoldenReportTests.Render(model, format);
        string path = Path.Combine(directory.Root, "report.txt");
        File.WriteAllText(path, original);
        TestCaseExporter.Validate(path, model, format);
        string[] corruptions = format == ReportFormat.Html
            ? [original.Replace("data-case-count=\"1\"", "data-case-count=\"2\"", StringComparison.Ordinal),
               original.Replace("data-step-count=\"4\"", "data-step-count=\"3\"", StringComparison.Ordinal),
               original.Replace("class=\"step-card\"", "class=\"missing-card\"", StringComparison.Ordinal),
               original.Replace("name=\"generator\"", "name=\"missing\"", StringComparison.Ordinal), original[..(original.Length / 2)]]
            : [original.Replace("# Azure", "Azure", StringComparison.Ordinal),
               original.Replace("### 2\\.1.1", "2\\.1.1", StringComparison.Ordinal), original + "# Unexpected\n", original[..(original.Length / 2)]];
        foreach (string corrupted in corruptions)
        {
            File.WriteAllText(path, corrupted);
            Assert.Throws<InvalidDataException>(() => TestCaseExporter.Validate(path, model, format));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":1")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":\"1\"}")]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"schemaVersion\":1.5}")]
    [InlineData("{\"schemaVersion\":99999999999999999999}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1} trailing")]
    public void InvalidJsonIsRejected(string contents)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.json");
        File.WriteAllText(path, contents);
        Assert.Throws<InvalidDataException>(() => ReportOutputValidator.ValidateJson(path, CultureInfo.GetCultureInfo("fr-CA")));
    }

    [Fact]
    public void MissingFileIsRejectedAndCommitCheckDoesNotPretendToValidateSchema()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "report.json");
        Assert.Throws<InvalidDataException>(() => ReportOutputValidator.ValidateNonEmpty(path, CultureInfo.InvariantCulture));
        File.WriteAllText(path, "{\"schemaVersion\":1}");
        ReportOutputValidator.ValidateJson(path, CultureInfo.InvariantCulture);
    }
}
