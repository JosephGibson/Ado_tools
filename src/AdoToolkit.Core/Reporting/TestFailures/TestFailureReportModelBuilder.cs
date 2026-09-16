using System.Collections.ObjectModel;
using System.Resources;
using AdoToolkit.Core.Configuration;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

public static class TestFailureReportModelBuilder
{
    private static readonly ResourceManager Resources = new("AdoToolkit.Core.Resources.Strings", typeof(Messages).Assembly);
    public static TestFailureReportModel Build(AdoBuildTestFailureSet set, TestFailureReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ToolkitVersion);
        ReportCultureResult resolved = ReportCultureResolver.Resolve(options.Culture, options.ConfiguredCulture, options.SessionCulture);
        CultureInfo culture = resolved.Culture;
        Uri collection = set.CollectionUri;
        string project = set.Build.TeamProject;
        if (set.Build.CollectionUri != collection || set.Failures.Any(f => f.CollectionUri != collection) || set.Runs.Any(r => r.CollectionUri != collection))
            throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, culture));
        AdoDiagnostic[] diagnostics = set.Diagnostics.Select(d => new AdoDiagnostic
        {
            Code = d.Code, Severity = d.Severity, Arguments = Array.AsReadOnly(d.Arguments.ToArray()),
            Message = DiagnosticMessageRenderer.Render(d.Code, d.Arguments, culture),
            WorkItemId = d.WorkItemId, StepNumber = d.StepNumber, ReferenceChain = Array.AsReadOnly(d.ReferenceChain.ToArray()),
        }).ToArray();
        AdoTestFailure[] failures = set.Failures.OrderBy(f => f.Classification == AdoTestFailureClassification.Failed ? 0 : 1)
            .ThenBy(f => f.Storage, StringComparer.Ordinal).ThenBy(f => f.TestName, StringComparer.Ordinal)
            .ThenBy(f => f.Attempts.Count == 0 ? 0 : f.Attempts[0].ResultId).Select((f, index) => new AdoTestFailure
            {
                Ordinal = index + 1, Classification = f.Classification, TestName = f.TestName, ShortName = f.ShortName,
                Storage = f.Storage, Title = f.Title, Attempts = Array.AsReadOnly(f.Attempts.OrderBy(a => a.Number).ToArray()),
                TestCase = f.TestCase, History = Array.AsReadOnly(f.History.ToArray()), Owner = f.Owner, Priority = f.Priority, CollectionUri = collection,
            }).ToArray();
        return new TestFailureReportModel
        {
            Build = set.Build, Culture = culture, GeneratedAt = options.GeneratedAt, ToolkitVersion = options.ToolkitVersion,
            Failures = Array.AsReadOnly(failures), History = Array.AsReadOnly(set.History.ToArray()), Runs = Array.AsReadOnly(set.Runs.ToArray()),
            Diagnostics = Array.AsReadOnly(diagnostics), Warnings = resolved.Warnings,
            Labels = new ReadOnlyDictionary<string, string>(Enum.GetValues<AdoMessage>()
                .Where(key => key.ToString().StartsWith("TestReport", StringComparison.Ordinal))
                .ToDictionary(key => key.ToString()[10..], key => Resources.GetString(key.ToString(), culture) ?? throw new InvalidOperationException(key.ToString()), StringComparer.Ordinal)),
            FailedCount = set.FailedCount, FlakyCount = set.FlakyCount,
            Status = diagnostics.Any(d => d.Severity == AdoDiagnosticSeverity.Error) ? AdoTestFailureStatus.Partial : set.Status,
            BuildUrl = AdoWebLinks.Build(collection, project, set.Build.Id),
            ResultsUrl = AdoWebLinks.BuildTestResult(collection, project, set.Build.Id),
            DefinitionUrl = AdoWebLinks.BuildDefinition(collection, project, set.Build.Definition.Id),
            CommitUrl = AdoWebLinks.Commit(collection, project, set.Build.RepositoryType, set.Build.RepositoryId, set.Build.SourceVersion),
        };
    }

    // Applies an export's downloads: the same failures in the same order with updated attachments,
    // and the download diagnostics appended in the report culture.
    internal static TestFailureReportModel WithAttachments(TestFailureReportModel model, IReadOnlyList<AdoTestFailure> failures,
        IReadOnlyList<AdoDiagnostic> added, TestFailureLocalAttachments? local)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(added);
        if (failures.Count != model.Failures.Count || failures.Where((f, i) => f.Ordinal != model.Failures[i].Ordinal).Any())
            throw new ArgumentException(null, nameof(failures));
        AdoDiagnostic[] diagnostics = [.. model.Diagnostics, .. added.Select(d => new AdoDiagnostic
        {
            Code = d.Code, Severity = d.Severity, Arguments = Array.AsReadOnly(d.Arguments.ToArray()),
            Message = DiagnosticMessageRenderer.Render(d.Code, d.Arguments, model.Culture),
            WorkItemId = d.WorkItemId, StepNumber = d.StepNumber, ReferenceChain = Array.AsReadOnly(d.ReferenceChain.ToArray()),
        })];
        return new TestFailureReportModel
        {
            Build = model.Build, Culture = model.Culture, GeneratedAt = model.GeneratedAt, ToolkitVersion = model.ToolkitVersion,
            Failures = Array.AsReadOnly(failures.ToArray()), History = model.History, Runs = model.Runs,
            Diagnostics = Array.AsReadOnly(diagnostics), Labels = model.Labels, Warnings = model.Warnings,
            FailedCount = model.FailedCount, FlakyCount = model.FlakyCount,
            Status = diagnostics.Any(d => d.Severity == AdoDiagnosticSeverity.Error) ? AdoTestFailureStatus.Partial : model.Status,
            BuildUrl = model.BuildUrl, ResultsUrl = model.ResultsUrl, DefinitionUrl = model.DefinitionUrl, CommitUrl = model.CommitUrl,
            LocalAttachments = local,
        };
    }
}
