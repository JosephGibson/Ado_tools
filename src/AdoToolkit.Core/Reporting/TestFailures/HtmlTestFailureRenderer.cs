using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.Reporting.Highlighting;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// One scanned page per build: views for an overview with its table, runs with history, compact
// failure cards, failures grouped by error, and failures grouped by bug. Every section renders
// without scripts; the script switches views, filters, navigates, expands, wraps and copies. Every
// <details> starts closed.
public static class HtmlTestFailureRenderer
{
    private const int MaximumSummaryCharacters = 240;
    private const string BranchPrefix = "refs/heads/";

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

    // The error line of the last failed attempt, else of any attempt. Public for the console table
    // of AdoTestFailure, which shows the line that the report's tables show.
    public static string? LatestError(AdoTestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return FirstLine(LatestErrorAttempt(failure)?.ErrorMessage);
    }

    // The attempt that LatestError reads: the last failed attempt with an error line, else the last
    // attempt with one.
    internal static AdoTestAttempt? LatestErrorAttempt(AdoTestFailure failure) =>
        failure.Attempts.LastOrDefault(a => a.OutcomeClass == AdoTestOutcomeClass.Failure && FirstLine(a.ErrorMessage) is not null)
        ?? failure.Attempts.LastOrDefault(a => FirstLine(a.ErrorMessage) is not null);

    // The text of one metadata value in the report culture. A custom field keeps the JSON shape
    // the server sent (§15.11), so an array or object shows as compact JSON, never as a type name.
    internal static string? FieldText(object value, CultureInfo culture) => value switch
    {
        string text => text,
        IFormattable formatted => formatted.ToString(null, culture),
        IReadOnlyDictionary<string, object?> or System.Collections.IEnumerable => FieldJson(value),
        _ => value.ToString(),
    };

    private static string FieldJson(object value)
    {
        using MemoryStream buffer = new();
        // The text is encoded again at the HTML sink, so JSON escaping stays minimal and readable.
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteJson(json, value);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static void WriteJson(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null: json.WriteNullValue(); break;
            case string text: json.WriteStringValue(text); break;
            case bool flag: json.WriteBooleanValue(flag); break;
            case long integer: json.WriteNumberValue(integer); break;
            case double number when double.IsFinite(number): json.WriteNumberValue(number); break;
            case AdoIdentityRef identity:
                json.WriteStartObject();
                json.WriteString("displayName", identity.DisplayName);
                if (identity.UniqueName is not null) json.WriteString("uniqueName", identity.UniqueName);
                if (identity.Id is not null) json.WriteString("id", identity.Id);
                json.WriteEndObject();
                break;
            case IReadOnlyDictionary<string, object?> map:
                json.WriteStartObject();
                foreach ((string name, object? item) in map) { json.WritePropertyName(name); WriteJson(json, item); }
                json.WriteEndObject();
                break;
            case System.Collections.IEnumerable items:
                json.WriteStartArray();
                foreach (object? item in items) WriteJson(json, item);
                json.WriteEndArray();
                break;
            default: json.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
        }
    }

    // Errors that differ only in URLs, GUIDs, paths, hexadecimal IDs or numbers fall into one group.
    internal static string ErrorKey(string line) => ErrorClusters.Key(line);

    private sealed record BugEntry(AdoTestBug Bug, IReadOnlyList<(AdoTestFailure Failure, AdoTestBug Bug)> Tests);

    // The optional columns of a table of tests; number, test, class, trend, Test Case and the group
    // columns are always there.
    [Flags]
    private enum Columns { None = 0, Bugs = 1, Error = 2, Values = 4, Source = 8 }

    private sealed class Document(TestFailureReportModel model, TextWriter writer)
    {
        private const int GlanceItems = 3;
        private const int SampleCharacters = 1000, SampleLines = 12;
        // Characters beyond which a metadata value takes a whole row.
        private const int WideField = 48;
        private HashSet<string>? previews;
        private IReadOnlyList<BugEntry>? bugEntries;
        private IReadOnlyList<ErrorClusters.Cluster>? clusters;
        private Dictionary<int, ErrorClusters.Cluster>? clusterOf;
        private readonly Dictionary<int, TestFailureSignal?> signals = [];
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
        private static string Anchor(AdoTestFailure failure, AdoTestAttempt attempt, AdoTestAttachment attachment) =>
            Anchor(failure, attempt) + "-att" + N(attachment.Id);
        private string? Duration(TimeSpan? value) => value.HasValue ? F(AdoMessage.TestReportSeconds, value.Value.TotalSeconds) : null;
        // Server times are UTC; every time shows in the export's offset, like the generation time.
        private string? Date(DateTimeOffset? value) =>
            value is { } time ? ReportTime.InOffset(time, model.GeneratedAt.Offset).ToString("g", Culture) : null;
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
            // The views in the order of their tabs, so print and a page with scripts blocked read the same.
            Overview();
            Runs();
            Details();
            ByError();
            Bugs();
            Diagnostics();
            W("</main>\n");
            Footer();
            W("<p class=\"copy-feedback\" role=\"status\" data-copy-status hidden data-label-done=\""); T(L("Copied")); W("\" data-label-selected=\""); T(L("SelectCopy")); W("\"></p>\n");
            W("<script>"); W(script); W("</script>\n</body>\n</html>\n");
        }

        private bool HasDiagnostics => model.Diagnostics.Count > 0 || model.Status == AdoTestFailureStatus.Partial;

        private void Header()
        {
            W("<a class=\"skip-link\" href=\"#overview\">"); T(L("Overview")); W("</a>\n<header class=\"top-bar\"><div class=\"top-bar-inner\">\n");
            // The counts lead the band. With scripts on, a count that can filter the tests is a shortcut to its filter.
            W("<div class=\"title-line\">");
            Chip("failed", "✕ " + L("Failed"), model.FailedCount, null);
            Chip("flaky", "≈ " + L("Flaky"), model.FlakyCount, model.FlakyExcluded ? L("NotShown") : null);
            // Attachments that were not listed have no count: 0 would say that the tests have none.
            if (model.AttachmentsListed) Chip("attachments", L("Attachments"), model.Failures.Sum(Attachments), null);
            else Chip("attachments", L("Attachments"), null, L("NotListed"));
            W("<span class=\"report-brand\">"); T(M(AdoMessage.ReportBrand)); W("</span><h1>"); T(L("Heading")); W("</h1>");
            W("<span class=\"pipeline-name\">"); Link(model.DefinitionUrl, model.Build.Definition.Name); W("</span><span class=\"build-number\">");
            Link(model.BuildUrl, model.Build.BuildNumber); W("</span>");
            // To keep the band short, the branch shows by its name and the finish time without its
            // label; the full ref and the label are their titles.
            if (model.Build.SourceBranch is { } branch)
            {
                if (branch.Length > BranchPrefix.Length && branch.StartsWith(BranchPrefix, StringComparison.Ordinal))
                { W("<code title=\""); T(branch); W("\">"); T(branch[BranchPrefix.Length..]); W("</code>"); }
                else { W("<code>"); T(branch); W("</code>"); }
            }
            if (model.Build.SourceVersion is { } version)
            {
                W("<span class=\"commit\" title=\""); T(L("Commit")); W("\">");
                if (model.CommitUrl is not null) Link(model.CommitUrl, version[..7]); else T(version);
                W("</span>");
            }
            if (model.Build.Result is { } result)
            { W("<span class=\"build-result" + (ResultStatus(result) is { } status ? " status-" + status : "") + "\">"); T(result); W("</span>"); }
            if (model.Build.FinishTime is not null) { W("<span title=\""); T(L("Finished")); W("\">"); T(Date(model.Build.FinishTime)); W("</span>"); }
            W("<span class=\"results-link\">"); Link(model.ResultsUrl, L("Result")); W("</span>");
            if (model.Status == AdoTestFailureStatus.Partial)
            { W("<a class=\"partial-link\" href=\"#diagnostics\"><span aria-hidden=\"true\">!</span> "); T(M(AdoMessage.ReportPartial)); W("</a>"); }
            // The views and the filters share the second line of the band.
            W("</div>\n<div class=\"nav-line\"><nav class=\"view-links\" aria-label=\""); T(M(AdoMessage.ReportNavigation)); W("\">");
            foreach ((string id, string label) in Views())
            { W("<a href=\"#" + id + "\" data-view-link=\"" + id + "\">"); T(label); W("</a>"); }
            W("</nav>\n");
            Filters();
            W("</div>\n</div></header>\n");
        }

        // The colour of the build result. The class comes from this list, never from the server's
        // text; a value that is not listed has no colour.
        private static string? ResultStatus(string result) =>
            string.Equals(result, "succeeded", StringComparison.OrdinalIgnoreCase) ? "passed"
            : string.Equals(result, "partiallySucceeded", StringComparison.OrdinalIgnoreCase) ? "flaky"
            : string.Equals(result, "failed", StringComparison.OrdinalIgnoreCase) ? "failed"
            : string.Equals(result, "canceled", StringComparison.OrdinalIgnoreCase) ? "other" : null;

        private IEnumerable<(string Id, string Label)> Views()
        {
            yield return ("overview", L("Overview"));
            yield return ("runs", L("RunsAndHistory"));
            yield return ("details", L("Details"));
            yield return ("by-error", L("ByError"));
            yield return ("bugs", L("OpenBugs") + " " + BugEntries.Count.ToString(Culture));
            if (HasDiagnostics) yield return ("diagnostics", M(AdoMessage.ReportDiagnostics) + " " + model.Diagnostics.Count.ToString(Culture));
        }

