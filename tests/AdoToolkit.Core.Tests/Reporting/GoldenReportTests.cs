using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-1")]
public sealed class GoldenReportTests
{
    private static readonly string[] Variants = ["direct", "nested", "partial", "parameterized", "french"];
    private static readonly string[] Cultures = ["en-US", "fr-CA"];
    public static TheoryData<string, string, ReportFormat> Cases => new(
        from variant in Variants
        from culture in Cultures
        from format in Enum.GetValues<ReportFormat>()
        select (variant, culture, format));

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExactUtf8BytesMatchReviewedGolden(string variant, string culture, ReportFormat format)
    {
        ReportDocumentModel model = ReportFixture.Model(variant, culture);
        using TestDirectory directory = new();
        string name = "testcase-" + variant + "." + culture + "." + Extension(format);
        string golden = Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Reports", name);
        string actual = Path.Combine(directory.Root, name);
        new AtomicFileWriter().Write(actual, writer => TestCaseExporter.Render(model, writer, format),
            path => TestCaseExporter.Validate(path, model, format), model.Culture,
            cancellationToken: TestContext.Current.CancellationToken);
        byte[] bytes = File.ReadAllBytes(actual);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        if (format == ReportFormat.Json) ReportFixture.AssertValid(File.ReadAllText(actual));
        if (Environment.GetEnvironmentVariable("ADOTOOLKIT_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(golden)!);
            new AtomicFileWriter().Write(golden, writer => TestCaseExporter.Render(model, writer, format),
                path => TestCaseExporter.Validate(path, model, format), model.Culture,
                cancellationToken: TestContext.Current.CancellationToken);
        }
        Assert.True(File.Exists(golden), "Missing reviewed golden: " + name);
        if (format == ReportFormat.Json) ReportFixture.AssertValid(File.ReadAllText(golden));
        Assert.Equal(File.ReadAllBytes(golden), bytes);
    }

    internal static string Extension(ReportFormat format) => format switch
    {
        ReportFormat.Html => "html", ReportFormat.Markdown => "md", ReportFormat.Json => "json",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    internal static string Render(ReportDocumentModel model, ReportFormat format)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        TestCaseExporter.Render(model, writer, format);
        return writer.ToString();
    }
}
