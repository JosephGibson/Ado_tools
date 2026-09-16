using System.Collections.ObjectModel;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

public static class ReportModelBuilder
{
    public static ReportDocumentModel Build(AdoTestCase testCase, AdoConnection connection, ReportModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        return Build([testCase], connection, options);
    }

    /// <summary>
    /// Validates every case and computes table-of-contents data up front; case models are built
    /// on access so a large document renders one case body at a time (§12.5).
    /// </summary>
    public static ReportDocumentModel Build(IReadOnlyList<AdoTestCase> cases, AdoConnection connection, ReportModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ToolkitVersion);
        if (cases.Count == 0) throw new ArgumentException(Messages.Get(AdoMessage.NoTestCasesToExport, options.Culture), nameof(cases));
        foreach (AdoTestCase testCase in cases)
        {
            ArgumentNullException.ThrowIfNull(testCase);
            if (testCase.CollectionUri != connection.CollectionUri ||
                (testCase.Suite is not null && testCase.Suite.CollectionUri != connection.CollectionUri))
                throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, options.Culture));
        }

        CultureInfo culture = CultureInfo.ReadOnly((CultureInfo)options.Culture.Clone());
        Uri collection = connection.CollectionUri;
        IReadOnlyDictionary<string, string> labels = new ReadOnlyDictionary<string, string>(
            Enum.GetValues<AdoMessage>().Where(key => key.ToString().StartsWith("Report", StringComparison.Ordinal))
                .ToDictionary(key => key.ToString()[6..], key => Messages.Get(key, culture), StringComparer.Ordinal));
        Dictionary<int, int> occurrences = [];
        List<ReportContentsEntry> contents = [];
        foreach (AdoTestCase testCase in cases)
        {
            int occurrence = occurrences[testCase.Id] = occurrences.GetValueOrDefault(testCase.Id) + 1;
            string anchor = "tc-" + testCase.Id.ToString(CultureInfo.InvariantCulture)
                + (occurrence == 1 ? string.Empty : "-" + occurrence.ToString(CultureInfo.InvariantCulture));
            contents.Add(new ReportContentsEntry
            {
                Id = testCase.Id, Title = testCase.Title, Anchor = anchor, Status = testCase.Status,
                Suite = BuildSuite(testCase.Suite, collection), RowCount = testCase.Steps.Count, StepCount = testCase.StepCount,
            });
        }
        bool multiple = cases.Count > 1;
        int withSuite = cases.Count(testCase => testCase.Suite is not null);
        // Input lists are snapshotted now (references only); full case models are built when rendered.
        AdoTestCase[] snapshot = cases.Select(Snapshot).ToArray();
        return new ReportDocumentModel
        {
            Culture = culture, GeneratedAt = options.GeneratedAt, ToolkitVersion = options.ToolkitVersion,
            CollectionUri = collection, Project = string.Join(", ", cases.Select(testCase => testCase.TeamProject).Distinct(StringComparer.Ordinal)),
            Source = !multiple ? "TestCase" : withSuite == cases.Count ? "TestSuite" : withSuite == 0 ? "Query" : "Mixed",
            Labels = labels, Contents = contents.AsReadOnly(),
            Cases = new ReportCaseList(snapshot, testCase => BuildCase(testCase, collection, culture, labels, options)),
        };
    }

    private static AdoTestCase Snapshot(AdoTestCase testCase) => new()
    {
        Id = testCase.Id, Rev = testCase.Rev, Title = testCase.Title, WorkItemType = testCase.WorkItemType,
        TeamProject = testCase.TeamProject, State = testCase.State, Priority = testCase.Priority,
        AutomationStatus = testCase.AutomationStatus, AreaPath = testCase.AreaPath, IterationPath = testCase.IterationPath,
        AssignedTo = testCase.AssignedTo, ChangedBy = testCase.ChangedBy, ChangedDate = testCase.ChangedDate,
        RetrievedAt = testCase.RetrievedAt, WebUrl = testCase.WebUrl, CollectionUri = testCase.CollectionUri,
        Steps = testCase.Steps.ToArray(), SharedSteps = testCase.SharedSteps.ToArray(), Diagnostics = testCase.Diagnostics.ToArray(),
        Suite = testCase.Suite is null ? null : new AdoTestSuiteRef
        {
            PlanId = testCase.Suite.PlanId, SuiteId = testCase.Suite.SuiteId, PlanName = testCase.Suite.PlanName,
            SuiteName = testCase.Suite.SuiteName, SuitePath = testCase.Suite.SuitePath.ToArray(), TeamProject = testCase.Suite.TeamProject,
            CollectionUri = testCase.Suite.CollectionUri, PlanWebUrl = testCase.Suite.PlanWebUrl, WebUrl = testCase.Suite.WebUrl,
        },
        Parameters = new AdoTestParameters
        {
            Source = testCase.Parameters.Source, Names = testCase.Parameters.Names.ToArray(),
            Rows = testCase.Parameters.Rows.Select(row => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(row, StringComparer.Ordinal)).ToArray(),
            SharedParameterSets = testCase.Parameters.SharedParameterSets.ToArray(),
        },
    };

    private static TestCaseReportModel BuildCase(AdoTestCase testCase, Uri collection, CultureInfo culture,
        IReadOnlyDictionary<string, string> labels, ReportModelOptions options)
    {
        AdoDiagnostic[] diagnostics = testCase.Diagnostics.Select(diagnostic => new AdoDiagnostic
        {
            Code = diagnostic.Code, Severity = diagnostic.Severity,
            Arguments = Array.AsReadOnly(diagnostic.Arguments.ToArray()),
            Message = DiagnosticMessageRenderer.Render(diagnostic.Code, diagnostic.Arguments, culture),
            WorkItemId = diagnostic.WorkItemId, StepNumber = diagnostic.StepNumber,
            ReferenceChain = Array.AsReadOnly(diagnostic.ReferenceChain.ToArray()),
        }).ToArray();
        return new TestCaseReportModel
        {
            Culture = culture, ServerUri = new Uri(collection.GetLeftPart(UriPartial.Authority)),
            CollectionUri = collection, Project = testCase.TeamProject,
            Id = testCase.Id, Rev = testCase.Rev, Title = testCase.Title, WorkItemType = testCase.WorkItemType,
            State = testCase.State, Priority = testCase.Priority, AutomationStatus = testCase.AutomationStatus,
            AreaPath = testCase.AreaPath, IterationPath = testCase.IterationPath,
            AssignedTo = testCase.AssignedTo, ChangedBy = testCase.ChangedBy,
            ChangedDate = testCase.ChangedDate, RetrievedAt = testCase.RetrievedAt,
            GeneratedAt = options.GeneratedAt, ToolkitVersion = options.ToolkitVersion,
            WebUrl = AdoWebLinks.WorkItem(collection, testCase.TeamProject, testCase.Id),
            Suite = BuildSuite(testCase.Suite, collection),
            Status = testCase.Status, Diagnostics = Array.AsReadOnly(diagnostics), Labels = labels,
            SharedSteps = Array.AsReadOnly(testCase.SharedSteps.Select(shared => BuildSharedStep(shared, collection)).ToArray()),
            Parameters = new AdoTestParameters
            {
                Source = testCase.Parameters.Source, Names = Array.AsReadOnly(testCase.Parameters.Names.ToArray()),
                Rows = Array.AsReadOnly(testCase.Parameters.Rows.Select(row => (IReadOnlyDictionary<string, string>)
                    new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(row, StringComparer.Ordinal))).ToArray()),
                SharedParameterSets = Array.AsReadOnly(testCase.Parameters.SharedParameterSets.Select(shared => new AdoSharedParameterInfo
                {
                    Id = shared.Id, Title = shared.Title, Rev = shared.Rev, TeamProject = shared.TeamProject,
                    WebUrl = BuildWorkItemUrl(collection, shared.TeamProject, shared.Id),
                }).ToArray()),
            },
            Rows = Array.AsReadOnly(testCase.Steps.OrderBy(step => step.Sequence).Select(step => new ReportRow
            {
                Sequence = step.Sequence, Number = step.Number, Kind = step.Kind,
                Action = step.Action, ExpectedResult = step.ExpectedResult,
                ActionSource = step.ActionSource, ExpectedResultSource = step.ExpectedResultSource,
                SharedStep = step.SharedStep is null ? null : BuildSharedStep(step.SharedStep, collection),
                IsExpanded = step.IsExpanded, SourceWorkItemId = step.SourceWorkItemId,
                SourceRev = step.SourceRev, SourceStepId = step.SourceStepId,
                SharedStepPath = Array.AsReadOnly(step.SharedStepPath.Select(shared => BuildSharedStep(shared, collection)).ToArray()),
                DiagnosticCode = step.DiagnosticCode,
                DiagnosticMessage = step.DiagnosticCode is null ? null : diagnostics.FirstOrDefault(diagnostic =>
                    diagnostic.Code == step.DiagnosticCode && diagnostic.StepNumber == step.Number)?.Message
                    ?? DiagnosticMessageRenderer.Render(step.DiagnosticCode, Array.Empty<string>(), culture),
            }).ToArray()),
        };
    }

    private static Uri? BuildWorkItemUrl(Uri collection, string? project, int id) =>
        project is null ? null : AdoWebLinks.WorkItem(collection, project, id);

    private static AdoSharedStepInfo BuildSharedStep(AdoSharedStepInfo shared, Uri collection) => new()
    {
        Id = shared.Id, Title = shared.Title, Rev = shared.Rev, TeamProject = shared.TeamProject,
        ReferenceCount = shared.ReferenceCount, WebUrl = BuildWorkItemUrl(collection, shared.TeamProject, shared.Id),
    };

    private static AdoTestSuiteRef? BuildSuite(AdoTestSuiteRef? suite, Uri collection) => suite is null ? null : new()
    {
        PlanId = suite.PlanId, SuiteId = suite.SuiteId, PlanName = suite.PlanName, SuiteName = suite.SuiteName,
        SuitePath = Array.AsReadOnly(suite.SuitePath.ToArray()), TeamProject = suite.TeamProject, CollectionUri = collection,
        PlanWebUrl = AdoWebLinks.TestPlan(collection, suite.TeamProject, suite.PlanId),
        WebUrl = AdoWebLinks.TestPlan(collection, suite.TeamProject, suite.PlanId, suite.SuiteId),
    };
}