        // A chip without a count shows its note only; a count of zero is muted.
        private void Chip(string css, string label, int? count, string? note)
        {
            W("<span class=\"count-chip status-" + css + "\"" + (count == 0 ? " data-zero" : "") + "><span class=\"count-label\">"); T(label); W("</span>");
            if (count is int value) { W("<strong class=\"count-value\">"); T(value.ToString(Culture)); W("</strong>"); }
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
            // Without attachment lists this filter could only hide every test.
            if (model.AttachmentsListed) Toggle("attachments", L("HasAttachments"), false);
            // Only meaningful when some test is tracked; it leaves the tests that still need a bug.
            if (model.Failures.Any(static f => f.HasOpenBug)) Toggle("untracked", L("WithoutOpenBug"), false);
            // They act on the cards only, so the script shows them in Details alone.
            Button("expand", L("ExpandAll"), detailsOnly: true); Button("collapse", L("CollapseAll"), detailsOnly: true);
            W("<span class=\"filter-result-count\" role=\"status\" aria-live=\"polite\" data-filter-count data-label-count=\""); T(L("FilterCount")); W("\"></span>");
            W("</div>\n");
        }

        // The table of tests first, then the build at a glance in one row of cards.
        private void Overview()
        {
            W("<section class=\"view\" id=\"overview\" data-view>\n"); Heading(2, L("Overview"));
            if (model.Failures.Count == 0) { W("<p>"); T(L("NoFailures")); W("</p>\n</section>\n"); return; }
            W("<p data-no-matches hidden>"); T(L("NoMatches")); W("</p>\n");
            W("<div class=\"table-scroll\"><table class=\"failure-table\">\n"); TableHead(Columns.Bugs | Columns.Error);
            W("<tbody>\n");
            foreach (AdoTestFailure failure in model.Failures) Row(failure, Columns.Bugs | Columns.Error, strip: true);
            W("</tbody></table></div>\n");
            Glance();
            W("</section>\n");
        }

        // The build at a glance: new or recurring, the largest errors, the tests that no open bug
        // tracks, and the groups. Each card is a short list, left out when it would be empty; the
        // cards describe the build, the filters act on the table.
        private void Glance()
        {
            IReadOnlyList<AdoTestFailure> failures = model.Failures;
            int fresh = failures.Count(failure => Signal(failure) is { IsNew: true });
            int recurring = failures.Count(failure => Signal(failure) is { IsNew: false });
            ErrorClusters.Cluster[] common = [.. Clusters.Where(static cluster => cluster.Key is not null && cluster.Members.Count > 1).Take(GlanceItems)];
            bool tracked = failures.Any(static failure => failure.HasOpenBug);
            if (fresh + recurring == 0 && common.Length == 0 && !tracked && !Grouping.IsGrouped) return;
            W("<div class=\"glance\">\n");
            if (fresh + recurring > 0)
            {
                int none = failures.Count - fresh - recurring;
                GlancePanel(L("NewAndRecurring"));
                W("<ul class=\"glance-counts\"><li>"); NewChip(L("NewTests")); W(" <strong>" + N(fresh) + "</strong></li><li><span class=\"trend trend-since\">");
                T(L("RecurringTests")); W("</span> <strong>" + N(recurring) + "</strong></li>");
                if (none > 0) { W("<li><span class=\"trend trend-none\">"); T(L("NoComparison")); W("</span> <strong>" + N(none) + "</strong></li>"); }
                W("</ul>");
                // The shares as one bar; the counts above carry the same numbers.
                W("<div class=\"glance-bar\" aria-hidden=\"true\">");
                foreach ((string css, int count) in new[] { ("bar-new", fresh), ("bar-recurring", recurring), ("bar-none", none) })
                    if (count > 0) W("<span class=\"" + css + "\" style=\"--share: " + Share(count, failures.Count) + "\"></span>");
                W("</div></div>\n");
            }
            if (common.Length > 0)
            {
                GlancePanel(L("CommonErrors")); W("<ol class=\"glance-list\">");
                foreach (ErrorClusters.Cluster cluster in common)
                {
                    W("<li><span class=\"glance-count\">" + N(cluster.Members.Count) + "</span> <a class=\"glance-error\" href=\"#e-" + N(cluster.Number) + "\">");
                    if (cluster.ExceptionType is { } type) { W("<code class=\"exception-type\">"); T(ErrorClusters.ShortType(type)); W("</code> "); }
                    W("<span class=\"glance-line\">"); T(PlainLine(cluster)); W("</span></a></li>");
                }
                W("</ol></div>\n");
            }
            if (tracked)
            {
                // The tests that most need a bug first: the longest failing, then the new ones.
                AdoTestFailure[] untracked = [.. failures.Where(static failure => !failure.HasOpenBug)
                    .OrderByDescending(failure => Signal(failure) is { IsNew: false } signal ? signal.Streak : 0)
                    .ThenByDescending(failure => Signal(failure) is { IsNew: true }).ThenBy(static failure => failure.Ordinal)];
                GlancePanel(L("WithoutOpenBug"));
                W("<p class=\"glance-note\">"); T(F(AdoMessage.TestReportFilterCount, untracked.Length.ToString(Culture), failures.Count.ToString(Culture))); W("</p>");
                if (untracked.Length > 0)
                {
                    W("<ol class=\"glance-list\">");
                    foreach (AdoTestFailure failure in untracked.Take(GlanceItems)) { W("<li>"); GlanceName(failure); W(" "); Trend(failure); W("</li>"); }
                    W("</ol><p class=\"glance-more\"><a href=\"#no-bug\">"); T(L("AllWithoutOpenBug")); W("</a></p>");
                }
                W("</div>\n");
            }
            if (Grouping.IsGrouped) GroupPanel();
            W("</div>\n");
        }

        private void GlancePanel(string heading) { W("<div class=\"glance-panel\">"); Heading(3, heading); }

        private void GlanceName(AdoTestFailure failure)
        { W("<a class=\"glance-name\" href=\"#" + Anchor(failure) + "\" title=\""); T(failure.ShortName); W("\">"); T(failure.ShortName); W("</a>"); }

        // Per group, the tests that ended failed or flaky in it, and those that failed in it alone,
        // as the Failing in filter counts them.
        private void GroupPanel()
        {
            GlancePanel(L("ByGroup"));
            W("<table class=\"glance-table\"><thead><tr><td></td><th scope=\"col\" class=\"num status-failed\"><span aria-hidden=\"true\">✕</span> "); T(L("Failed"));
            W("</th><th scope=\"col\" class=\"num status-flaky\"><span aria-hidden=\"true\">≈</span> "); T(L("Flaky")); W("</th><th scope=\"col\" class=\"num\">"); T(L("OnlyHere"));
            W("</th></tr></thead><tbody>");
            for (int index = 0; index < Grouping.Labels.Count; index++)
            {
                int failed = 0, flaky = 0, only = 0;
                foreach (AdoTestFailure failure in model.Failures)
                {
                    var groups = Groups(failure);
                    AdoTestHistoryOutcome status = TestFailureGroups.Status(groups.FirstOrDefault(group => group.Group == index).Attempts ?? []);
                    if (status == AdoTestHistoryOutcome.Failed) failed++;
                    if (status == AdoTestHistoryOutcome.Flaky) flaky++;
                    if (status == AdoTestHistoryOutcome.Failed && groups.Count(group => group.Group.HasValue && TestFailureGroups.Status(group.Attempts) == AdoTestHistoryOutcome.Failed) == 1) only++;
                }
                W("<tr><th scope=\"row\">"); T(GroupLabel(index)); W("</th>");
                foreach (int count in new[] { failed, flaky, only }) W(count == 0 ? "<td class=\"num text-muted\">0</td>" : "<td class=\"num\">" + N(count) + "</td>");
                W("</tr>");
            }
            W("</tbody></table></div>\n");
        }

        // A share as a CSS percentage, in the invariant culture: a comma would make it invalid.
        private static string Share(int count, int total) => (100d * count / Math.Max(1, total)).ToString("0.##", CultureInfo.InvariantCulture) + "%";

        // The tests grouped by their latest error, largest group first. A group of two tests or more
        // also says what its tests have in common and shows a sample of the message.
        private void ByError()
        {
            W("<section class=\"view\" id=\"by-error\" data-view>\n"); Heading(2, L("ByError"));
            if (model.Failures.Count == 0) { W("<p>"); T(L("NoFailures")); W("</p>\n</section>\n"); return; }
            W("<p class=\"cluster-summary\">"); T(F(AdoMessage.TestReportLabelValue, L("DistinctErrors"), Clusters.Count(static cluster => cluster.Key is not null).ToString(Culture)));
            W(" · "); T(F(AdoMessage.TestReportLabelValue, L("Tests"), model.Failures.Count.ToString(Culture))); W("</p>\n");
            W("<p data-no-matches hidden>"); T(L("NoMatches")); W("</p>\n<div class=\"table-scroll\"><table class=\"failure-table by-error\">\n");
            Columns columns = Columns.Bugs | (Clusters.Any(static cluster => cluster.Varying.Count > 0) ? Columns.Values : Columns.None);
            TableHead(columns);
            int span = ColumnCount(columns);
            foreach (ErrorClusters.Cluster cluster in Clusters)
            {
                W("<tbody class=\"error-cluster\" id=\"e-" + N(cluster.Number) + "\"><tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"" + N(span)
                    + "\"><div class=\"cluster-head\"><span class=\"cluster-count\">");
                T(cluster.Members.Count.ToString(Culture)); W("</span>");
                if (cluster.ExceptionType is { } type) { W(" <code class=\"exception-type\" title=\""); T(type); W("\">"); T(ErrorClusters.ShortType(type)); W("</code>"); }
                if (cluster.Key is null) { W(" <span class=\"cluster-line no-message\">"); T(L("NoErrorMessage")); W("</span>"); }
                else { W(" <code class=\"cluster-line\">"); ClusterLine(cluster); W("</code>"); }
                W("</div></th></tr>\n");
                if (cluster.Members.Count > 1) ClusterFacts(cluster, span);
                foreach (ErrorClusters.Member member in cluster.Members)
                    Row(member.Failure, columns, strip: false, values: (columns & Columns.Values) != 0 ? cluster.ValuesOf(member) : null);
                W("</tbody>\n");
            }
            W("</table></div>\n</section>\n");
        }

