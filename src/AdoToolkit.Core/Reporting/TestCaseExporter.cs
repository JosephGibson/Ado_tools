using AdoToolkit.Core.Configuration;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting.Html;
using AdoToolkit.Core.Reporting.Json;
using AdoToolkit.Core.Reporting.Markdown;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

public sealed class TestCaseExporter
{
    private readonly AtomicFileWriter writer;
    private readonly IDocumentLauncher launcher;
    private readonly Func<string>? downloads;

    public TestCaseExporter(IDocumentLauncher launcher, AtomicFileWriter? writer = null, Func<string>? downloads = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        this.launcher = launcher;
        this.writer = writer ?? new AtomicFileWriter();
        this.downloads = downloads;
    }

    public FileInfo? Export(IReadOnlyList<AdoTestCase> cases, AdoConnection connection, TestCaseExportOptions options,
        Func<string, bool> shouldProcess, Action<string> warning, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(shouldProcess);
        ArgumentNullException.ThrowIfNull(warning);
        if (options.IncludeSource && options.Format != ReportFormat.Json)
            throw new ArgumentException(Messages.Get(AdoMessage.IncludeSourceJsonOnly, options.SessionCulture), nameof(options));
        if (cases.Count == 0) throw new ArgumentException(Messages.Get(AdoMessage.NoTestCasesToExport, options.SessionCulture), nameof(cases));
        ReportCultureResult culture = ReportCultureResolver.Resolve(options.Culture, options.ConfiguredCulture, options.SessionCulture);
        foreach (string message in culture.Warnings) warning(message);
        // Any number of cases yields exactly one document (§12.5).
        ReportDocumentModel model = ReportModelBuilder.Build(cases, connection, new ReportModelOptions
        {
            Culture = culture.Culture, GeneratedAt = options.GeneratedAt, ToolkitVersion = options.ToolkitVersion,
        });
        string path = ReportFileNames.Resolve(options.Path, ReportFileNames.ForCases(cases, options.GeneratedAt, options.Format),
            options.SessionCulture, downloads);
        if (!shouldProcess(path)) return null;
        FileInfo file = writer.Write(path, output => Render(model, output, options.Format, options.IncludeSource),
            temporary => Validate(temporary, model, options.Format), options.SessionCulture, options.NoClobber, cancellationToken);
        if (options.Open) launcher.Open(file.FullName);
        return file;
    }

    public static void Render(ReportDocumentModel model, TextWriter writer, ReportFormat format, bool includeSource = false)
    {
        switch (format)
        {
            case ReportFormat.Html: HtmlTestCaseRenderer.Render(model, writer); break;
            case ReportFormat.Markdown: MarkdownTestCaseRenderer.Render(model, writer); break;
            case ReportFormat.Json: JsonTestCaseRenderer.Render(model, writer, includeSource); break;
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    public static void Validate(string path, ReportDocumentModel model, ReportFormat format)
    {
        switch (format)
        {
            case ReportFormat.Html: ReportOutputValidator.ValidateHtml(path, model); break;
            case ReportFormat.Markdown: ReportOutputValidator.ValidateMarkdown(path, model); break;
            case ReportFormat.Json: ReportOutputValidator.ValidateJson(path, model); break;
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }
    }
}
