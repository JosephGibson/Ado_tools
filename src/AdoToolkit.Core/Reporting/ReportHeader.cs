using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

internal static class ReportHeader
{
    internal static IEnumerable<(string Label, string Value, Uri? Url)> Fields(TestCaseReportModel model)
    {
        CultureInfo culture = model.Culture;
        yield return ("Id", model.Id.ToString(culture), null);
        yield return ("Revision", model.Rev.ToString(culture), null);
        yield return ("WorkItemType", model.WorkItemType, null);
        yield return ("State", model.State, null);
        if (model.Priority.HasValue) yield return ("Priority", model.Priority.Value.ToString(culture), null);
        if (model.AutomationStatus is not null) yield return ("AutomationStatus", model.AutomationStatus, null);
        if (model.AreaPath is not null) yield return ("AreaPath", model.AreaPath, null);
        if (model.IterationPath is not null) yield return ("IterationPath", model.IterationPath, null);
        if (model.AssignedTo is not null) yield return ("AssignedTo", model.AssignedTo.DisplayName, null);
        yield return ("ChangedDate", model.ChangedDate.ToString("D", culture) + " " + model.ChangedDate.ToString("HH:mm zzz", culture), null);
        if (model.ChangedBy is not null) yield return ("ChangedBy", model.ChangedBy.DisplayName, null);
        yield return ("Server", model.ServerUri.AbsoluteUri, null);
        yield return ("Collection", model.CollectionUri.AbsoluteUri, null);
        yield return ("Project", model.Project, null);
        if (model.Suite is not null)
        {
            yield return ("TestPlan", model.Suite.PlanName, model.Suite.PlanWebUrl);
            yield return ("TestSuite", model.Suite.SuiteName, model.Suite.WebUrl);
        }
        yield return ("GeneratedAt", model.GeneratedAt.ToString("D", culture) + " " + model.GeneratedAt.ToString("HH:mm zzz", culture), null);
        yield return ("ToolkitVersion", model.ToolkitVersion, null);
        yield return ("StepCount", model.StepCount.ToString(culture), null);
        yield return ("Status", model.Labels[model.Status.ToString()], null);
        if (model.Status == AdoTestCaseStatus.Partial)
            yield return ("Diagnostics", Messages.Get(AdoMessage.PartialTestCase, culture, model.Id.ToString(culture),
                model.ErrorCount, model.WarningCount, model.InformationCount), null);
    }

    // Multi-case cover (§12.5): server, collection, project, source, generated-at, totals by status.
    internal static IEnumerable<(string Label, string Value, Uri? Url)> CoverFields(ReportDocumentModel model)
    {
        CultureInfo culture = model.Culture;
        yield return ("Server", model.ServerUri.AbsoluteUri, null);
        yield return ("Collection", model.CollectionUri.AbsoluteUri, null);
        yield return ("Project", model.Project, null);
        AdoTestSuiteRef[] suites = model.Contents.Select(entry => entry.Suite).OfType<AdoTestSuiteRef>()
            .DistinctBy(suite => (suite.PlanId, suite.SuiteId)).ToArray();
        foreach (AdoTestSuiteRef plan in suites.DistinctBy(suite => suite.PlanId))
            yield return ("TestPlan", plan.PlanName, plan.PlanWebUrl);
        if (suites.Length == 1) yield return ("TestSuite", suites[0].SuiteName, suites[0].WebUrl);
        else if (suites.Length > 1 && suites.All(suite => suite.PlanId == suites[0].PlanId))
        {
            // Several suites of one plan: show the path they share, such as the recursion root.
            IReadOnlyList<string> common = suites[0].SuitePath;
            int length = suites.Min(suite => suite.SuitePath.Count);
            int shared = 0;
            while (shared < length && suites.All(suite => string.Equals(suite.SuitePath[shared], common[shared], StringComparison.Ordinal))) shared++;
            if (shared > 0) yield return ("TestSuite", SuitePathText(common.Take(shared)), null);
        }
        if (model.Contents.Any(entry => entry.Suite is null)) yield return ("Source", model.Labels["QuerySource"], null);
        yield return ("GeneratedAt", model.GeneratedAt.ToString("D", culture) + " " + model.GeneratedAt.ToString("HH:mm zzz", culture), null);
        yield return ("CaseCount", model.Contents.Count.ToString(culture), null);
        yield return ("Complete", model.CompleteCount.ToString(culture), null);
        yield return ("Partial", model.PartialCount.ToString(culture), null);
    }

    // Groups keep the order of their first case; cases without a suite form one query group.
    internal static IEnumerable<(string Label, IReadOnlyList<ReportContentsEntry> Entries)> Groups(ReportDocumentModel model) =>
        model.Contents.GroupBy(entry => entry.Suite is null ? (0, 0) : (entry.Suite.PlanId, entry.Suite.SuiteId))
            .Select(group => (group.First().Suite is AdoTestSuiteRef suite
                    ? SuitePathText(suite.SuitePath.Count > 0 ? suite.SuitePath : [suite.SuiteName]) : model.Labels["QuerySource"],
                (IReadOnlyList<ReportContentsEntry>)group.ToArray()));

    internal static string SuitePathText(IEnumerable<string> path) =>
        string.Join(" › ", path.Select(segment => SinkEncoding.NormalizeLines(segment).Replace('\n', ' ')));
}