        private IReadOnlyList<ErrorClusters.Cluster> Clusters => clusters ??= ErrorClusters.Of(model.Failures);

        private ErrorClusters.Cluster ClusterOf(AdoTestFailure failure) => (clusterOf ??= Clusters
            .SelectMany(static cluster => cluster.Members.Select(member => (member.Failure.Ordinal, Cluster: cluster)))
            .ToDictionary(static item => item.Ordinal, static item => item.Cluster))[failure.Ordinal];

        // The first test's key line, without a leading exception type when the cluster shows it, with
        // each value that differs between the tests marked and titled with the values. Cut at the
        // length of the other one-line summaries, never inside a value.
        private void ClusterLine(ErrorClusters.Cluster cluster)
        {
            ErrorClusters.Member first = cluster.Representative;
            int skip = first.PrefixType is not null && first.PrefixType == cluster.ExceptionType ? first.PrefixLength : 0;
            int left = MaximumSummaryCharacters;
            foreach (ErrorClusters.Part part in first.Parts)
            {
                string text = part.Text;
                if (skip >= text.Length) { skip -= text.Length; continue; }
                text = text[skip..];
                skip = 0;
                bool cut = text.Length > left, marked = part.Slot >= 0 && cluster.Varying.Contains(part.Slot);
                // A marked value that does not fit is left out whole.
                if (cut && marked) { W("…"); return; }
                if (cut) text = Shorten(text, left);
                if (marked) { W("<span class=\"error-var\" title=\""); T(Differs(cluster, part.Slot)); W("\">"); T(text); W("</span>"); }
                else T(text);
                if (cut) { W("…"); return; }
                left -= text.Length;
            }
        }

        // The same line as plain text, for the Overview.
        private static string PlainLine(ErrorClusters.Cluster cluster)
        {
            ErrorClusters.Member first = cluster.Representative;
            string line = string.Concat(first.Parts.Select(static part => part.Text));
            if (first.PrefixType is not null && first.PrefixType == cluster.ExceptionType) line = line[first.PrefixLength..];
            return line.Length > MaximumSummaryCharacters ? Shorten(line, MaximumSummaryCharacters) + "…" : line;
        }

        // The values of one slot across the cluster's tests: five at most, then how many more.
        private string Differs(ErrorClusters.Cluster cluster, int slot)
        {
            IReadOnlyList<string> values = cluster.Values(slot);
            string shown = string.Join(", ", values.Take(5).Select(static value => value.Length > 80 ? Shorten(value, 79) + "…" : value));
            if (values.Count > 5) shown += " (+" + (values.Count - 5).ToString(CultureInfo.InvariantCulture) + ")";
            return F(AdoMessage.TestReportLabelValue, L("Differs"), shown);
        }

        // What the tests of a cluster share: outcome, trend, bugs, groups and the frame where they
        // fail, then a sample of the first test's message.
        private void ClusterFacts(ErrorClusters.Cluster cluster, int span)
        {
            AdoTestFailure[] tests = [.. cluster.Members.Select(static member => member.Failure)];
            W("<tr class=\"cluster-facts\"><td colspan=\"" + N(span) + "\"><div class=\"facts\"><span class=\"fact-group\">");
            foreach ((AdoTestHistoryOutcome status, string label) in new[] { (AdoTestHistoryOutcome.Failed, L("Failed")), (AdoTestHistoryOutcome.Flaky, L("Flaky")) })
            {
                int count = tests.Count(test => Status(test) == status);
                if (count == 0) continue;
                W("<span class=\"fact status-" + StatusPresentation.Css(status) + "\"><span aria-hidden=\"true\">" + StatusPresentation.Glyph(status) + "</span> ");
                T(label); W(" <strong>" + N(count) + "</strong></span>");
            }
            W("</span>");
            int fresh = tests.Count(test => Signal(test) is { IsNew: true }), recurring = tests.Count(test => Signal(test) is { IsNew: false });
            if (fresh + recurring > 0)
            {
                W("<span class=\"fact-group\">");
                if (fresh > 0) { W("<span class=\"fact\">"); NewChip(L("NewTests")); W(" <strong>" + N(fresh) + "</strong></span>"); }
                if (recurring > 0) { W("<span class=\"fact\"><span class=\"trend trend-since\">"); T(L("RecurringTests")); W("</span> <strong>" + N(recurring) + "</strong></span>"); }
                W("</span>");
            }
            AdoTestBug[] open = [.. tests.SelectMany(static test => test.Bugs).Where(static bug => bug.Id > 0 && bug.IsOpen == true).DistinctBy(static bug => bug.Id).OrderBy(static bug => bug.Id)];
            int tracked = tests.Count(static test => test.HasOpenBug);
            W("<span class=\"fact-group\">");
            if (tracked > 0)
            {
                W("<span class=\"fact fact-tracked\">"); T(L("WithOpenBug")); W(" <strong>" + N(tracked) + "</strong>");
                foreach (AdoTestBug bug in open.Take(3)) { W(" "); BugChip(bug); }
                if (open.Length > 3) { W(" <span class=\"text-muted\">+" + N(open.Length - 3) + "</span>"); }
                W("</span>");
            }
            if (tracked < tests.Length) { W("<span class=\"fact fact-untracked\">"); T(L("WithoutOpenBug")); W(" <strong>" + N(tests.Length - tracked) + "</strong></span>"); }
            W("</span>");
            GroupFacts(tests);
            if (cluster.Frame is { } frame)
            {
                W("<span class=\"fact-group\"><span class=\"fact\">"); T(L("CommonFrame")); W(" <code title=\""); T(frame); W("\">"); T(ErrorClusters.ShortFrame(frame));
                W("</code> <strong>" + N(cluster.FrameCount) + "</strong></span></span>");
            }
            W("</div>");
            AdoTestFailure first = cluster.Representative.Failure;
            if (LatestErrorAttempt(first)?.ErrorMessage is { Length: > 0 } message)
            {
                W("<details class=\"cluster-sample\"><summary>"); T(F(AdoMessage.TestReportLabelValue, L("SampleMessage"), first.ShortName)); W("</summary>");
                Code(Sample(message), CodeLanguage.ErrorMessage); W("</details>");
            }
            W("</td></tr>\n");
        }

        // The start of a message: at most SampleLines lines and SampleCharacters characters, then a
        // line with an ellipsis. The test's card holds the whole message.
        private static string Sample(string message)
        {
            string text = SinkEncoding.NormalizeLines(message.Length > 4 * SampleCharacters ? Shorten(message, 4 * SampleCharacters) : message).TrimEnd();
            int end = 0, lines = 1;
            while (end < text.Length && end < SampleCharacters && !(text[end] == '\n' && ++lines > SampleLines)) end++;
            return end >= text.Length ? text : Shorten(text, end).TrimEnd() + "\n…";
        }

        // At most length characters, without splitting a surrogate pair.
        private static string Shorten(string text, int length) =>
            text.Length <= length ? text : text[..(length > 0 && char.IsHighSurrogate(text[length - 1]) ? length - 1 : length)];

        // One entry per bug, each with the tests it is linked to. A bug that was read comes before one
        // that was not; among read bugs the one with the most tests comes first. Ties follow the bug ID.
        private IReadOnlyList<BugEntry> BugEntries => bugEntries ??= [.. model.Failures
            .SelectMany(failure => failure.Bugs.Where(bug => bug.Id > 0).DistinctBy(bug => bug.Id).Select(bug => (Failure: failure, Bug: bug)))
            .GroupBy(item => item.Bug.Id)
            .Select(group => new BugEntry(group.First().Bug, [.. group]))
            .OrderBy(entry => entry.Bug.IsOpen is null ? 1 : 0)
            .ThenByDescending(entry => entry.Bug.IsOpen is null ? 0 : entry.Tests.Count)
            .ThenBy(entry => entry.Bug.Id)];

