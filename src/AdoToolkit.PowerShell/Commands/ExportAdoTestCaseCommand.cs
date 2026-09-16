using System.IO;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit;

[Cmdlet(VerbsData.Export, "AdoTestCase", SupportsShouldProcess = true, DefaultParameterSetName = "Input")]
[OutputType(typeof(FileInfo))]
public sealed class ExportAdoTestCaseCommand : AdoCmdletBase
{
    private readonly List<AdoTestCase> cases = [];

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = "Input")]
    [ValidateNotNullOrEmpty]
    public AdoTestCase[] InputObject { get; set; } = [];

    [Parameter] public ReportFormat Format { get; set; } = ReportFormat.Html;
    [Parameter] [ValidateNotNullOrEmpty] public string? Culture { get; set; }
    [Parameter] [ValidateNotNullOrEmpty] public string? Path { get; set; }
    [Parameter] public SwitchParameter NoClobber { get; set; }
    [Parameter] public SwitchParameter IncludeSource { get; set; }
    [Parameter] public SwitchParameter Open { get; set; }
    [Parameter] public AdoConnection? Connection { get; set; }

    protected override void BeginProcessing()
    {
        base.BeginProcessing();
        if (IncludeSource && Format != ReportFormat.Json)
            ThrowTerminatingError(new ErrorRecord(new ArgumentException(Messages.Get(AdoMessage.IncludeSourceJsonOnly, MessageCulture)),
                "IncludeSourceJsonOnly", ErrorCategory.InvalidArgument, Format));
    }

    protected override void ProcessRecord() => cases.AddRange(InputObject);

    protected override void EndProcessing() => RunLocal(() =>
    {
        if (cases.Count == 0)
        {
            WriteWarning(Messages.Get(AdoMessage.NoTestCasesToExport, MessageCulture));
            return;
        }
        AdoConnection connection = ResolveConnection(Connection);
        foreach (AdoTestCase item in cases) EnsureSameCollection(item.CollectionUri, connection);
        string? resolvedPath = null;
        if (Path is not null)
        {
            resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path, out ProviderInfo provider, out _);
            if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase))
                ThrowTerminatingError(new ErrorRecord(new ArgumentException(Messages.Get(AdoMessage.FileSystemPathRequired, MessageCulture)),
                    "FileSystemPathRequired", ErrorCategory.InvalidArgument, Path));
        }
        AdoConfiguration configuration = new ConfigurationStore().Load(MessageCulture);
        foreach (string warning in configuration.Warnings) WriteWarning(warning);
        TestCaseExportOptions options = new()
        {
            Format = Format, Culture = Culture, ConfiguredCulture = configuration.Reporting.Culture,
            SessionCulture = MessageCulture, Path = resolvedPath, NoClobber = NoClobber, IncludeSource = IncludeSource,
            Open = Open, GeneratedAt = DateTimeOffset.Now,
            ToolkitVersion = typeof(ExportAdoTestCaseCommand).Assembly.GetName().Version!.ToString(),
        };
        FileInfo? output = new TestCaseExporter(new ShellDocumentLauncher()).Export(cases, connection, options,
            target => ShouldProcess(target, Messages.Get(AdoMessage.ExportReport, MessageCulture)), WriteWarning);
        if (output is not null) WriteObject(output);
    });
}
