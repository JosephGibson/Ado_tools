using System.Text;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.Reporting.Highlighting;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// One scanned page per build: views for an overview table, failures grouped by error, compact
// failure cards, and runs with history. Every section renders without scripts; the script only
// switches views, filters and navigates. Every <details> starts closed.
public static partial class HtmlTestFailureRenderer
{
    private const int MaximumSummaryCharacters = 240;

    public static void Render(TestFailureReportModel model, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);
        new Document(model, writer).Write();
    }

    // First non-empty line of an error, shortened for one-line summaries.
    internal static string? FirstLine(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (string line in SinkEncoding.NormalizeLines(text).Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            return trimmed.Length <= MaximumSummaryCharacters ? trimmed : trimmed[..MaximumSummaryCharacters] + "…";
        }
        return null;
    }

    // The error line of the last failed attempt, else of any attempt.
    internal static string? LatestError(AdoTestFailure failure) =>
        failure.Attempts.Reverse().Where(a => a.OutcomeClass == AdoTestOutcomeClass.Failure).Select(a => FirstLine(a.ErrorMessage)).FirstOrDefault(l => l is not null)
        ?? failure.Attempts.Reverse().Select(a => FirstLine(a.ErrorMessage)).FirstOrDefault(l => l is not null);

    // Errors that differ only in numbers or GUIDs fall into one group.
    internal static string ErrorKey(string line) => Digits().Replace(Guids().Replace(line, "…"), "#");

    [GeneratedRegex("[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    private static partial Regex Guids();

    [GeneratedRegex("[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();

    private sealed class Document(TestFailureReportModel model, TextWriter writer)
    {
        private long inlined;
        private CultureInfo Culture => model.Culture;
        private Uri Collection => model.Build.CollectionUri;
        private string Project => model.Build.TeamProject;
        private PipelineGrouping Grouping => model.Grouping;
        private void W(string value) => writer.Write(value);
        private void T(string? value) => W(SinkEncoding.Attribute(value ?? string.Empty));
        private string L(string key) => model.Labels[key];
        private string M(AdoMessage key) => Messages.Get(key, Culture);
        private string F(AdoMessage key, params object[] arguments) => Messages.Get(key, Culture, arguments);
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Anchor(AdoTestFailure failure) => "f-" + N(failure.Ordinal);
        private static string Anchor(AdoTestFailure failure, AdoTestAttempt attempt) => Anchor(failure) + "-a" + N(attempt.Number);
        private string? Duration(TimeSpan? value) => value.HasValue ? F(AdoMessage.TestReportSeconds, value.Value.TotalSeconds) : null;
        // Server times are UTC; every time shows in the export's offset, like the generation time.
        private string? Date(DateTimeOffset? value) => value?.ToOffset(model.GeneratedAt.Offset).ToString("g", Culture);
        private static int Attachments(AdoTestFailure failure) => failure.Attempts.Sum(a => a.Attachments.Count);
        private static AdoTestHistoryOutcome Status(AdoTestFailure failure) => failure.Classification == AdoTestFailureClassification.Flaky
            ? AdoTestHistoryOutcome.Flaky : AdoTestHistoryOutcome.Failed;
        private static AdoTestHistoryOutcome Status(AdoTestOutcomeClass outcome) => outcome switch
        {
            AdoTestOutcomeClass.Pass => AdoTestHistoryOutcome.Passed, AdoTestOutcomeClass.Failure => AdoTestHistoryOutcome.Failed, _ => AdoTestHistoryOutcome.Other,
        };
        private string GroupLabel(int? group) => group is int index && index < Grouping.Labels.Count && Grouping.Labels[index] is string label
            ? label : L("NoPipelineName");
        private IReadOnlyList<(int? Group, IReadOnlyList<AdoTestAttempt> Attempts)> Groups(AdoTestFailure failure) => TestFailureGroups.Of(failure, Grouping);
        // __default is the agent's placeholder for an unnamed level, so it shows as no name.
        private static string? PipelineName(string? value) =>
            string.IsNullOrWhiteSpace(value) || string.Equals(value, "__default", StringComparison.OrdinalIgnoreCase) ? null : value;
        private void Heading(int level, string label)
        { W("<h" + N(level) + (level == 2 ? " class=\"section-heading\"" : "") + ">"); T(label); W("</h" + N(level) + ">\n"); }

        internal void Write()
        {
            string script = TestFailureAssets.Read("test-failures.js");
            W("<!doctype html>\n<html lang=\""); T(Culture.Name);
            W("\" data-failure-count=\"" + N(model.Failures.Count) + "\" data-history-count=\"" + N(model.History.Count) + "\">\n<head>\n<meta charset=\"utf-8\">\n");
            W("<meta http-equiv=\"Content-Security-Policy\" content=\""); T(ContentSecurityPolicy.Create([script])); W("\">\n");
            W("<meta name=\"generator\" content=\"AdoToolkit "); T(model.ToolkitVersion); W("\">\n");
            W("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<title>"); T(L("Heading")); W(" — "); T(model.Build.BuildNumber); W("</title>\n<style>");
            W(TestFailureAssets.Read("report-base.css")); W(TestFailureAssets.Read("test-failures.css")); W("</style>\n</head>\n<body>\n");
            Header();
            W("<main id=\"report-content\">\n");
            Overview();
            ByError();
            Details();
            Runs();
            Diagnostics();
            W("</main>\n");
            W("<p class=\"copy-feedback\" role=\"status\" data-copy-status hidden data-label-done=\""); T(L("Copied")); W("\" data-label-selected=\""); T(L("SelectCopy")); W("\"></p>\n");
            W("<script>"); W(script); W("</script>\n</body>\n</html>\n");
        }

        private bool HasDiagnostics => model.Diagnostics.Count > 0 || model.Status == AdoTestFailureStatus.Partial;

        private void Header()
        {
            W("<a class=\"skip-link\" href=\"#overview\">"); T(L("Overview")); W("</a>\n<header class=\"top-bar\"><div class=\"top-bar-inner\">\n");
            W("<div class=\"title-line\"><span class=\"report-brand\">"); T(M(AdoMessage.ReportBrand)); W("</span><h1>"); T(L("Heading")); W("</h1>");
            W("<span class=\"pipeline-name\">"); Link(model.DefinitionUrl, model.Build.Definition.Name); W("</span><span class=\"build-number\">");
            Link(model.BuildUrl, model.Build.BuildNumber); W("</span>");
            if (model.Build.SourceBranch is not null) { W("<code>"); T(model.Build.SourceBranch); W("</code>"); }
            if (model.Build.SourceVersion is { } version)
            {
                W("<span class=\"commit\" title=\""); T(L("Commit")); W("\">");
                if (model.CommitUrl is not null) Link(model.CommitUrl, version[..7]); else T(version);
                W("</span>");
            }
            if (model.Build.Result is not null) { W("<span>"); T(model.Build.Result); W("</span>"); }
            if (model.Build.FinishTime is not null) { W("<span>"); T(L("Finished") + " " + Date(model.Build.FinishTime)); W("</span>"); }
            W("<span class=\"results-link\">"); Link(model.ResultsUrl, L("Result")); W("</span>");
            Chip("failed", "✕ " + L("Failed"), model.FailedCount, null);
            Chip("flaky", "≈ " + L("Flaky"), model.FlakyCount, model.FlakyExcluded ? L("NotShown") : null);
            Chip("attachments", L("Attachments"), model.Failures.Sum(Attachments), null);
            if (model.Status == AdoTestFailureStatus.Partial)
            { W("<a class=\"partial-link\" href=\"#diagnostics\"><span aria-hidden=\"true\">!</span> "); T(M(AdoMessage.ReportPartial)); W("</a>"); }
            W("</div>\n<nav class=\"view-links\" aria-label=\""); T(M(AdoMessage.ReportNavigation)); W("\">");
            foreach ((string id, string label) in Views())
            { W("<a href=\"#" + id + "\" data-view-link=\"" + id + "\">"); T(label); W("</a>"); }
            W("</nav>\n");
            Filters();
            W("</div></header>\n");
        }

        private IEnumerable<(string Id, string Label)> Views()
        {
            yield return ("overview", L("Overview"));
            yield return ("by-error", L("ByError"));
            yield return ("details", L("Details"));
            yield return ("runs", L("RunsAndHistory"));
            if (HasDiagnostics) yield return ("diagnostics", M(AdoMessage.ReportDiagnostics) + " " + model.Diagnostics.Count.ToString(Culture));
        }

        private void Chip(string css, string label, int count, string? note)
        {
            W("<span class=\"count-chip status-" + css + "\"><span class=\"count-label\">"); T(label);
            W("</span><strong class=\"count-value\">"); T(count.ToString(Culture)); W("</strong>");
            if (note is not null) { W("<span class=\"count-note\">"); T(note); W("</span>"); }
            W("</span>");
        }

        private void Filters()
        {
            W("<div class=\"interactive filter-controls\" data-enhance hidden>\n<label class=\"search\">"); T(L("Filter")); W(" <input type=\"search\" data-filter></label>\n");
            if (Grouping.IsGrouped)
            {
                W("<label>"); T(L("FailingIn")); W(" <select data-failing><option value=\"\">"); T(L("AnyGroup")); W("</option>");
                for (int index = 0; index < Grouping.Labels.Count; index++) { W("<option value=\"g" + N(index) + "\">"); T(GroupLabel(index)); W("</option>"); }
                for (int index = 0; index < Grouping.Labels.Count; index++)
                { W("<option value=\"o" + N(index) + "\">"); T(F(AdoMessage.TestReportGroupOnly, GroupLabel(index))); W("</option>"); }
                W("</select></label>\n");
            }
            if (model.Failures.Any(f => f.Classification == AdoTestFailureClassification.Flaky))
            { Toggle("failed", L("Failed"), true); Toggle("flaky", L("Flaky"), true); }
            Toggle("attachments", L("HasAttachments"), false);
            // Only meaningful when some test is tracked; it leaves the tests that still need a bug.
            if (model.Failures.Any(static f => f.HasOpenBug)) Toggle("untracked", L("WithoutOpenBug"), false);
            Button("expand", L("ExpandAll")); Button("collapse", L("CollapseAll"));
            W("<span class=\"filter-result-count\" role=\"status\" aria-live=\"polite\" data-filter-count data-label-count=\""); T(L("FilterCount")); W("\"></span>");
            W("<p class=\"text-muted keyboard-hint\">"); T(L("NavigationHint")); W("</p></div>\n");
        }

        private void Overview()
        {
            W("<section class=\"view\" id=\"overview\" data-view>\n"); Heading(2, L("Overview"));
            if (model.Failures.Count == 0) { W("<p>"); T(L("NoFailures")); W("</p>\n</section>\n"); return; }
            W("<p class=\"text-muted legend\">"); T(L("GroupLegend")); W("</p>\n<p data-no-matches hidden>"); T(L("NoMatches")); W("</p>\n");
            W("<div class=\"table-scroll\"><table class=\"failure-table\">\n"); TableHead(error: true);
            W("<tbody>\n");
            foreach (AdoTestFailure failure in model.Failures) Row(failure, error: true, strip: true);
            W("</tbody></table></div>\n</section>\n");
        }

        private void ByError()
        {
            W("<section class=\"view\" id=\"by-error\" data-view>\n"); Heading(2, L("ByError"));
            if (model.Failures.Count == 0) { W("<p>"); T(L("NoFailures")); W("</p>\n</section>\n"); return; }
            W("<p data-no-matches hidden>"); T(L("NoMatches")); W("</p>\n<div class=\"table-scroll\"><table class=\"failure-table by-error\">\n");
            TableHead(error: false);
            var clusters = model.Failures.Select(failure => (Failure: failure, Line: LatestError(failure)))
                .GroupBy(item => item.Line is null ? null : ErrorKey(item.Line), StringComparer.Ordinal)
                .OrderBy(group => group.Key is null ? 1 : 0).ThenByDescending(group => group.Count()).ThenBy(group => group.First().Failure.Ordinal);
            int columns = 3 + (Grouping.IsGrouped ? Grouping.Labels.Count : 1);
            foreach (var cluster in clusters)
            {
                W("<tbody class=\"error-cluster\"><tr class=\"cluster-heading\"><th scope=\"colgroup\" colspan=\"" + N(columns) + "\"><span class=\"cluster-count\">");
                T(cluster.Count().ToString(Culture)); W("</span> <code>"); T(cluster.First().Line ?? L("NoErrorMessage")); W("</code></th></tr>\n");
                foreach (var item in cluster) Row(item.Failure, error: false, strip: false);
                W("</tbody>\n");
            }
            W("</table></div>\n</section>\n");
        }

        private void TableHead(bool error)
        {
            W("<thead><tr><th scope=\"col\">#</th><th scope=\"col\">"); T(L("Test")); W("</th><th scope=\"col\">"); T(L("TestCase")); W("</th>");
            if (Grouping.IsGrouped)
                for (int index = 0; index < Grouping.Labels.Count; index++) { W("<th scope=\"col\">"); T(GroupLabel(index)); W("</th>"); }
            else { W("<th scope=\"col\">"); T(L("Attempts")); W("</th>"); }
            if (error) { W("<th scope=\"col\">"); T(L("LatestError")); W("</th>"); }
            W("</tr></thead>\n");
        }

        private void Row(AdoTestFailure failure, bool error, bool strip)
        {
            string anchor = Anchor(failure);
            W("<tr data-index-for=\"" + anchor + "\"><td class=\"col-number\">"); Glyph(Status(failure)); W(" " + N(failure.Ordinal) + "</td><td class=\"col-test\"><a href=\"#" + anchor + "\">");
            T(failure.ShortName); W("</a>");
            // Before the class name, so a long name that is cut off never hides it.
            if (failure.Bugs.Where(bug => bug.Id > 0 && bug.IsOpen == true).MinBy(bug => bug.Id) is { } openBug)
            {
                W(" <a class=\"open-bug-marker\" rel=\"noreferrer\" href=\"");
                T(AdoWebLinks.WorkItem(Collection, openBug.TeamProject ?? Project, openBug.Id).AbsoluteUri);
                W("\">"); T(L("OpenBug")); W("</a>");
            }
            // The class name only; the card shows the full name.
            if (AttemptGrouper.ClassName(failure.TestName) is { } type) { W(" <span class=\"text-muted\">"); T(type); W("</span>"); }
            W("</td><td class=\"col-case\">");
            if (failure.TestCase is { Id: > 0 } testCase) Link(AdoWebLinks.WorkItem(Collection, Project, testCase.Id), "#" + testCase.Id.ToString(Culture));
            else W("<span class=\"text-muted\">—</span>");
            W("</td>");
            IReadOnlyList<(int? Group, IReadOnlyList<AdoTestAttempt> Attempts)> groups = Groups(failure);
            if (Grouping.IsGrouped)
                for (int index = 0; index < Grouping.Labels.Count; index++)
                    GroupCell(failure, groups.FirstOrDefault(g => g.Group == index).Attempts ?? [], strip);
            else GroupCell(failure, failure.Attempts, strip);
            if (error) { W("<td class=\"col-error\">"); T(LatestError(failure)); W("</td>"); }
            W("</tr>\n");
        }

        private void GroupCell(AdoTestFailure failure, IReadOnlyList<AdoTestAttempt> attempts, bool strip)
        {
            W("<td class=\"col-group\">");
            if (attempts.Count == 0) { W("<span class=\"text-muted\">—</span></td>"); return; }
            GroupStatus(attempts);
            if (strip) Strip(failure, attempts, links: true);
            W("</td>");
        }

        // Glyph, hidden status word and failed/total count, for a table cell or group summary.
        private void GroupStatus(IReadOnlyList<AdoTestAttempt> attempts)
        {
            AdoTestHistoryOutcome status = TestFailureGroups.Status(attempts);
            W("<span class=\"group-status status-" + StatusPresentation.Css(status) + "\"><span aria-hidden=\"true\">" + StatusPresentation.Glyph(status) + "</span> ");
            W("<span class=\"sr-only\">"); T(StatusPresentation.Label(status, Culture)); W("</span>");
            T(attempts.Count(a => a.OutcomeClass == AdoTestOutcomeClass.Failure).ToString(Culture) + "/" + attempts.Count.ToString(Culture)); W("</span>");
        }

        // One square per attempt: filled for a failure, hollow for a pass, dashed otherwise.
        private void Strip(AdoTestFailure failure, IReadOnlyList<AdoTestAttempt> attempts, bool links)
        {
            W(links ? " <span class=\"mini-strip\">" : " <span class=\"mini-strip\" aria-hidden=\"true\">");
            foreach (AdoTestAttempt attempt in attempts)
            {
                AdoTestHistoryOutcome status = Status(attempt.OutcomeClass);
                if (!links) { W("<span class=\"sq status-" + StatusPresentation.Css(status) + "\"></span>"); continue; }
                W("<a class=\"sq status-" + StatusPresentation.Css(status) + "\" href=\"#" + Anchor(failure, attempt) + "\" aria-label=\"");
                T(F(AdoMessage.TestReportAttemptOf, attempt.Number, failure.Attempts.Count) + ": " + StatusPresentation.Label(status, Culture)); W("\"></a>");
            }
            W("</span>");
        }

        private void Glyph(AdoTestHistoryOutcome status)
        {
            W("<span class=\"status-glyph status-" + StatusPresentation.Css(status) + "\" role=\"img\" aria-label=\""); T(StatusPresentation.Label(status, Culture));
            W("\">" + StatusPresentation.Glyph(status) + "</span>");
        }

        private void Details()
        {
            W("<section class=\"view\" id=\"details\" data-view>\n"); Heading(2, L("Details"));
            if (model.Failures.Count == 0) { W("<p>"); T(L("NoFailures")); W("</p>\n"); }
            else { W("<p data-no-matches hidden>"); T(L("NoMatches")); W("</p>\n"); }
            foreach (AdoTestFailure failure in model.Failures) Card(failure);
            W("</section>\n");
        }

        private void Card(AdoTestFailure failure)
        {
            string anchor = Anchor(failure);
            IReadOnlyList<(int? Group, IReadOnlyList<AdoTestAttempt> Attempts)> groups = Groups(failure);
            W("<article class=\"card failure-card\" id=\"" + anchor + "\" tabindex=\"-1\" data-attempt-count=\"" + N(failure.Attempts.Count)
                + "\" data-classification=\"" + StatusPresentation.Css(Status(failure)) + "\" data-attachment-count=\"" + N(Attachments(failure)) + "\"");
            if (failure.TestCase is { Id: > 0 } testCase) W(" data-test-case=\"" + N(testCase.Id) + "\"");
            if (failure.HasOpenBug) W(" data-open-bug");
            if (Grouping.IsGrouped)
                W(" data-failing=\"" + string.Join(' ', groups.Where(g => g.Group.HasValue && TestFailureGroups.Status(g.Attempts) == AdoTestHistoryOutcome.Failed)
                    .Select(g => N(g.Group!.Value))) + "\"");
            W(">\n<header><span class=\"failure-number\" aria-hidden=\"true\">" + N(failure.Ordinal) + "</span>"); StatusPresentation.Write(writer, Status(failure), Culture);
            W("<h2 data-short-name>"); T(failure.ShortName); W("</h2>"); TestCaseHeading(failure.TestCase);
            W("<span class=\"card-links\">");
            if (failure.Attempts.Count > 0)
            { AdoTestAttempt last = failure.Attempts[^1]; Link(Result(last.RunId, last.ResultId), L("Result")); Link(AdoWebLinks.TestRun(Collection, Project, last.RunId), L("Run")); }
            W("</span></header>\n");
            if (failure.TestName is not null)
            { W("<div class=\"name-section\"><code data-full-name data-copy-value>"); T(failure.TestName); W("</code> "); Button("copy", L("Copy"), description: L("FullName")); W("</div>\n"); }
            W("<dl class=\"metadata-grid\">"); Field(L("Storage"), failure.Storage); Field(M(AdoMessage.ReportTitle), failure.Title);
            Field(L("Owner"), Identity(failure.Owner)); Field(M(AdoMessage.ReportPriority), failure.Priority); W("</dl>\n");
            BugList(failure.Bugs);
            if (failure.History.Count > 0)
            { W("<div class=\"card-history\"><span class=\"strip-label\">"); T(L("History")); W("</span>"); HistoryStrip.Write(writer, failure.History, Collection, Project, Culture); W("</div>\n"); }
            Dictionary<string, int> errors = new(StringComparer.Ordinal), stacks = new(StringComparer.Ordinal);
            foreach ((int? group, IReadOnlyList<AdoTestAttempt> attempts) in groups)
            {
                if (!Grouping.IsGrouped)
                {
                    PipelineNames(attempts);
                    foreach (AdoTestAttempt attempt in attempts) Attempt(failure, attempt, errors, stacks);
                    continue;
                }
                W("<details class=\"attempt-group\" id=\"" + anchor + "-g" + N((group ?? Grouping.Labels.Count) + 1) + "\"><summary><span class=\"group-label\">");
                T(GroupLabel(group)); W("</span> "); StatusPresentation.Write(writer, TestFailureGroups.Status(attempts), Culture);
                W(" <span class=\"group-count\">"); T(F(AdoMessage.TestReportGroupFailed, attempts.Count(a => a.OutcomeClass == AdoTestOutcomeClass.Failure), attempts.Count)); W("</span>");
                Strip(failure, attempts, links: false); W("</summary>\n");
                PipelineNames(attempts);
                foreach (AdoTestAttempt attempt in attempts) Attempt(failure, attempt, errors, stacks);
                W("</details>\n");
            }
            W("</article>\n");
        }

        private void Attempt(AdoTestFailure failure, AdoTestAttempt attempt, Dictionary<string, int> errors, Dictionary<string, int> stacks)
        {
            W("<details class=\"attempt\" id=\"" + Anchor(failure, attempt) + "\">");
            W("<summary><span class=\"attempt-title\">"); T(F(AdoMessage.TestReportAttemptOf, attempt.Number, failure.Attempts.Count)); W("</span> ");
            Outcome(attempt.Outcome, attempt.OutcomeClass);
            if (attempt.Duration is not null) { W(" <span class=\"attempt-meta\">"); T(Duration(attempt.Duration)); W("</span>"); }
            if (attempt.ComputerName is not null) { W(" <span class=\"attempt-meta\">"); T(attempt.ComputerName); W("</span>"); }
            if (FirstLine(attempt.ErrorMessage) is { } line) { W(" <span class=\"attempt-error\">"); T(line); W("</span>"); }
            W("</summary>\n<dl class=\"metadata-grid\">");
            AdoTestRun? run = model.Runs.FirstOrDefault(r => r.Id == attempt.RunId);
            Field(L("Source"), L(attempt.Source.ToString()));
            FieldLink(L("Run"), AdoWebLinks.TestRun(Collection, Project, attempt.RunId),
                string.IsNullOrEmpty(run?.Name) ? attempt.RunId.ToString(Culture) : run.Name + " (" + attempt.RunId.ToString(Culture) + ")");
            FieldLink(L("Result"), Result(attempt.RunId, attempt.ResultId), attempt.ResultId.ToString(Culture));
            Field(L("Started"), Date(attempt.StartedDate)); Field(L("Finished"), Date(attempt.CompletedDate));
            Field(M(AdoMessage.ReportId), attempt.SubResultId); Field(L("RunBy"), Identity(attempt.RunBy));
            Field(L("FailureType"), attempt.FailureType); Field(L("Resolution"), attempt.ResolutionState);
            if (attempt.FailingSinceBuildId is > 0) FieldLink(L("FailingSince"), AdoWebLinks.Build(Collection, Project, attempt.FailingSinceBuildId.Value), attempt.FailingSinceBuildId.Value.ToString(Culture));
            foreach (int bug in attempt.AssociatedBugIds.Where(id => id > 0).Distinct()) FieldLink(L("Bugs"), AdoWebLinks.WorkItem(Collection, Project, bug), "#" + bug.ToString(Culture));
            Field(L("Comment"), attempt.Comment); W("</dl>\n");
            // Text repeated from an earlier attempt of the same failure is referenced, not repeated.
            int? sameError = attempt.ErrorMessage is { Length: > 0 } error && errors.TryGetValue(error, out int e) ? e : null;
            int? sameStack = attempt.StackTrace is { Length: > 0 } stack && stacks.TryGetValue(stack, out int s) ? s : null;
            if (sameError is int both && sameStack == both) SameAs(failure, "SameBoth", both);
            else
            {
                if (sameError is int earlier) SameAs(failure, "SameError", earlier); else Code(attempt.ErrorMessage, CodeLanguage.ErrorMessage);
                if (sameStack is int previous) SameAs(failure, "SameStack", previous); else Code(attempt.StackTrace, CodeLanguage.StackTrace);
            }
            if (!string.IsNullOrEmpty(attempt.ErrorMessage)) errors.TryAdd(attempt.ErrorMessage, attempt.Number);
            if (!string.IsNullOrEmpty(attempt.StackTrace)) stacks.TryAdd(attempt.StackTrace, attempt.Number);
            if (attempt.SubResults.Count > 0)
            {
                Heading(3, L("SubResults"));
                foreach (AdoTestSubResult sub in attempt.SubResults) SubResult(sub);
            }
            if (attempt.Iterations.Count > 0)
            {
                Heading(3, L("Iterations"));
                foreach (AdoTestIteration iteration in attempt.Iterations) Iteration(iteration);
            }
            AttachmentList(failure, attempt);
            Fields(L("CustomFields"), attempt.CustomFields); Fields(L("AdditionalFields"), attempt.AdditionalFields);
            W("</details>\n");
        }

        // Runs of one group share their pipeline names, so they show once per group, not per attempt.
        private void PipelineNames(IReadOnlyList<AdoTestAttempt> attempts)
        {
            AdoTestRun? run = attempts.Select(a => model.Runs.FirstOrDefault(r => r.Id == a.RunId)).FirstOrDefault(r => r is not null);
            if (run is null || (PipelineName(run.StageName) ?? PipelineName(run.PhaseName) ?? PipelineName(run.JobName)) is null) return;
            W("<dl class=\"metadata-grid pipeline-names\">"); Field(L("Stage"), PipelineName(run.StageName)); Field(L("Job"), PipelineName(run.PhaseName));
            Field(L("Instance"), PipelineName(run.JobName)); W("</dl>\n");
        }

        // The label holds {0} where the attempt link goes, so word order follows the culture.
        private void SameAs(AdoTestFailure failure, string key, int number)
        {
            string template = L(key);
            int slot = template.IndexOf("{0}", StringComparison.Ordinal);
            W("<p class=\"same-as\">"); T(template[..slot]);
            W("<a href=\"#" + Anchor(failure) + "-a" + N(number) + "\">"); T(number.ToString(Culture)); W("</a>");
            T(template[(slot + 3)..]); W("</p>\n");
        }

        private void SubResult(AdoTestSubResult sub, int headingLevel = 4)
        {
            W("<section class=\"sub-result\" data-sub-result=\"" + N(sub.Id) + "\">"); Heading(headingLevel, sub.DisplayName ?? sub.Id.ToString(Culture));
            Outcome(sub.Outcome, sub.OutcomeClass);
            W("<dl class=\"metadata-grid\">"); Field(M(AdoMessage.ReportId), sub.Id); Field(L("Sequence"), sub.SequenceId); Field(L("GroupType"), sub.ResultGroupType);
            Field(L("Started"), Date(sub.StartedDate)); Field(L("Finished"), Date(sub.CompletedDate)); Field(L("Duration"), Duration(sub.Duration));
            Field(L("Machine"), sub.ComputerName); Field(L("Comment"), sub.Comment); W("</dl>\n");
            Code(sub.ErrorMessage, CodeLanguage.ErrorMessage); Code(sub.StackTrace, CodeLanguage.StackTrace);
            foreach (AdoTestSubResult child in sub.SubResults) SubResult(child, Math.Min(6, headingLevel + 1));
            W("</section>\n");
        }

        private void Iteration(AdoTestIteration iteration)
        {
            W("<section class=\"iteration\" data-iteration=\"" + N(iteration.Id) + "\"><h4>"); T(L("Iterations") + " " + iteration.Id.ToString(Culture)); W("</h4>");
            W("<dl class=\"metadata-grid\">"); Field(L("Outcome"), iteration.Outcome); W("</dl>"); Code(iteration.ErrorMessage, CodeLanguage.ErrorMessage);
            if (iteration.Parameters.Count > 0)
            {
                Heading(5, M(AdoMessage.ReportParameters)); W("<dl class=\"metadata-grid\">");
                foreach (AdoTestIterationParameter parameter in iteration.Parameters) Field(parameter.Name, parameter.Value);
                W("</dl>\n");
            }
            foreach (AdoTestActionResult action in iteration.ActionResults)
            {
                W("<div class=\"action-result\"><dl class=\"metadata-grid\">");
                Field(L("ActionPath"), action.ActionPath); Field(L("StepIdentifier"), action.StepIdentifier); Field(L("Outcome"), action.Outcome);
                W("</dl>"); Code(action.ErrorMessage, CodeLanguage.ErrorMessage); W("</div>\n");
            }
            W("</section>\n");
        }

        private void AttachmentList(AdoTestFailure failure, AdoTestAttempt attempt)
        {
            if (attempt.Attachments.Count == 0) return;
            Heading(3, L("Attachments")); W("<ul class=\"attachment-list\">\n");
            foreach (AdoTestAttachment attachment in attempt.Attachments)
            {
                W("<li id=\"" + Anchor(failure, attempt) + "-att" + N(attachment.Id) + "\"");
                if (attachment.DownloadStatus != AdoTestAttachmentStatus.NotRequested)
                    W(" data-download-status=\"" + attachment.DownloadStatus.ToString().ToLowerInvariant() + "\"");
                W(">");
                // Browser navigation uses Windows authentication to download the original, for every file type.
                Link(AdoWebLinks.TestResultAttachment(Collection, Project, attachment.RunId, attachment.ResultId, attachment.Id, attachment.SubResultId), attachment.FileName);
                if (attachment.Size.HasValue) { W(" <span class=\"attachment-size\">"); T(F(AdoMessage.TestReportBytes, attachment.Size.Value)); W("</span>"); }
                // GeneralAttachment is the default type of nearly every attachment, so only other types show.
                string? type = string.Equals(attachment.AttachmentType, "GeneralAttachment", StringComparison.OrdinalIgnoreCase) ? null : attachment.AttachmentType;
                if (attachment.Comment is not null || type is not null || attachment.SubResultId is not null)
                {
                    W("<dl class=\"metadata-grid\">"); Field(L("Comment"), attachment.Comment); Field(L("AttachmentType"), type);
                    Field(M(AdoMessage.ReportId), attachment.SubResultId); W("</dl>");
                }
                LocalAttachment(attachment);
                W("</li>\n");
            }
            W("</ul>\n");
        }

        // Writes the local link, status notes and a collapsed preview of verified JSON or text. Local
        // names and hrefs are toolkit-generated; the remote name appears only as encoded text.
        private void LocalAttachment(AdoTestAttachment attachment)
        {
            (string Href, string Name, long Length)? file = LocalFile(attachment);
            string? note = attachment.DownloadStatus switch
            {
                AdoTestAttachmentStatus.TooLarge => L("NotDownloadedTooLarge"),
                AdoTestAttachmentStatus.BudgetExceeded => L("NotDownloadedBudget"),
                AdoTestAttachmentStatus.Failed => L("DownloadFailed"),
                AdoTestAttachmentStatus.ContentMismatch => L("ContentMismatch"),
                _ => null,
            };
            if (file is null && note is null) return;
            bool preview = file is not null && attachment.DownloadStatus == AdoTestAttachmentStatus.Downloaded
                && attachment.Kind is AdoTestAttachmentKind.Json or AdoTestAttachmentKind.Text;
            TestFailureLocalAttachments? limits = model.LocalAttachments;
            if (preview && file!.Value.Length > limits!.MaximumInlineJsonBytes) { note = L("NotInlinedSize"); preview = false; }
            else if (preview && inlined + file!.Value.Length > limits!.MaximumInlineTotalBytes) { note = L("NotInlinedBudget"); preview = false; }
            W("<div class=\"attachment-local\">");
            if (file is { } local)
            {
                W("<a class=\"local-file\" data-local-file href=\""); T(local.Href); W("\">"); T(L("LocalCopy"));
                W("</a> <code>"); T(local.Name); W("</code> <span>"); T(F(AdoMessage.TestReportBytes, local.Length)); W("</span>");
            }
            if (note is not null) { W("<p class=\"attachment-status\">"); T(note); W("</p>"); }
            W("</div>");
            if (!preview || file is not { } source) return;
            inlined += source.Length;
            // Display only: replacement decoding with byte order mark detection, so UTF-16 logs read too.
            string text;
            using (StreamReader reader = new(Path.Combine(limits!.SourceFolder, source.Name), new UTF8Encoding(false, false), detectEncodingFromByteOrderMarks: true))
                text = reader.ReadToEnd();
            W("<details class=\"attachment-preview\"><summary>"); T(L("Preview")); W("</summary>");
            if (attachment.Kind == AdoTestAttachmentKind.Json)
            {
                // A failed format falls back to the lexer's plain tokens.
                JsonLexer.TryFormat(text, (int)Math.Min(int.MaxValue, limits.MaximumInlineJsonBytes), out string formatted);
                Code(formatted, CodeLanguage.Json);
            }
            else Code(text, CodeLanguage.Text);
            W("</details>");
        }

        private (string Href, string Name, long Length)? LocalFile(AdoTestAttachment attachment)
        {
            if (attachment.DownloadStatus is not (AdoTestAttachmentStatus.Downloaded or AdoTestAttachmentStatus.ContentMismatch)
                || attachment.LocalRelativePath is not { } path || model.LocalAttachments is not { } local) return null;
            int slash = path.LastIndexOf('/');
            string name = path[(slash + 1)..];
            if (slash < 0 || !string.Equals(path[..slash], local.FolderName, StringComparison.Ordinal)
                || !local.Files.TryGetValue(name, out long length)) return null;
            return (Uri.EscapeDataString(local.FolderName) + "/" + Uri.EscapeDataString(name), name, length);
        }

        private void Code(string? text, CodeLanguage language)
        {
            if (string.IsNullOrEmpty(text)) return;
            W("<div class=\"code-section\"><div class=\"code-tools interactive\" data-enhance hidden>");
            Button("copy", L("Copy"), description: L(language switch
            {
                CodeLanguage.StackTrace => "StackTrace", CodeLanguage.Json => "Json", CodeLanguage.Text => "Text", _ => "ErrorMessage",
            }));
            Button("wrap", L("Wrap"), true);
            if (language == CodeLanguage.StackTrace) Button("framework", L("Framework"), true);
            W("</div>"); HighlightedCodeWriter.Write(writer, text, language, Culture); W("</div>\n");
        }

        // Every bug of the test with its title and state; a bug that could not be read shows its ID link only.
        private void BugList(IReadOnlyList<AdoTestBug> bugs)
        {
            if (!bugs.Any(b => b.Id > 0)) return;
            W("<div class=\"card-bugs\"><span class=\"strip-label\">"); T(L("BugList")); W("</span><ul class=\"bug-list\">");
            foreach (AdoTestBug bug in bugs.Where(b => b.Id > 0))
            {
                W("<li data-bug=\"" + N(bug.Id) + "\"" + (bug.IsOpen == true ? " data-open-bug" : "") + ">");
                Link(AdoWebLinks.WorkItem(Collection, bug.TeamProject ?? Project, bug.Id), "#" + bug.Id.ToString(Culture));
                if (bug.Title is not null) { W(" <span class=\"bug-title\">"); T(bug.Title); W("</span>"); }
                if (bug.State is not null) { W(" <span class=\"bug-state\">"); T(bug.State); W("</span>"); }
                if (bug.IsOpen == true) { W(" <span class=\"bug-open\">"); T(L("Open")); W("</span>"); }
                W("</li>");
            }
            W("</ul></div>\n");
        }

        private void TestCaseHeading(AdoTestCaseLink? testCase)
        {
            if (testCase is not { Id: > 0 }) return;
            W("<span class=\"test-case-link\">");
            Link(AdoWebLinks.WorkItem(Collection, Project, testCase.Id), L("TestCase") + " #" + testCase.Id.ToString(Culture));
            if (testCase.IsResolved && testCase.Title is not null) { W(" <span class=\"test-case-title\">"); T(testCase.Title); W("</span>"); }
            if (testCase.IsResolved && testCase.State is not null) { W(" <span class=\"test-case-state\">"); T(testCase.State); W("</span>"); }
            W("</span>");
        }

        private void Runs()
        {
            W("<section class=\"view\" id=\"runs\" data-view>\n"); Heading(2, L("RunsAndHistory"));
            W("<p class=\"window-note\">"); T(F(AdoMessage.TestReportWindowNote, Date(model.AttachmentWindowStart)!, model.OmittedAttachmentCount)); W("</p>\n");
            // Runs of one group sit together, in attempt order; the name columns already say the group.
            IReadOnlyList<AdoTestRun> runs = [.. AttemptGrouper.OrderRuns(model.Runs).Select((run, order) => (Run: run, Order: order))
                .OrderBy(item => Grouping.GroupOf(item.Run.Id) ?? int.MaxValue).ThenBy(item => item.Order).Select(item => item.Run)];
            if (runs.Count > 0)
            {
                bool stage = runs.Any(r => PipelineName(r.StageName) is not null), job = runs.Any(r => PipelineName(r.PhaseName) is not null),
                    instance = runs.Any(r => PipelineName(r.JobName) is not null);
                HashSet<int> downloaded = [.. model.Failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments)
                    .Where(a => LocalFile(a) is not null).Select(a => a.RunId)];
                W("<div class=\"table-scroll\"><table class=\"runs-table\"><thead><tr><th scope=\"col\">"); T(L("Run")); W("</th>");
                if (stage) { W("<th scope=\"col\">"); T(L("Stage")); W("</th>"); }
                if (job) { W("<th scope=\"col\">"); T(L("Job")); W("</th>"); }
                if (instance) { W("<th scope=\"col\">"); T(L("Instance")); W("</th>"); }
                foreach (string key in new[] { "AttemptNumbers", "Started", "State", "Tests", "Passed", "Attachments" }) { W("<th scope=\"col\">"); T(L(key)); W("</th>"); }
                W("</tr></thead>\n<tbody>\n");
                foreach (AdoTestRun run in runs)
                {
                    W("<tr data-run=\"" + N(run.Id) + "\"><td>"); Link(AdoWebLinks.TestRun(Collection, Project, run.Id), run.Name.Length > 0 ? run.Name : run.Id.ToString(Culture));
                    W(" <span class=\"text-muted\">"); T(run.Id.ToString(Culture)); W("</span></td>");
                    if (stage) Cell(PipelineName(run.StageName));
                    if (job) Cell(PipelineName(run.PhaseName));
                    if (instance) Cell(PipelineName(run.JobName));
                    Cell(string.Join(" / ", new[] { run.StageAttempt, run.PhaseAttempt, run.PipelineAttempt }.Where(v => v.HasValue).Select(v => v!.Value.ToString(Culture))));
                    Cell(Date(run.StartedDate)); Cell(run.State); Cell(run.TotalTests?.ToString(Culture)); Cell(run.PassedTests?.ToString(Culture));
                    Cell(downloaded.Contains(run.Id) ? L("Downloaded") : model.AttachmentRunIds.Contains(run.Id) ? L("Listed") : L("OutsideWindow"));
                    W("</tr>\n");
                }
                W("</tbody></table></div>\n");
            }
            W("<section class=\"history-panel\" id=\"history\">\n"); Heading(3, L("History"));
            RunHistoryChart.Write(writer, model.History, Collection, Project, Culture, model.GeneratedAt.Offset); W("\n</section>\n");
            W("<dl class=\"secondary-line\">");
            Field(M(AdoMessage.ReportGeneratedAt), Date(model.GeneratedAt)); Field(M(AdoMessage.ReportToolkitVersion), model.ToolkitVersion);
            Field(M(AdoMessage.ReportServer), Collection.GetLeftPart(UriPartial.Authority)); Field(M(AdoMessage.ReportCollection), Collection.AbsolutePath);
            Field(M(AdoMessage.ReportProject), Project); W("</dl>\n</section>\n");
        }

        private void Cell(string? value) { W("<td>"); T(value); W("</td>"); }

        private void Diagnostics()
        {
            if (!HasDiagnostics) return;
            W("<section class=\"view\" id=\"diagnostics\" data-view>\n"); Heading(2, M(AdoMessage.ReportDiagnostics));
            foreach (AdoDiagnostic diagnostic in model.Diagnostics)
            {
                W("<div class=\"diagnostic\" data-severity=\""); T(diagnostic.Severity.ToString().ToLowerInvariant()); W("\" data-diagnostic=\""); T(diagnostic.Code); W("\"><strong>");
                T(L(diagnostic.Severity.ToString())); W(" · "); T(diagnostic.Code); W("</strong><p>"); T(diagnostic.Message); W("</p></div>\n");
            }
            W("</section>\n");
        }

        private void Fields(string label, IReadOnlyDictionary<string, object?> fields)
        {
            if (fields.Count == 0) return;
            Heading(3, label); W("<dl class=\"metadata-grid\">");
            foreach ((string key, object? value) in fields.OrderBy(pair => pair.Key, StringComparer.Ordinal)) Field(key, value);
            W("</dl>\n");
        }

        private void Field(string label, object? value)
        {
            if (value is null) return;
            W("<div><dt>"); T(label); W("</dt><dd>");
            T(value is IFormattable formatted ? formatted.ToString(null, Culture) : value.ToString()); W("</dd></div>");
        }

        private void FieldLink(string label, Uri uri, string text)
        { W("<div><dt>"); T(label); W("</dt><dd>"); Link(uri, text); W("</dd></div>"); }

        private void Link(Uri uri, string text)
        { W("<a rel=\"noreferrer\" href=\""); T(uri.AbsoluteUri); W("\">"); T(text); StatusPresentation.ExternalGlyph(writer, Culture); W("</a>"); }

        private Uri Result(int run, int result) => AdoWebLinks.BuildTestResult(Collection, Project, model.Build.Id, run, result);
        private static string? Identity(AdoIdentityRef? identity) => identity is null ? null : identity.DisplayName
            + (identity.UniqueName is null ? "" : " <" + identity.UniqueName + ">");

        // The server's outcome shows only when it adds to the badge, such as Timeout or Aborted.
        private void Outcome(string outcome, AdoTestOutcomeClass outcomeClass)
        {
            AdoTestHistoryOutcome status = Status(outcomeClass);
            StatusPresentation.Write(writer, status, Culture);
            string? plain = outcomeClass switch { AdoTestOutcomeClass.Pass => "Passed", AdoTestOutcomeClass.Failure => "Failed", _ => null };
            if (!string.Equals(outcome, plain, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(outcome, StatusPresentation.Label(status, Culture), StringComparison.Ordinal))
            { W(" <span class=\"attempt-outcome\">"); T(outcome); W("</span>"); }
        }

        private void Button(string action, string label, bool toggle = false, string? description = null)
        {
            W("<button type=\"button\" class=\"interactive\" data-enhance hidden data-action=\"" + action + "\"");
            if (toggle) W(" aria-pressed=\"false\"");
            if (description is not null) { W(" aria-label=\""); T(label + " — " + description); W("\""); }
            W(">");
            if (toggle) W("<span class=\"toggle-off\" aria-hidden=\"true\">○</span><span class=\"toggle-on\" aria-hidden=\"true\">✓</span>");
            T(label); W("</button>");
        }

        private void Toggle(string name, string label, bool enabled)
        { W("<label><input type=\"checkbox\" data-toggle=\"" + name + "\"" + (enabled ? " checked" : "") + ">"); T(label); W("</label>\n"); }
    }
}