        // The tests clustered by bug; a test with several bugs has one row under each. Each bug then
        // says what it does not cover: tests of the same Test Case and tests with the same error that
        // are not linked to it. The tests that no open bug tracks come last.
        private void Bugs()
        {
            W("<section class=\"view\" id=\"bugs\" data-view>\n"); Heading(2, L("OpenBugs"));
            if (model.Failures.Count == 0) { W("<p>"); T(L("NoFailures")); W("</p>\n</section>\n"); return; }
            if (BugEntries.Count == 0) { W("<p>"); T(L("NoOpenBugs")); W("</p>\n</section>\n"); return; }
            W("<p data-no-matches hidden>"); T(L("NoMatches")); W("</p>\n<div class=\"table-scroll\"><table class=\"failure-table by-error by-bug\">\n");
            TableHead(Columns.Source);
            int span = ColumnCount(Columns.Source);
            foreach (BugEntry entry in BugEntries)
            {
                AdoTestBug bug = entry.Bug;
                AdoTestFailure[] related = RelatedByTestCase(entry);
                (int sameError, ErrorClusters.Cluster? cluster) = SameError(entry, related);
                W("<tbody class=\"error-cluster\" data-bug=\"" + N(bug.Id) + "\"><tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"" + N(span)
                    + "\"><div class=\"bug-head\"><span class=\"cluster-count\">");
                T(entry.Tests.Count.ToString(Culture)); W("</span> ");
                BugChip(bug);
                W(" <span class=\"bug-summary\">"); BugText(bug);
                if (bug.WorkItemType is { } type && !string.Equals(type, "Bug", StringComparison.OrdinalIgnoreCase)) { W(" <span class=\"bug-state\">"); T(type); W("</span>"); }
                // A bug can live in another project than the build.
                if (bug.TeamProject is { } project && !string.Equals(project, Project, StringComparison.OrdinalIgnoreCase))
                { W(" <span class=\"bug-state\">"); T(F(AdoMessage.TestReportLabelValue, M(AdoMessage.ReportProject), project)); W("</span>"); }
                // What the bug covers, on the same line: its tests, the tests it leaves out, and where its tests fail.
                W(" <span class=\"facts\"><span class=\"fact-group\"><span class=\"fact\">"); T(L("LinkedTests")); W(" <strong>" + N(entry.Tests.Count) + "</strong></span>");
                if (related.Length > 0) { W("<span class=\"fact fact-untracked\">"); T(L("SameTestCase")); W(" <strong>" + N(related.Length) + "</strong></span>"); }
                if (sameError > 0)
                { W("<a class=\"fact fact-untracked\" href=\"#e-" + N(cluster!.Number) + "\">"); T(L("SameError")); W(" <strong>" + N(sameError) + "</strong></a>"); }
                W("</span>"); GroupFacts(entry.Tests.Select(static item => item.Failure)); W("</span></span></div></th></tr>\n");
                foreach ((AdoTestFailure failure, AdoTestBug link) in entry.Tests) Row(failure, Columns.Source, strip: false, source: Source(failure, link));
                foreach (AdoTestFailure failure in related) Row(failure, Columns.Source, strip: false, source: L("SameTestCase"), related: true);
                W("</tbody>\n");
            }
            // The tests that still need a bug, when some test has one: the Overview links here.
            AdoTestFailure[] untracked = [.. model.Failures.Where(static failure => !failure.HasOpenBug)];
            if (untracked.Length > 0 && untracked.Length < model.Failures.Count)
            {
                W("<tbody class=\"error-cluster\" id=\"no-bug\"><tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"" + N(span)
                    + "\"><div class=\"bug-head\"><span class=\"cluster-count\">" + N(untracked.Length) + "</span> <span class=\"bug-summary\"><span class=\"bug-title\">");
                T(L("WithoutOpenBug")); W("</span></span></div></th></tr>\n");
                foreach (AdoTestFailure failure in untracked) Row(failure, Columns.Source, strip: false, source: string.Empty);
                W("</tbody>\n");
            }
            W("</table></div>\n</section>\n");
        }

        // How one test is linked to a bug: through a test result, through its Test Case, or both. A
        // link through results alone says how many of the test's failed results carry the bug; reruns
        // share their parent result's bugs, so results are counted, not attempts.
        private string Source(AdoTestFailure failure, AdoTestBug bug)
        {
            string? result = null;
            if (bug.IsAssociatedWithResult)
            {
                result = L("Result");
                (int RunId, int ResultId)[] failed = [.. failure.Attempts.Where(static attempt => attempt.OutcomeClass == AdoTestOutcomeClass.Failure)
                    .Select(static attempt => (attempt.RunId, attempt.ResultId)).Distinct()];
                int covered = failure.Attempts.Where(attempt => attempt.OutcomeClass == AdoTestOutcomeClass.Failure && attempt.AssociatedBugIds.Contains(bug.Id))
                    .Select(static attempt => (attempt.RunId, attempt.ResultId)).Distinct().Count();
                if (!bug.IsLinkedToTestCase && covered > 0 && covered < failed.Length)
                    result = F(AdoMessage.TestReportLabelValue, L("Result"), F(AdoMessage.TestReportResultsCovered, covered, failed.Length));
            }
            return string.Join(", ", new[] { result, bug.IsLinkedToTestCase ? L("TestCase") : null }.Where(static label => label is not null));
        }

        // Tests that share a Test Case with a test of the bug and do not have the bug. A bug linked to
        // a Test Case is on every test of it (TestBugResolver), so only a Test Case whose tests reach
        // the bug through test results alone can leave a test out.
        private AdoTestFailure[] RelatedByTestCase(BugEntry entry)
        {
            HashSet<int> cases = [.. entry.Tests.Where(static item => item.Failure.TestCase is { Id: > 0 }).GroupBy(static item => item.Failure.TestCase!.Id)
                .Where(static group => !group.Any(static item => item.Bug.IsLinkedToTestCase)).Select(static group => group.Key)];
            if (cases.Count == 0) return [];
            HashSet<int> linked = [.. entry.Tests.Select(static item => item.Failure.Ordinal)];
            return [.. model.Failures.Where(failure => !linked.Contains(failure.Ordinal) && failure.TestCase is { Id: > 0 } testCase && cases.Contains(testCase.Id))];
        }

        // Tests that fail with the error of a test of the bug and are neither linked to it nor listed
        // as of the same Test Case, with the cluster that holds most of them.
        private (int Count, ErrorClusters.Cluster? Cluster) SameError(BugEntry entry, AdoTestFailure[] related)
        {
            HashSet<int> known = [.. entry.Tests.Select(static item => item.Failure.Ordinal), .. related.Select(static failure => failure.Ordinal)];
            (int Count, ErrorClusters.Cluster? Cluster) best = (0, null);
            int total = 0;
            foreach (ErrorClusters.Cluster cluster in entry.Tests.Select(item => ClusterOf(item.Failure)).Where(static cluster => cluster.Key is not null).Distinct())
            {
                int count = cluster.Members.Count(member => !known.Contains(member.Failure.Ordinal));
                total += count;
                if (count > best.Count) best = (count, cluster);
            }
            return (total, best.Cluster);
        }

        // Number, Test Case, test, class, trend, then the optional open bugs, one column per group (or
        // the attempts), and the optional last columns.
        private void TableHead(Columns columns)
        {
            W("<thead><tr><th scope=\"col\">#</th>"); ColumnHead(L("TestCase")); ColumnHead(L("Test")); ColumnHead(L("Class")); ColumnHead(L("Trend"), css: "col-trend");
            if ((columns & Columns.Bugs) != 0) ColumnHead(L("OpenBugs"));
            if (Grouping.IsGrouped)
                for (int index = 0; index < Grouping.Labels.Count; index++) ColumnHead(GroupLabel(index));
            else ColumnHead(L("Attempts"));
            if ((columns & Columns.Error) != 0) ColumnHead(L("LatestError"));
            if ((columns & Columns.Values) != 0) ColumnHead(L("Values"));
            if ((columns & Columns.Source) != 0) ColumnHead(L("BugSource"));
            W("</tr></thead>\n");
        }

        private int ColumnCount(Columns columns) => 5 + (Grouping.IsGrouped ? Grouping.Labels.Count : 1)
            + ((columns & Columns.Bugs) != 0 ? 1 : 0) + ((columns & Columns.Error) != 0 ? 1 : 0) + ((columns & Columns.Values) != 0 ? 1 : 0)
            + ((columns & Columns.Source) != 0 ? 1 : 0);

        // One test: each part in a column of its own, so parts line up from row to row. values are
        // the parts of its error that differ in its cluster; source, when given, is how it is linked.
        private void Row(AdoTestFailure failure, Columns columns, bool strip, string? source = null, IReadOnlyList<string>? values = null, bool related = false)
        {
            string anchor = Anchor(failure);
            W("<tr data-index-for=\"" + anchor + "\"" + (related ? " class=\"related\"" : "") + ">"); Number(failure);
            W("<td class=\"col-case\">");
            if (failure.TestCase is { Id: > 0 } testCase) Link(AdoWebLinks.WorkItem(Collection, Project, testCase.Id), "#" + testCase.Id.ToString(Culture));
            else W("<span class=\"text-muted\">—</span>");
            W("</td><td class=\"col-test\"><a href=\"#" + anchor + "\">"); TestName(failure); W("</a></td><td class=\"col-class\">");
            // The class name only; the card shows the full name.
            if (AttemptGrouper.ClassName(failure.TestName) is { } type) { W("<span class=\"class-name\" title=\""); T(type); W("\">"); T(type); W("</span>"); }
            W("</td><td class=\"col-trend\">"); Trend(failure); W("</td>");
            if ((columns & Columns.Bugs) != 0) BugCell(failure);
            IReadOnlyList<(int? Group, IReadOnlyList<AdoTestAttempt> Attempts)> groups = Groups(failure);
            if (Grouping.IsGrouped)
                for (int index = 0; index < Grouping.Labels.Count; index++)
                    GroupCell(failure, groups.FirstOrDefault(g => g.Group == index).Attempts ?? [], strip);
            else GroupCell(failure, failure.Attempts, strip);
            if ((columns & Columns.Error) != 0)
            {
                // The cell cuts a long line; the title holds all of it.
                string? line = LatestError(failure);
                W("<td class=\"col-error\"");
                if (line is not null) { W(" title=\""); T(line); W("\""); }
                W(">"); T(line); W("</td>");
            }
            if ((columns & Columns.Values) != 0)
            {
                W("<td class=\"col-values\">");
                if (values is { Count: > 0 }) { string text = string.Join(" · ", values); W("<span class=\"values\" title=\""); T(text); W("\">"); T(text); W("</span>"); }
                W("</td>");
            }
            if (source is not null) { W("<td class=\"col-source\">"); T(source); W("</td>"); }
            W("</tr>\n");
        }

        // The short name, cut by the stylesheet when it is long; its title holds all of it.
        private void TestName(AdoTestFailure failure)
        { W("<span class=\"test-name\" title=\""); T(failure.ShortName); W("\">"); T(failure.ShortName); W("</span>"); }

