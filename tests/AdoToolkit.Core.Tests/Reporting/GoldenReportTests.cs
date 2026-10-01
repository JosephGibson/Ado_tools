using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Tests.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-1")]
public sealed class GoldenReportTests
{
    private static readonly string[] Variants = ["direct", "nested", "partial", "parameterized", "french"];
    // What only the HTML report renders: formatted steps, and the details of -IncludeDetail.
    private static readonly string[] HtmlVariants = ["rich", "detailed"];
    private static readonly string[] Cultures = ["en-US", "fr-CA"];
    public static TheoryData<string, string, ReportFormat> Cases => new(
        (from variant in Variants
         from culture in Cultures
         from format in Enum.GetValues<ReportFormat>()
         select (variant, culture, format)).Concat(
            from variant in HtmlVariants
            from culture in Cultures
            select (variant, culture, ReportFormat.Html)));

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExactUtf8BytesMatchReviewedGolden(string variant, string culture, ReportFormat format)
    {
        ReportDocumentModel model = ReportFixture.Model(variant, culture);
        using TestDirectory directory = new();
        string name = "testcase-" + variant + "." + culture + "." + Extension(format);
        string golden = Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Reports", name);
        byte[] bytes = Written(model, format, Path.Combine(directory.Root, name), TestContext.Current.CancellationToken);
        if (format == ReportFormat.Json) ReportFixture.AssertValid(Encoding.UTF8.GetString(bytes));
        if (Environment.GetEnvironmentVariable("ADOTOOLKIT_UPDATE_GOLDEN") == "1") Update(golden, bytes, model.Culture, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(golden), "Missing reviewed golden: " + name);
        if (format == ReportFormat.Json) ReportFixture.AssertValid(File.ReadAllText(golden));
        Assert.Equal(File.ReadAllBytes(golden), bytes);
    }

    // The bytes of a report as an export writes and validates it. In an HTML golden the script
    // body and its hash are placeholders, as in the failed-test goldens: the script has its own tests.
    internal static byte[] Written(ReportDocumentModel model, ReportFormat format, string path, CancellationToken cancellationToken)
    {
        new AtomicFileWriter().Write(path, writer => TestCaseExporter.Render(model, writer, format),
            temporary => TestCaseExporter.Validate(temporary, model, format), model.Culture, cancellationToken: cancellationToken);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        if (format != ReportFormat.Html) return bytes;
        string normalized = TestFailureMarkup.NormalizeGolden(Encoding.UTF8.GetString(bytes));
        File.WriteAllText(path, normalized, new UTF8Encoding(false));
        // The placeholders leave every marker of the report in place.
        TestCaseExporter.Validate(path, model, format);
        return Encoding.UTF8.GetBytes(normalized);
    }

    internal static void Update(string golden, byte[] bytes, CultureInfo culture, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(golden)!);
        new AtomicFileWriter().Write(golden, writer => writer.Write(Encoding.UTF8.GetString(bytes)),
            path => Assert.Equal(bytes, File.ReadAllBytes(path)), culture, cancellationToken: cancellationToken);
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