        private TestFailureSignal? Signal(AdoTestFailure failure)
        {
            if (!signals.TryGetValue(failure.Ordinal, out TestFailureSignal? signal)) signals[failure.Ordinal] = signal = TestFailureSignal.Of(failure.History);
            return signal;
        }

        // New, or since when the test fails, linked to that build's test results; nothing when there is
        // no comparison. The title says how many builds in a row.
        private void Trend(AdoTestFailure failure)
        {
            if (Signal(failure) is not { } signal) return;
            if (signal.IsNew) { NewChip(L("New")); return; }
            AdoTestHistoryEntry first = failure.History[^signal.Streak];
            W("<a class=\"trend trend-since\" rel=\"noreferrer\" href=\""); T(AdoWebLinks.BuildTestResult(Collection, Project, first.BuildId).AbsoluteUri); W("\" title=\"");
            T(F(AdoMessage.TestReportInARowSince, signal.Streak, first.BuildNumber)); W("\">");
            T(BuildDate(first.BuildId) is { } date ? F(AdoMessage.TestReportSince, date) : F(AdoMessage.TestReportSinceBuild, first.BuildNumber)); W("</a>");
        }

        // The day a build of the history finished, in the report's offset; null when it is not known.
        private string? BuildDate(int buildId) => model.History.FirstOrDefault(build => build.BuildId == buildId)?.FinishTime is { } time
            ? ReportTime.InOffset(time, model.GeneratedAt.Offset).ToString("d", Culture) : null;

        private void NewChip(string label) { W("<span class=\"trend trend-new\"><span aria-hidden=\"true\">✦</span> "); T(label); W("</span>"); }

        // A bug as a chip, the same wherever it shows, linked to the bug in its own project: red when it
        // is open, grey when it could not be read.
        private void BugChip(AdoTestBug bug)
        {
            bool unread = bug.IsOpen is null;
            W("<a class=\"" + (unread ? "bug-marker bug-unread" : "open-bug-marker") + "\" rel=\"noreferrer\" href=\"");
            T(AdoWebLinks.WorkItem(Collection, bug.TeamProject ?? Project, bug.Id).AbsoluteUri); W("\"");
            if ((unread ? L("BugNotRead") : bug.Title) is { } title) { W(" title=\""); T(title); W("\""); }
            W(">");
            if (!unread) { W("<span class=\"sr-only\">"); T(L("OpenBug")); W(" </span>"); }
            T("#" + bug.Id.ToString(Culture)); W("</a>");
        }

        // The bug's title and state after its chip, and whether it could be read.
        private void BugText(AdoTestBug bug)
        {
            if (bug.Title is not null) { W(" <span class=\"bug-title\">"); T(bug.Title); W("</span>"); }
            if (bug.State is not null) { W(" <span class=\"bug-state\">"); T(bug.State); W("</span>"); }
            if (bug.IsOpen is null) { W(" <span class=\"bug-unread\">"); T(L("BugNotRead")); W("</span>"); }
        }

        // Per pipeline group, how many of the tests ended failed or flaky in it.
        private void GroupFacts(IEnumerable<AdoTestFailure> tests)
        {
            if (!Grouping.IsGrouped) return;
            AdoTestFailure[] list = [.. tests];
            W("<span class=\"fact-group\">");
            for (int index = 0; index < Grouping.Labels.Count; index++)
            {
                int count = list.Count(test => TestFailureGroups.Status(Groups(test).FirstOrDefault(group => group.Group == index).Attempts ?? [])
                    is AdoTestHistoryOutcome.Failed or AdoTestHistoryOutcome.Flaky);
                if (count > 0) { W("<span class=\"fact\">"); T(GroupLabel(index)); W(" <strong>" + N(count) + "</strong></span>"); }
            }
            W("</span>");
        }

        // The lowest-numbered open bug, with how many more; else a bug that could not be read; else
        // nothing.
        private void BugCell(AdoTestFailure failure)
        {
            W("<td class=\"col-bug\">");
            AdoTestBug[] open = [.. failure.Bugs.Where(static bug => bug.Id > 0 && bug.IsOpen == true).OrderBy(static bug => bug.Id)];
            if (open.Length > 0)
            {
                BugChip(open[0]);
                if (open.Length > 1)
                { W(" <span class=\"bug-more\" title=\""); T(string.Join(", ", open.Skip(1).Select(other => "#" + other.Id.ToString(Culture)))); W("\">+" + N(open.Length - 1) + "</span>"); }
            }
            else if (failure.Bugs.FirstOrDefault(static bug => bug.Id > 0 && bug.IsOpen is null) is { } unread) BugChip(unread);
            W("</td>");
        }

        // The status glyph and the ordinal, in a span of its own so that 1, 10 and 100 end at the same place;
        // the stylesheet sizes the span to the largest ordinal.
        private void Number(AdoTestFailure failure)
        { W("<td class=\"col-number\">"); Glyph(Status(failure)); W(" <span class=\"ordinal\">" + N(failure.Ordinal) + "</span></td>"); }

        private void GroupCell(AdoTestFailure failure, IReadOnlyList<AdoTestAttempt> attempts, bool strip)
        {
            W("<td class=\"col-group\">");
            if (attempts.Count == 0) { W("<span class=\"text-muted\">—</span></td>"); return; }
            GroupStatus(attempts);
            if (strip) Strip(failure, attempts, links: true);
            W("</td>");
        }

        // Glyph, hidden status word and failed/total count, for a table cell.
        private void GroupStatus(IReadOnlyList<AdoTestAttempt> attempts)
        {
            AdoTestHistoryOutcome status = TestFailureGroups.Status(attempts);
            W("<span class=\"group-status status-" + StatusPresentation.Css(status) + "\" title=\"");
            T(F(AdoMessage.TestReportGroupFailed, attempts.Count(a => a.OutcomeClass == AdoTestOutcomeClass.Failure), attempts.Count)); W("\"><span aria-hidden=\"true\">" + StatusPresentation.Glyph(status) + "</span> ");
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
                T(F(AdoMessage.TestReportLabelValue, F(AdoMessage.TestReportAttemptOf, attempt.Number, failure.Attempts.Count), StatusPresentation.Label(status, Culture)));
                W("\"></a>");
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
            AdoTestAttempt? latest = LatestErrorAttempt(failure);
            W("<article class=\"card failure-card\" id=\"" + anchor + "\" tabindex=\"-1\" data-attempt-count=\"" + N(failure.Attempts.Count)
                + "\" data-classification=\"" + StatusPresentation.Css(Status(failure)) + "\" data-attachment-count=\"" + N(Attachments(failure)) + "\"");
            if (failure.TestCase is { Id: > 0 } testCase) W(" data-test-case=\"" + N(testCase.Id) + "\"");
            if (failure.HasOpenBug) W(" data-open-bug");
            if (Grouping.IsGrouped)
                W(" data-failing=\"" + string.Join(' ', groups.Where(g => g.Group.HasValue && TestFailureGroups.Status(g.Attempts) == AdoTestHistoryOutcome.Failed)
                    .Select(g => N(g.Group!.Value))) + "\"");
            // The attempt behind the latest error, which the script opens when the test is reached.
            if (latest is not null) W(" data-latest-error=\"" + Anchor(failure, latest) + "\"");
            W(">\n<header><span class=\"failure-number\" aria-hidden=\"true\">" + N(failure.Ordinal) + "</span>"); StatusPresentation.Write(writer, Status(failure), Culture);
            // The class leads the name, so a card says where the test lives without its full name.
            W("<h3 data-short-name>");
            if (AttemptGrouper.ClassName(failure.TestName) is { } type) { W("<span class=\"card-class\">"); T(type + "."); W("</span>"); }
            T(failure.ShortName); W("</h3>"); TestCaseHeading(failure.TestCase);
            W("<span class=\"card-links\">");
            if (failure.Attempts.Count > 0)
            { AdoTestAttempt last = failure.Attempts[^1]; Link(Result(last.RunId, last.ResultId), L("Result")); Link(AdoWebLinks.TestRun(Collection, Project, last.RunId), L("Run")); }
            W("</span></header>\n");
            if (failure.TestName is not null)
            { W("<div class=\"name-section\"><code data-full-name data-copy-value>"); T(failure.TestName); W("</code> "); Button("copy", L("Copy"), description: L("FullName")); W("</div>\n"); }
            // What triage asks first, new or recurring, tracked or not, and what the attempts left
            // behind, comes before the metadata; the rows share one label column.
            bool history = failure.History.Count > 0, bugs = failure.Bugs.Any(static bug => bug.Id > 0), files = Attachments(failure) > 0;
            if (history || bugs || files)
            {
                W("<div class=\"card-facts\">");
                if (history)
                {
                    W("<div class=\"card-history\"><span class=\"strip-label\">"); T(L("History")); W("</span><div class=\"history-line\">");
                    HistoryStrip.Write(writer, failure.History, Collection, Project, Culture, entry => BuildDate(entry.BuildId));
                    Trend(failure);
                    W("</div></div>");
                }
                BugList(failure.Bugs);
                CardAttachments(failure);
                W("</div>\n");
            }
            W("<dl class=\"metadata-grid\">"); Field(L("Storage"), failure.Storage); Field(M(AdoMessage.ReportTitle), failure.Title);
            Field(L("Owner"), Identity(failure.Owner)); Field(M(AdoMessage.ReportPriority), failure.Priority); W("</dl>\n");
            foreach ((int? group, IReadOnlyList<AdoTestAttempt> attempts) in groups)
            {
                if (!Grouping.IsGrouped)
                {
                    PipelineNames(attempts);
                    foreach (AdoTestAttempt attempt in attempts) Attempt(failure, attempt);
                    continue;
                }
                W("<details class=\"attempt-group\" id=\"" + anchor + "-g" + N((group ?? Grouping.Labels.Count) + 1) + "\"><summary><span class=\"group-label\">");
                T(GroupLabel(group)); W("</span> "); StatusPresentation.Write(writer, TestFailureGroups.Status(attempts), Culture);
                W(" <span class=\"group-count\">"); T(F(AdoMessage.TestReportGroupFailed, attempts.Count(a => a.OutcomeClass == AdoTestOutcomeClass.Failure), attempts.Count)); W("</span>");
                Strip(failure, attempts, links: false); W("</summary>\n");
                PipelineNames(attempts);
                foreach (AdoTestAttempt attempt in attempts) Attempt(failure, attempt);
                W("</details>\n");
            }
            W("</article>\n");
        }

        // The test's attachments without opening an attempt: each file name once, from the last attempt
        // that has it, linked to its original, then the attempt it comes from, whose list holds the
        // local copy and the preview, and how many attempts have a file of that name.
        private void CardAttachments(AdoTestFailure failure)
        {
            var files = Groups(failure).SelectMany(static group => group.Attempts)
                .SelectMany(static attempt => attempt.Attachments.Select(attachment => (Attempt: attempt, Attachment: attachment)))
                .GroupBy(static item => item.Attachment.FileName, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) return;
            W("<div class=\"card-attachments\"><span class=\"strip-label\">"); T(L("Attachments")); W("</span><ul class=\"attachment-summary\">");
            foreach (var file in files)
            {
                (AdoTestAttempt attempt, AdoTestAttachment attachment) = file.Last();
                int attempts = file.Select(static item => item.Attempt.Number).Distinct().Count();
                W("<li>");
                Link(AdoWebLinks.TestResultAttachment(Collection, Project, attachment.RunId, attachment.ResultId, attachment.Id, attachment.SubResultId), attachment.FileName);
                if (attachment.Size.HasValue) { W(" "); Size(attachment.Size.Value, " class=\"attachment-size\""); }
                W(" <a class=\"attachment-attempt\" href=\"#" + Anchor(failure, attempt, attachment) + "\">"); T(F(AdoMessage.TestReportAttemptOf, attempt.Number, failure.Attempts.Count)); W("</a>");
                if (attempts > 1)
                {
                    W(" <span class=\"attachment-more\" title=\""); T(F(AdoMessage.TestReportLabelValue, L("Attempts"), string.Join(", ", file.Select(static item => item.Attempt.Number).Distinct().Select(number => number.ToString(Culture)))));
                    W("\">×" + N(attempts) + "</span>");
                }
                W("</li>");
            }
            W("</ul></div>");
        }

        private void Attempt(AdoTestFailure failure, AdoTestAttempt attempt)
        {
            W("<details class=\"attempt\" id=\"" + Anchor(failure, attempt) + "\">");
            // Title, status, duration and machine keep their places from one attempt to the next, even
            // when a value is missing; the server's own outcome, when it adds to the status, follows them.
            W("<summary><span class=\"attempt-title\">"); T(F(AdoMessage.TestReportAttemptOf, attempt.Number, failure.Attempts.Count)); W("</span> ");
            StatusPresentation.Write(writer, Status(attempt.OutcomeClass), Culture);
            W(" <span class=\"attempt-meta attempt-duration\">"); T(Duration(attempt.Duration)); W("</span>");
            W(" <span class=\"attempt-meta attempt-machine\""); if (attempt.ComputerName is not null) { W(" title=\""); T(attempt.ComputerName); W("\""); }
            W(">"); T(attempt.ComputerName); W("</span>");
            if (OutcomeText(attempt.Outcome, attempt.OutcomeClass) is { } outcome) { W(" <span class=\"attempt-outcome\">"); T(outcome); W("</span>"); }
            if (SummaryLine(attempt.ErrorMessage) is { } line) { W(" <span class=\"attempt-error\">"); T(line); W("</span>"); }
            W("</summary>\n<dl class=\"metadata-grid\">");
            AdoTestRun? run = model.Runs.FirstOrDefault(r => r.Id == attempt.RunId);
            Field(L("Source"), L(attempt.Source.ToString()));
            FieldLink(L("Run"), AdoWebLinks.TestRun(Collection, Project, attempt.RunId),
                string.IsNullOrEmpty(run?.Name) ? attempt.RunId.ToString(Culture) : run.Name + " (" + attempt.RunId.ToString(Culture) + ")");
            FieldLink(L("Result"), Result(attempt.RunId, attempt.ResultId), attempt.ResultId.ToString(Culture));
            Field(L("Started"), Date(attempt.StartedDate)); Field(L("Finished"), Date(attempt.CompletedDate));
            Field(M(AdoMessage.ReportId), attempt.SubResultId); Field(L("RunBy"), Identity(attempt.RunBy));
            Field(L("FailureType"), attempt.FailureType); Field(L("Resolution"), attempt.ResolutionState);
            // A build of the history window shows its number; an older one, its ID.
            if (attempt.FailingSinceBuildId is int since and > 0)
                FieldLink(L("FailingSince"), AdoWebLinks.Build(Collection, Project, since),
                    model.History.FirstOrDefault(build => build.BuildId == since)?.BuildNumber ?? since.ToString(Culture));
            // An associated ID that is not among the test's bugs is a closed bug, which the report leaves out.
            // A bug can live in another project than the build.
            foreach (int bug in attempt.AssociatedBugIds.Where(id => id > 0).Distinct())
                if (failure.Bugs.FirstOrDefault(known => known.Id == bug) is { } known)
                { W("<div><dt>"); T(L("Bugs")); W("</dt><dd>"); BugChip(known); W("</dd></div>"); }
            Field(L("Comment"), attempt.Comment); W("</dl>\n");
            // Every attempt shows its own message and trace, even when an earlier attempt had the same text.
            Code(attempt.ErrorMessage, CodeLanguage.ErrorMessage); Code(attempt.StackTrace, CodeLanguage.StackTrace);
            if (attempt.SubResults.Count > 0)
            {
                Heading(4, L("SubResults"));
                foreach (AdoTestSubResult sub in attempt.SubResults) SubResult(sub);
            }
            if (attempt.Iterations.Count > 0)
            {
                Heading(4, L("Iterations"));
                foreach (AdoTestIteration iteration in attempt.Iterations) Iteration(iteration);
            }
            AttachmentList(failure, attempt);
            Fields(L("CustomFields"), attempt.CustomFields); Fields(L("AdditionalFields"), attempt.AdditionalFields);
            W("</details>\n");
        }

        // The line an attempt's summary shows: its error's key line, so that after MSTest's "Test method X
        // threw exception:" the exception itself shows, cut like the other one-line summaries.
        private static string? SummaryLine(string? message) => ErrorClusters.KeyLine(message) is { } line
            ? line.Length <= MaximumSummaryCharacters ? line : Shorten(line, MaximumSummaryCharacters) + "…" : null;

        // Runs of one group share their pipeline names, so they show once per group, not per attempt.
        private void PipelineNames(IReadOnlyList<AdoTestAttempt> attempts)
        {
            AdoTestRun? run = attempts.Select(a => model.Runs.FirstOrDefault(r => r.Id == a.RunId)).FirstOrDefault(r => r is not null);
            if (run is null || (PipelineName(run.StageName) ?? PipelineName(run.PhaseName) ?? PipelineName(run.JobName)) is null) return;
            W("<dl class=\"metadata-grid pipeline-names\">"); Field(L("Stage"), PipelineName(run.StageName)); Field(L("Job"), PipelineName(run.PhaseName));
            Field(L("Instance"), PipelineName(run.JobName)); W("</dl>\n");
        }

        private void SubResult(AdoTestSubResult sub, int headingLevel = 5)
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
            W("<section class=\"iteration\" data-iteration=\"" + N(iteration.Id) + "\"><h5>"); T(L("Iterations") + " " + iteration.Id.ToString(Culture)); W("</h5>");
            W("<dl class=\"metadata-grid\">"); Field(L("Outcome"), iteration.Outcome); W("</dl>"); Code(iteration.ErrorMessage, CodeLanguage.ErrorMessage);
            if (iteration.Parameters.Count > 0)
            {
                Heading(6, M(AdoMessage.ReportParameters)); W("<dl class=\"metadata-grid\">");
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
            Heading(4, L("Attachments")); W("<ul class=\"attachment-list\">\n");
            foreach (AdoTestAttachment attachment in attempt.Attachments)
            {
                string anchor = Anchor(failure, attempt, attachment);
                W("<li id=\"" + anchor + "\"");
                if (attachment.DownloadStatus != AdoTestAttachmentStatus.NotRequested)
                    W(" data-download-status=\"" + attachment.DownloadStatus.ToString().ToLowerInvariant() + "\"");
                W(">");
                // Browser navigation uses Windows authentication to download the original, for every file type.
                W("<span class=\"attachment-name\">");
                Link(AdoWebLinks.TestResultAttachment(Collection, Project, attachment.RunId, attachment.ResultId, attachment.Id, attachment.SubResultId), attachment.FileName);
                if (attachment.Size.HasValue) { W(" "); Size(attachment.Size.Value, " class=\"attachment-size\""); }
                W("</span>");
                // GeneralAttachment is the default type of nearly every attachment, so only other types show.
                string? type = string.Equals(attachment.AttachmentType, "GeneralAttachment", StringComparison.OrdinalIgnoreCase) ? null : attachment.AttachmentType;
                if (attachment.Comment is not null || type is not null || attachment.SubResultId is not null)
                {
                    W("<dl class=\"metadata-grid\">"); Field(L("Comment"), attachment.Comment); Field(L("AttachmentType"), type);
                    Field(M(AdoMessage.ReportId), attachment.SubResultId); W("</dl>");
                }
                LocalAttachment(attachment, anchor);
                W("</li>\n");
            }
            W("</ul>\n");
        }

        // The previews a report shows are chosen before anything is written: the attachments of the
        // latest run first, then those of the other runs, each in the order the cards show them. One
        // is admitted while the total stays within the inline limit; one that does not fit is passed
        // over, and a later, smaller one can still fit. Each listing has its own anchor.
        private HashSet<string> Previews => previews ??= ChoosePreviews();

        private HashSet<string> ChoosePreviews()
        {
            HashSet<string> chosen = new(StringComparer.Ordinal);
            if (model.LocalAttachments is not { } limits) return chosen;
            IReadOnlyList<AdoTestRun> runs = AttemptGrouper.OrderRuns(model.Runs);
            int? latest = runs.Count > 0 ? runs[^1].Id : null;
            long total = 0;
            for (int pass = 0; pass < 2; pass++)
                foreach (AdoTestFailure failure in model.Failures)
                    foreach (AdoTestAttempt attempt in Groups(failure).SelectMany(static group => group.Attempts))
                        foreach (AdoTestAttachment attachment in attempt.Attachments)
                        {
                            if ((attachment.RunId == latest) != (pass == 0) || !Previewable(attachment, limits, out long length)
                                || total + length > limits.MaximumInlineTotalBytes) continue;
                            total += length;
                            chosen.Add(Anchor(failure, attempt, attachment));
                        }
            return chosen;
        }

        // Verified JSON or text that was downloaded and is small enough to show.
        private bool Previewable(AdoTestAttachment attachment, TestFailureLocalAttachments limits, out long length)
        {
            length = LocalFile(attachment)?.Length ?? 0;
            return length > 0 && length <= limits.MaximumInlineJsonBytes && attachment.DownloadStatus == AdoTestAttachmentStatus.Downloaded
                && attachment.Kind is AdoTestAttachmentKind.Json or AdoTestAttachmentKind.Text;
        }

        // Writes the local link, status notes and a collapsed preview of verified JSON or text. Local
        // names and hrefs are toolkit-generated; the remote name appears only as encoded text.
        private void LocalAttachment(AdoTestAttachment attachment, string anchor)
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
            else if (preview && !Previews.Contains(anchor)) { note = L("NotInlinedBudget"); preview = false; }
            W("<div class=\"attachment-local\">");
            if (file is { } local)
            {
                W("<a class=\"local-file\" data-local-file href=\""); T(local.Href); W("\">"); T(L("LocalCopy"));
                W("</a> <code>"); T(local.Name); W("</code> "); Size(local.Length);
            }
            if (note is not null) { W("<p class=\"attachment-status\">"); T(note); W("</p>"); }
            W("</div>");
            if (!preview || file is not { } source) return;
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

        // Bytes up to 1,024. Above that, kilobytes or megabytes with one decimal, and the exact
        // number of bytes as the title.
        private void Size(long bytes, string attributes = "")
        {
            string exact = F(AdoMessage.TestReportBytes, bytes);
            if (bytes <= 1024) { W("<span" + attributes + ">"); T(exact); W("</span>"); return; }
            double kilobytes = bytes / 1024d;
            // A value that would print as 1,024.0 KB is a megabyte.
            bool mega = Math.Round(kilobytes, 1, MidpointRounding.AwayFromZero) >= 1024;
            W("<span" + attributes + " title=\""); T(exact); W("\">");
            T(mega ? F(AdoMessage.TestReportMegabytes, kilobytes / 1024d) : F(AdoMessage.TestReportKilobytes, kilobytes)); W("</span>");
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

        // Every bug of the test with its title and state. Each one is open, except a bug that could
        // not be read: it shows its ID link and says so.
        private void BugList(IReadOnlyList<AdoTestBug> bugs)
        {
            if (!bugs.Any(b => b.Id > 0)) return;
            W("<div class=\"card-bugs\"><span class=\"strip-label\">"); T(L("BugList")); W("</span><ul class=\"bug-list\">");
            foreach (AdoTestBug bug in bugs.Where(b => b.Id > 0))
            {
                W("<li data-bug=\"" + N(bug.Id) + "\"" + (bug.IsOpen == true ? " data-open-bug" : "") + ">");
                BugChip(bug); BugText(bug); W("</li>");
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

        // The build's runs with what each one ran and holds, then the run history as a chart, as a
        // table of builds and as a table of tests.
        private void Runs()
        {
            W("<section class=\"view\" id=\"runs\" data-view>\n"); Heading(2, L("RunsAndHistory"));
            // The history comes first: how this build compares with the ones before it.
            W("<section class=\"history-panel\" id=\"history\">\n"); Heading(3, L("History"));
            RunHistoryChart.Write(writer, model.History, Collection, Project, Culture, model.GeneratedAt.Offset); W("\n</section>\n");
            // Runs of one group sit together, in attempt order; the name columns already say the group.
            IReadOnlyList<AdoTestRun> ordered = AttemptGrouper.OrderRuns(model.Runs);
            IReadOnlyList<AdoTestRun> runs = [.. ordered.Select((run, order) => (Run: run, Order: order))
                .OrderBy(item => Grouping.GroupOf(item.Run.Id) ?? int.MaxValue).ThenBy(item => item.Order).Select(item => item.Run)];
            if (runs.Count > 0)
            {
                Heading(3, L("TestRuns"));
                bool stage = runs.Any(r => PipelineName(r.StageName) is not null), job = runs.Any(r => PipelineName(r.PhaseName) is not null),
                    instance = runs.Any(r => PipelineName(r.JobName) is not null);
                // The state says something only when a run has not completed; otherwise every row repeats it.
                bool state = runs.Any(static r => !string.Equals(r.State, "Completed", StringComparison.OrdinalIgnoreCase));
                // Only the attempt levels that some run has. The heading names them, so a lone number is
                // never read as another level's attempt; a run without one of them shows a dash there.
                (AdoMessage Name, Func<AdoTestRun, int?> Of)[] levels = [.. new (AdoMessage Name, Func<AdoTestRun, int?> Of)[]
                {
                    (AdoMessage.TestReportStageLevel, static r => r.StageAttempt), (AdoMessage.TestReportJobLevel, static r => r.PhaseAttempt),
                    (AdoMessage.TestReportInstanceLevel, static r => r.PipelineAttempt),
                }.Where(level => runs.Any(run => level.Of(run) is not null))];
                // The latest run is the one whose larger attachments are downloaded by default.
                int latest = ordered[^1].Id;
                int span = 9 + (stage ? 1 : 0) + (job ? 1 : 0) + (instance ? 1 : 0) + (levels.Length > 0 ? 1 : 0) + (state ? 1 : 0);
                W("<div class=\"table-scroll\"><table class=\"runs-table\"><thead><tr>");
                ColumnHead(L("Run")); ColumnHead(M(AdoMessage.ReportId), numeric: true);
                if (stage) ColumnHead(L("Stage"));
                if (job) ColumnHead(L("Job"));
                if (instance) ColumnHead(L("Instance"));
                if (levels.Length > 0) ColumnHead(F(AdoMessage.TestReportAttemptLevels, string.Join(" / ", levels.Select(level => M(level.Name)))));
                ColumnHead(L("Started")); ColumnHead(L("Duration"), numeric: true);
                if (state) ColumnHead(L("State"));
                ColumnHead(L("Tests"), numeric: true); ColumnHead(L("Passed"), numeric: true); ColumnHead(L("Failed"), numeric: true);
                ColumnHead(L("ReportedTests"), numeric: true); ColumnHead(L("Attachments"));
                W("</tr></thead>\n");
                // A grouped build has one row group per pipeline group, headed by its name.
                bool started = false;
                int? current = null;
                foreach (AdoTestRun run in runs)
                {
                    int? group = Grouping.GroupOf(run.Id);
                    if (!started || (Grouping.IsGrouped && group != current))
                    {
                        if (started) W("</tbody>\n");
                        W("<tbody>\n");
                        started = true;
                        current = group;
                        if (Grouping.IsGrouped)
                        {
                            W("<tr class=\"group-heading\"><th scope=\"rowgroup\" colspan=\"" + N(span) + "\">"); T(GroupLabel(group)); W(" <span class=\"group-runs\">");
                            T(F(AdoMessage.TestReportLabelValue, L("TestRuns"), runs.Count(other => Grouping.GroupOf(other.Id) == group).ToString(Culture))); W("</span></th></tr>\n");
                        }
                    }
                    // The window applies to listed attachments only.
                    bool outside = model.AttachmentsListed && !model.AttachmentRunIds.Contains(run.Id);
                    W("<tr data-run=\"" + N(run.Id) + "\"" + (run.Id == latest ? " data-latest-run" : "") + (outside ? " class=\"outside-window\"" : "")
                        + "><td class=\"col-run\"><span class=\"cell-split\">");
                    Link(AdoWebLinks.TestRun(Collection, Project, run.Id), run.Name.Length > 0 ? run.Name : run.Id.ToString(Culture));
                    // At the end of the cell, so that it lines up from row to row.
                    if (run.Id == latest) { W(" <span class=\"latest-run\">"); T(L("LatestRun")); W("</span>"); }
                    W("</span></td>");
                    Cell(run.Id.ToString(Culture), numeric: true);
                    if (stage) Cell(PipelineName(run.StageName));
                    if (job) Cell(PipelineName(run.PhaseName));
                    if (instance) Cell(PipelineName(run.JobName));
                    if (levels.Length > 0) Cell(string.Join(" / ", levels.Select(level => level.Of(run)?.ToString(Culture) ?? "–")));
                    Cell(Date(run.StartedDate)); Cell(RunDuration(run), numeric: true);
                    if (state) Cell(run.State);
                    Cell(run.TotalTests?.ToString(Culture), numeric: true); CountCell(run.PassedTests, AdoTestHistoryOutcome.Passed);
                    // Empty when the server sent no per-outcome statistics: an absent count is not zero.
                    CountCell(run.OutcomeCounts.Count == 0 ? null : run.OutcomeCounts.Where(static pair => OutcomeClassifier.Classify(pair.Key) == AdoTestOutcomeClass.Failure)
                        .Sum(static pair => (long)pair.Value), AdoTestHistoryOutcome.Failed);
                    Cell(model.Failures.Count(f => f.Attempts.Any(a => a.RunId == run.Id)).ToString(Culture), numeric: true);
                    AdoTestAttachment[] listed = [.. model.Failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments).Where(a => a.RunId == run.Id)
                        .DistinctBy(static a => (a.ResultId, a.SubResultId, a.Id))];
                    Cell(!model.AttachmentsListed ? L("RunAttachmentsNotListed") : outside ? L("OutsideWindow")
                        : F(AdoMessage.TestReportRunAttachmentCounts, listed.Length, listed.Count(a => LocalFile(a) is not null)));
                    W("</tr>\n");
                }
                W("</tbody></table></div>\n");
            }
            W("<p class=\"window-note\">");
            T(model.AttachmentsListed ? F(AdoMessage.TestReportWindowNote, Date(model.AttachmentWindowStart)!, model.OmittedAttachmentCount) : L("AttachmentsNotListedNote"));
            W("</p>\n");
            HistoryByTest();
            W("</section>\n");
        }

        // h:mm:ss, the same in every culture; null when the run has no start, no end, or ends before it starts.
        private static string? RunDuration(AdoTestRun run) =>
            run.CompletedDate - run.StartedDate is { } elapsed && elapsed >= TimeSpan.Zero
                ? ((long)elapsed.TotalHours).ToString(CultureInfo.InvariantCulture) + elapsed.ToString(@"\:mm\:ss", CultureInfo.InvariantCulture) : null;

        // One row per reported test with its outcome in every build of the history, oldest first, and
        // whether it is new or since when it fails. The cards hold the same cells one test at a time;
        // here they can be compared across tests.
        private void HistoryByTest()
        {
            if (model.Failures.Count == 0 || model.History.Count == 0) return;
            Heading(3, L("HistoryByTest"));
            W("<div class=\"table-scroll\"><table class=\"failure-table history-by-test\">\n<thead><tr><th scope=\"col\">#</th>");
            ColumnHead(L("Test")); ColumnHead(L("Class"));
            foreach (AdoBuildTestSummary build in model.History) { W("<th scope=\"col\" class=\"col-build\">"); T(build.BuildNumber); W("</th>"); }
            ColumnHead(L("Trend"), css: "col-trend");
            W("</tr></thead>\n<tbody>\n");
            foreach (AdoTestFailure failure in model.Failures)
            {
                string anchor = Anchor(failure);
                W("<tr data-index-for=\"" + anchor + "\">"); Number(failure); W("<td class=\"col-test\"><a href=\"#" + anchor + "\">");
                TestName(failure); W("</a></td><td class=\"col-class\">");
                if (AttemptGrouper.ClassName(failure.TestName) is { } type) { W("<span class=\"class-name\" title=\""); T(type); W("\">"); T(type); W("</span>"); }
                W("</td>");
                // A result without an automated name has no history cells.
                foreach (AdoBuildTestSummary build in model.History)
                {
                    W("<td class=\"col-build\">");
                    if (failure.History.FirstOrDefault(entry => entry.BuildId == build.BuildId) is { } cell) Glyph(cell.Outcome);
                    else W("<span class=\"text-muted\">—</span>");
                    W("</td>");
                }
                W("<td class=\"col-trend\">"); Trend(failure); W("</td></tr>\n");
            }
            W("</tbody></table></div>\n");
        }

        // When and where the report was made, under every view, then the keys the script answers to,
        // which the band leaves out to stay short.
        private void Footer()
        {
            W("<footer class=\"report-footer\"><dl class=\"secondary-line\">");
            Field(M(AdoMessage.ReportGeneratedAt), Date(model.GeneratedAt)); Field(M(AdoMessage.ReportToolkitVersion), model.ToolkitVersion);
            Field(M(AdoMessage.ReportServer), Collection.GetLeftPart(UriPartial.Authority)); Field(M(AdoMessage.ReportCollection), Collection.AbsolutePath);
            Field(M(AdoMessage.ReportProject), Project); W("</dl>");
            W("<p class=\"interactive keyboard-hint\" data-enhance hidden>"); T(L("NavigationHint")); W("</p></footer>\n");
        }

        private void ColumnHead(string label, bool numeric = false, string? css = null)
        { W("<th scope=\"col\"" + (numeric ? " class=\"num\"" : css is null ? "" : " class=\"" + css + "\"") + ">"); T(label); W("</th>"); }

        private void Cell(string? value, bool numeric = false) { W(numeric ? "<td class=\"num\">" : "<td>"); T(value); W("</td>"); }

        // A count of passed or failed tests with its status's glyph and colour, muted at zero, empty
        // when the server sent none.
        private void CountCell(long? count, AdoTestHistoryOutcome status)
        {
            W("<td class=\"num\">");
            if (count == 0) W("<span class=\"text-muted\">0</span>");
            else if (count is long value)
            {
                W("<span class=\"run-count status-" + StatusPresentation.Css(status) + "\"><span aria-hidden=\"true\">" + StatusPresentation.Glyph(status) + "</span> ");
                T(value.ToString(Culture)); W("</span>");
            }
            W("</td>");
        }

        private void Diagnostics()
        {
            if (!HasDiagnostics) return;
            W("<section class=\"view\" id=\"diagnostics\" data-view>\n<h2 class=\"section-heading\">"); T(M(AdoMessage.ReportDiagnostics));
            // Errors, then warnings, then information; the sort keeps the order within each.
            AdoDiagnostic[] ordered = [.. model.Diagnostics.OrderByDescending(static diagnostic => diagnostic.Severity)];
            foreach (IGrouping<AdoDiagnosticSeverity, AdoDiagnostic> severity in ordered.GroupBy(static diagnostic => diagnostic.Severity))
            {
                W(" <span class=\"diagnostic-count\" data-severity=\"" + severity.Key.ToString().ToLowerInvariant() + "\">"); T(L(severity.Key.ToString()));
                W(" <strong>"); T(severity.Count().ToString(Culture)); W("</strong></span>");
            }
            W("</h2>\n");
            if (ordered.Length > 0)
            {
                W("<ul class=\"diagnostic-list\">\n");
                foreach (AdoDiagnostic diagnostic in ordered)
                {
                    W("<li data-severity=\"" + diagnostic.Severity.ToString().ToLowerInvariant() + "\" data-diagnostic=\""); T(diagnostic.Code);
                    W("\"><span class=\"diagnostic-severity\">"); T(L(diagnostic.Severity.ToString())); W("</span> <code>"); T(diagnostic.Code);
                    W("</code> <span class=\"diagnostic-message\">"); T(diagnostic.Message); W("</span></li>\n");
                }
                W("</ul>\n");
            }
            W("</section>\n");
        }

        private void Fields(string label, IReadOnlyDictionary<string, object?> fields)
        {
            if (fields.Count == 0) return;
            Heading(4, label); W("<dl class=\"metadata-grid\">");
            foreach ((string key, object? value) in fields.OrderBy(pair => pair.Key, StringComparer.Ordinal)) Field(key, value);
            W("</dl>\n");
        }

        // A long value, such as a comment or the JSON of a custom field, takes a whole row of the grid.
        private void Field(string label, object? value)
        {
            if (value is null) return;
            string? text = FieldText(value, Culture);
            W(text is { Length: > WideField } || text?.Contains('\n', StringComparison.Ordinal) == true ? "<div class=\"wide\"><dt>" : "<div><dt>");
            T(label); W("</dt><dd>"); T(text); W("</dd></div>");
        }

        private void FieldLink(string label, Uri uri, string text)
        { W("<div><dt>"); T(label); W("</dt><dd>"); Link(uri, text); W("</dd></div>"); }

        private void Link(Uri uri, string text)
        { W("<a rel=\"noreferrer\" href=\""); T(uri.AbsoluteUri); W("\">"); T(text); W("</a>"); }

        private Uri Result(int run, int result) => AdoWebLinks.BuildTestResult(Collection, Project, model.Build.Id, run, result);
        private static string? Identity(AdoIdentityRef? identity) => identity is null ? null : identity.DisplayName
            + (identity.UniqueName is null ? "" : " <" + identity.UniqueName + ">");

        // The server's outcome shows only when it adds to the badge, such as Timeout or Aborted.
        private void Outcome(string outcome, AdoTestOutcomeClass outcomeClass)
        {
            StatusPresentation.Write(writer, Status(outcomeClass), Culture);
            if (OutcomeText(outcome, outcomeClass) is { } text) { W(" <span class=\"attempt-outcome\">"); T(text); W("</span>"); }
        }

        private string? OutcomeText(string outcome, AdoTestOutcomeClass outcomeClass)
        {
            string? plain = outcomeClass switch { AdoTestOutcomeClass.Pass => "Passed", AdoTestOutcomeClass.Failure => "Failed", _ => null };
            return string.Equals(outcome, plain, StringComparison.OrdinalIgnoreCase)
                || string.Equals(outcome, StatusPresentation.Label(Status(outcomeClass), Culture), StringComparison.Ordinal) ? null : outcome;
        }

        private void Button(string action, string label, bool toggle = false, string? description = null, bool detailsOnly = false)
        {
            W("<button type=\"button\" class=\"interactive\" data-enhance hidden data-action=\"" + action + "\"" + (detailsOnly ? " data-details-only" : ""));
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
