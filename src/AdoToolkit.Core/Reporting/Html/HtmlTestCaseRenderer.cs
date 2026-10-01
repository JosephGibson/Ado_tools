using System.Text;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.RichText;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.Html;

// One page per export, themed like the failed-test report: a top bar with counts, section links
// and filters, then one card per Test Case. A document of several cases starts with an overview
// and a table of contents grouped by suite. Every part renders without the script; the script
// only filters, collapses, copies, shows parameter values and moves the selection.
public static class HtmlTestCaseRenderer
{
    internal const string ScriptAsset = "testcase-report.js";

    public static void Render(ReportDocumentModel model, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);
        new Document(model, writer).Write();
    }

    // Wraps each @name token of a declared parameter so the script can show the values of one
    // iteration. The name follows a character that is neither a letter nor a digit, and ends there.
    internal static string MarkParameters(string text, IReadOnlyList<string> names, Func<string, string> encode)
    {
        if (names.Count == 0 || text.IndexOf('@', StringComparison.Ordinal) < 0) return encode(text);
        StringBuilder result = new();
        int position = 0;
        for (int at = text.IndexOf('@', StringComparison.Ordinal); at >= 0; at = text.IndexOf('@', at + 1))
        {
            if (at > 0 && (char.IsLetterOrDigit(text[at - 1]) || text[at - 1] == '@')) continue;
            int best = -1, length = 0;
            for (int index = 0; index < names.Count; index++)
            {
                string name = names[index];
                int end = at + 1 + name.Length;
                if (name.Length > length && end <= text.Length
                    && string.Compare(text, at + 1, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0
                    && (end == text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] == '_')))
                { best = index; length = name.Length; }
            }
            if (best < 0) continue;
            result.Append(encode(text[position..at])).Append("<span class=\"param\" data-param=\"")
                .Append(best.ToString(CultureInfo.InvariantCulture)).Append("\">")
                .Append(SinkEncoding.Attribute(text.Substring(at, length + 1))).Append("</span>");
            position = at + 1 + length;
            at = position - 1;
        }
        return result.Append(encode(text[position..])).ToString();
    }

    private sealed class Document(ReportDocumentModel model, TextWriter writer)
    {
        private static readonly IReadOnlyList<string> NoParameters = [];
        private bool Multiple => model.IsMultiCase;
        private CultureInfo Culture => model.Culture;
        private void W(string value) => writer.Write(value);
        private void T(string? value) => W(SinkEncoding.Attribute(value ?? string.Empty));
        private void H(string value) => W(SinkEncoding.Html(value));
        private string L(string key) => model.Labels[key];
        private string M(AdoMessage key) => Messages.Get(key, Culture);
        private string F(AdoMessage key, params object[] arguments) => Messages.Get(key, Culture, arguments);
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        // Server times are UTC; every time shows in the export's offset, like the generation time.
        // A time too close to the limits of the calendar to be shifted keeps its own offset.
        private string? Date(DateTimeOffset? value)
        {
            if (value is not { } time) return null;
            long shifted = time.UtcTicks + model.GeneratedAt.Offset.Ticks;
            return (shifted < DateTime.MinValue.Ticks || shifted > DateTime.MaxValue.Ticks ? time : time.ToOffset(model.GeneratedAt.Offset)).ToString("g", Culture);
        }

        internal void Write()
        {
            string script = TestFailureAssets.Read(ScriptAsset);
            TestCaseReportModel? only = Multiple ? null : model.Cases[0];
            W("<!DOCTYPE html>\n<html lang=\""); T(Culture.Name); W("\" data-case-count=\"" + N(model.Cases.Count) + "\">\n<head>\n<meta charset=\"utf-8\">\n");
            W("<meta http-equiv=\"Content-Security-Policy\" content=\""); T(ContentSecurityPolicy.Create([script])); W("\">\n");
            W("<meta name=\"generator\" content=\"AdoToolkit "); T(model.ToolkitVersion); W("\">\n");
            W("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<title>");
            T(only is null ? L("DocumentHeading")
                : L("Heading") + " — " + only.Id.ToString(Culture) + " · " + SinkEncoding.NormalizeLines(only.Title).Replace('\n', ' '));
            W("</title>\n<style>\n");
            W(TestFailureAssets.Read("report-base.css")); W(TestFailureAssets.Read("testcase-report.css"));
            if (Multiple) W(TestFailureAssets.Read("testcase-document.css"));
            W("</style>\n</head>\n<body>\n");
            Header(only);
            W("<main id=\"report-content\">\n");
            if (only is not null) Case(only, "report-1", null);
            else
            {
                Cover();
                Contents();
                W("<p data-no-matches hidden>"); T(L("NoMatchingCases")); W("</p>\n");
                // The case list builds each model on access; each body is written before the next is built.
                int index = 0;
                foreach (TestCaseReportModel testCase in model.Cases)
                {
                    string anchor = model.Contents[index++].Anchor;
                    Case(testCase, anchor, anchor);
                    writer.Flush();
                }
            }
            Footer(only);
            W("</main>\n<p class=\"copy-feedback\" role=\"status\" data-copy-status hidden data-label-done=\""); T(M(AdoMessage.TestReportCopied));
            W("\" data-label-selected=\""); T(M(AdoMessage.TestReportSelectCopy)); W("\"></p>\n");
            W("<script>"); W(script); W("</script>\n</body>\n</html>\n");
        }

        private void Header(TestCaseReportModel? only)
        {
            W("<a class=\"skip-link\" href=\"#" + (only is null ? "contents" : "report-1-steps") + "\">"); T(L(only is null ? "Contents" : "Steps")); W("</a>\n");
            W("<header class=\"top-bar\"><div class=\"top-bar-inner\">\n<div class=\"title-line\"><span class=\"report-brand\">"); T(L("Brand"));
            W("</span><h1>"); T(L(only is null ? "DocumentHeading" : "Heading")); W("</h1><span class=\"report-scope\">"); T(model.Project); W("</span>");
            if (only is null)
            {
                Chip("cases", L("CaseCount"), model.Contents.Count);
                Chip("complete", L("Complete"), model.CompleteCount);
                Chip("partial", L("Partial"), model.PartialCount);
            }
            else
            {
                Chip("steps", L("Steps"), only.StepCount);
                if (only.SharedSteps.Count > 0) Chip("shared", L("SharedSteps"), only.SharedSteps.Count);
                if (only.Parameters.Rows.Count > 0) Chip("iterations", M(AdoMessage.TestReportIterations), only.Parameters.Rows.Count);
                if (only.Status == AdoTestCaseStatus.Partial)
                { W("<a class=\"partial-link\" href=\"#report-1-diagnostics\"><span aria-hidden=\"true\">!</span> "); T(L("Partial")); W("</a>"); }
                W("<span class=\"results-link\">"); Link(only.WebUrl, M(AdoMessage.TestReportOpenAdo), glyph: false); W("</span>");
            }
            W("</div>\n<nav class=\"section-links\" aria-label=\""); T(L("Navigation")); W("\">\n");
            foreach ((string id, string label) in only is null ? [("overview", L("Overview")), ("contents", L("Contents"))] : Sections(only, "report-1"))
            { W("<a href=\"#" + id + "\">"); T(label); W("</a>\n"); }
            W("</nav>\n<div class=\"interactive filter-controls\" data-enhance hidden>\n<label class=\"search\">"); T(M(AdoMessage.TestReportFilter));
            W(" <input type=\"search\" data-filter></label>\n");
            if (only is null && model.PartialCount > 0) Toggle("partial", L("PartialOnly"));
            if (only is null && model.Contents.Any(static entry => HasFailedPoint(entry.Detail))) Toggle("failed", L("FailedOutcome"));
            if (only is null || only.Rows.Any(static row => row.Kind == AdoTestStepKind.SharedStep && row.IsExpanded))
            { Button("expand", M(AdoMessage.TestReportExpandAll)); Button("collapse", M(AdoMessage.TestReportCollapseAll)); }
            W("<span class=\"filter-result-count\" role=\"status\" aria-live=\"polite\" data-filter-count data-label-count=\"");
            T(L(only is null ? "CaseFilterCount" : "StepFilterCount")); W("\"></span>");
            W("<p class=\"text-muted keyboard-hint\">"); T(L(only is null ? "CaseNavigationHint" : "StepNavigationHint")); W("</p></div>\n</div></header>\n");
        }

        // Multi-case overview (§12.5): project, source, plans, suites and totals by status.
        private void Cover()
        {
            W("<section class=\"document-cover\" id=\"overview\">\n<h2 class=\"section-heading\">"); T(L("Overview")); W("</h2>\n<dl class=\"metadata-grid\">");
            foreach ((string label, string value, Uri? url) in ReportHeader.CoverFields(model))
            {
                // The server, the collection and the generation time close the page.
                if (label is "Server" or "Collection" or "GeneratedAt") continue;
                if (url is null) Field(L(label), value); else FieldLink(L(label), url, value);
            }
            W("</dl>\n</section>\n");
        }

        private void Contents()
        {
            bool priority = model.Contents.Any(static entry => entry.Priority.HasValue), outcome = model.Contents.Any(static entry => entry.Detail?.Points is not null);
            W("<nav class=\"toc\" aria-labelledby=\"contents\">\n<h2 class=\"section-heading\" id=\"contents\">"); T(L("Contents"));
            W(" <span class=\"count\">"); T(model.Contents.Count.ToString(Culture)); W("</span></h2>\n<div class=\"table-scroll\"><table class=\"toc-table\">\n<thead><tr>");
            foreach (string key in new[] { "Id", "Title", "State" }) ColumnHead(L(key));
            if (priority) ColumnHead(L("Priority"));
            ColumnHead(L("Steps"));
            if (outcome) ColumnHead(L("LastOutcome"));
            ColumnHead(L("Status"));
            W("</tr></thead>\n");
            int columns = 5 + (priority ? 1 : 0) + (outcome ? 1 : 0);
            foreach ((string group, IReadOnlyList<ReportContentsEntry> entries) in ReportHeader.Groups(model))
            {
                W("<tbody class=\"toc-group\">\n<tr class=\"cluster-heading\"><th scope=\"colgroup\" colspan=\"" + N(columns) + "\">"); H(group); W("</th></tr>\n");
                foreach (ReportContentsEntry entry in entries)
                {
                    W("<tr data-index-for=\"" + entry.Anchor + "\"><td class=\"col-number\">"); T(entry.Id.ToString(Culture)); W("</td><td class=\"col-title\"><a href=\"#" + entry.Anchor + "\">");
                    H(entry.Title); W("</a></td><td>"); T(entry.State); W("</td>");
                    if (priority) { W("<td class=\"col-number\">"); T(entry.Priority?.ToString(Culture)); W("</td>"); }
                    W("<td class=\"col-number\">"); T(entry.StepCount.ToString(Culture)); W("</td>");
                    if (outcome) { W("<td class=\"col-outcome\">"); PointSummary(entry.Detail?.Points); W("</td>"); }
                    W("<td>"); StatusBadge(entry.Status); W("</td></tr>\n");
                }
                W("</tbody>\n");
            }
            W("</table></div>\n</nav>\n");
        }

        private void Case(TestCaseReportModel testCase, string anchor, string? id)
        {
            AdoTestCaseDetail? detail = testCase.Detail;
            W("<article class=\"test-case\"" + (id is null ? "" : " id=\"" + id + "\"") + ">\n");
            W("<header id=\"" + anchor + "-overview\" data-case=\"" + N(testCase.Id) + "\" data-status=\"" + StatusCss(testCase.Status) + "\""
                + (HasFailedPoint(detail) ? " data-failed" : "") + "><span class=\"case-number\">");
            T(testCase.Id.ToString(Culture)); W("</span>"); StatusBadge(testCase.Status);
            W("<h2><a rel=\"noreferrer\" href=\""); T(testCase.WebUrl.AbsoluteUri); W("\"><span data-copy-value>"); H(testCase.Title); W("</span>");
            StatusPresentation.ExternalGlyph(writer, Culture); W("</a></h2><span class=\"card-links\">");
            Button("copy", M(AdoMessage.TestReportCopy), description: L("Title"));
            if (id is not null) Collapse(testCase.Title);
            W("</span></header>\n<div class=\"case-body\">\n");
            if (id is not null)
            {
                // Each case of a document carries its own section links.
                W("<nav class=\"case-links\" aria-label=\""); T(L("Navigation") + " — " + testCase.Id.ToString(Culture)); W("\">\n");
                foreach ((string target, string label) in Sections(testCase, anchor).Skip(1)) { W("<a href=\"#" + target + "\">"); T(label); W("</a>\n"); }
                W("<a href=\"#contents\">"); T(L("Contents")); W("</a>\n</nav>\n");
            }
            Metadata(testCase);
            if (detail is { Tags.Count: > 0 })
            {
                W("<div class=\"tag-list\"><span class=\"strip-label\">"); T(L("Tags")); W("</span><ul>");
                foreach (string tag in detail.Tags) { W("<li>"); T(tag); W("</li>"); }
                W("</ul></div>\n");
            }
            if (testCase.SharedSteps.Count > 0)
            {
                W("<div class=\"shared-list\"><span class=\"strip-label\">"); T(L("SharedSteps")); W("</span><ul>");
                foreach (AdoSharedStepInfo shared in testCase.SharedSteps)
                {
                    string title = (shared.Title ?? L("SharedSteps")) + " (#" + shared.Id.ToString(Culture) + ")";
                    W("<li>");
                    if (shared.WebUrl is { } url) Link(url, title); else H(title);
                    if (shared.ReferenceCount > 1) { W(" <span class=\"text-muted\">× "); T(shared.ReferenceCount.ToString(Culture)); W("</span>"); }
                    W("</li>");
                }
                W("</ul></div>\n");
            }
            if (detail?.AutomatedTestName is { } automated)
            {
                W("<div class=\"name-section\"><span class=\"strip-label\">"); T(L("AutomatedTest")); W("</span><code data-copy-value>"); T(automated); W("</code> ");
                Button("copy", M(AdoMessage.TestReportCopy), description: L("AutomatedTest")); W("</div>\n");
            }
            if (detail?.Description is { } description)
            {
                W("<section class=\"description\">\n<h3 class=\"section-heading\" id=\"" + anchor + "-description\">"); T(L("Description")); W("</h3>\n<div class=\"description-text\">");
                W(Text(description, detail.DescriptionSource, NoParameters)); W("</div>\n</section>\n");
            }
            Parameters(testCase, anchor);
            Steps(testCase, anchor, id is null);
            if (detail is not null && HasLinks(detail)) Links(detail, anchor);
            if (detail?.Points is { } points) Points(points, anchor);
            Diagnostics(testCase, anchor);
            W("</div>\n</article>\n");
        }

        private IEnumerable<(string Id, string Label)> Sections(TestCaseReportModel testCase, string anchor)
        {
            yield return (anchor + "-overview", L("Overview"));
            if (testCase.Detail?.Description is not null) yield return (anchor + "-description", L("Description"));
            if (HasParameters(testCase)) yield return (anchor + "-parameters", L("Parameters"));
            yield return (anchor + "-steps", L("Steps"));
            if (testCase.Detail is { } detail && HasLinks(detail)) yield return (anchor + "-links", L("Links"));
            if (testCase.Detail?.Points is not null) yield return (anchor + "-points", L("TestPoints"));
            int diagnostics = testCase.Diagnostics.Count + testCase.DetailDiagnostics.Count;
            if (diagnostics > 0) yield return (anchor + "-diagnostics", L("Diagnostics") + " " + diagnostics.ToString(Culture));
        }

        private static bool HasParameters(TestCaseReportModel testCase) =>
            testCase.Parameters.Names.Count > 0 || testCase.Parameters.SharedParameterSets.Count > 0;

        private static bool HasLinks(AdoTestCaseDetail detail) => detail.Links.Count > 0 || detail.Hyperlinks.Count > 0 || detail.Attachments.Count > 0;

        private static bool HasFailedPoint(AdoTestCaseDetail? detail) =>
            detail?.Points is { } points && points.Any(static point => PointStatus(point) == AdoTestHistoryOutcome.Failed);

        // Absent metadata is omitted, not shown blank (§12.2).
        private void Metadata(TestCaseReportModel testCase)
        {
            AdoTestCaseDetail? detail = testCase.Detail;
            W("<dl class=\"metadata-grid\">");
            Field(L("Revision"), testCase.Rev.ToString(Culture)); Field(L("WorkItemType"), testCase.WorkItemType); Field(L("State"), testCase.State);
            Field(L("Priority"), testCase.Priority?.ToString(Culture)); Field(L("AutomationStatus"), testCase.AutomationStatus);
            Field(M(AdoMessage.TestReportStorage), detail?.AutomatedTestStorage); Field(L("AutomatedTestType"), detail?.AutomatedTestType);
            Field(L("AreaPath"), testCase.AreaPath); Field(L("IterationPath"), testCase.IterationPath); Field(L("AssignedTo"), testCase.AssignedTo?.DisplayName);
            Field(L("ChangedDate"), Date(testCase.ChangedDate)); Field(L("ChangedBy"), testCase.ChangedBy?.DisplayName);
            Field(L("CreatedDate"), Date(detail?.CreatedDate)); Field(L("CreatedBy"), detail?.CreatedBy?.DisplayName);
            Field(L("Project"), testCase.Project);
            if (testCase.Suite is { } suite)
            {
                if (suite.PlanWebUrl is { } plan) FieldLink(L("TestPlan"), plan, suite.PlanName); else Field(L("TestPlan"), suite.PlanName);
                string path = ReportHeader.SuitePathText(suite.SuitePath.Count > 0 ? suite.SuitePath : [suite.SuiteName]);
                if (suite.WebUrl is { } web) FieldLink(L("TestSuite"), web, path); else Field(L("TestSuite"), path);
            }
            if (Multiple) Field(L("StepCount"), testCase.StepCount.ToString(Culture));
            if (testCase.Status == AdoTestCaseStatus.Partial)
                Field(L("Diagnostics"), F(AdoMessage.PartialTestCase, testCase.Id.ToString(Culture), testCase.ErrorCount, testCase.WarningCount, testCase.InformationCount));
            W("</dl>\n");
        }

        private void Parameters(TestCaseReportModel testCase, string anchor)
        {
            if (!HasParameters(testCase)) return;
            AdoTestParameters parameters = testCase.Parameters;
            W("<section class=\"parameters\" aria-labelledby=\"" + anchor + "-parameters\">\n<h3 class=\"section-heading\" id=\"" + anchor + "-parameters\">"); T(L("Parameters"));
            if (parameters.Rows.Count > 0) { W(" <span class=\"count\">"); T(parameters.Rows.Count.ToString(Culture)); W("</span>"); }
            W("</h3>\n");
            foreach (AdoSharedParameterInfo shared in parameters.SharedParameterSets)
            {
                string title = (shared.Title ?? L("SharedParameters")) + " (#" + shared.Id.ToString(Culture) + ")";
                W("<p class=\"shared-parameters\">");
                if (shared.WebUrl is { } url) Link(url, title); else H(title);
                W("</p>\n");
            }
            if (parameters.Names.Count > 0)
            {
                if (parameters.Rows.Count > 0)
                {
                    // Enabled by the script: the steps then show the values of one iteration in place of the names.
                    W("<label class=\"interactive iteration-select\" data-enhance hidden>"); T(L("ParameterValues")); W(" <select data-iteration><option value=\"\">");
                    T(L("ParameterNames")); W("</option>");
                    for (int index = 0; index < parameters.Rows.Count; index++) { W("<option value=\"" + N(index) + "\">"); T(F(AdoMessage.ReportIteration, index + 1)); W("</option>"); }
                    W("</select></label>\n");
                }
                W("<div class=\"table-scroll\" role=\"region\" tabindex=\"0\" aria-labelledby=\"" + anchor + "-parameters\">\n<table class=\"parameters-table\">\n<thead><tr>");
                foreach (string name in parameters.Names) { W("<th scope=\"col\">"); H(name); W("</th>"); }
                W("</tr></thead>\n<tbody data-parameters>\n");
                foreach (IReadOnlyDictionary<string, string> row in parameters.Rows)
                {
                    W("<tr>");
                    foreach (string name in parameters.Names) { W("<td>"); H(row.GetValueOrDefault(name, "")); W("</td>"); }
                    W("</tr>\n");
                }
                W("</tbody>\n</table>\n</div>\n");
            }
            W("</section>\n");
        }

        private void Steps(TestCaseReportModel testCase, string anchor, bool filtered)
        {
            IReadOnlyList<string> names = testCase.Parameters.Names;
            W("<h3 class=\"section-heading\" id=\"" + anchor + "-steps\">"); T(L("Steps")); W(" <span class=\"count\">"); T(testCase.StepCount.ToString(Culture)); W("</span></h3>\n");
            if (filtered) { W("<p data-no-matches hidden>"); T(L("NoMatchingSteps")); W("</p>\n"); }
            W("<section class=\"steps\" data-step-count=\"" + N(testCase.Rows.Count) + "\">\n");
            int groups = 0, position = 0;
            foreach (ReportRow row in testCase.Rows)
            {
                while (groups > row.Depth) { W("</div>\n"); groups--; }
                string id = anchor + "-step-" + N(++position);
                if (row.Kind is AdoTestStepKind.SharedStep or AdoTestStepKind.Truncated)
                {
                    string title = row.SharedStep?.Title ?? L(row.Kind == AdoTestStepKind.SharedStep ? "SharedSteps" : "Diagnostics");
                    if (row.SharedStep is not null) title += " (#" + row.SharedStep.Id.ToString(Culture) + ")";
                    W("<div class=\"step-card shared-banner" + (!row.IsExpanded ? " warning" : "") + "\" id=\"" + id + "\"><h4><span class=\"outline-number\">"); T(row.Number); W("</span> ▸ ");
                    if (row.SharedStep?.WebUrl is { } url) Link(url, title); else H(title);
                    W("</h4>");
                    if (row.IsExpanded) Collapse(title);
                    if (row.DiagnosticMessage is not null) { W("<p>"); H(row.DiagnosticMessage); W("</p>"); }
                    W("</div>\n");
                    if (row.IsExpanded) { W("<div class=\"shared-group\">\n"); groups++; }
                }
                else
                {
                    W("<div class=\"step-card\" id=\"" + id + "\">\n<h4 class=\"step-number\"><span class=\"outline-number\">"); T(row.Number); W("</span> "); T(L("Step")); W("</h4>\n<div class=\"step-columns\">\n");
                    W("<div class=\"action\"><h5>"); T(L("Action")); W("</h5>"); W(Text(row.Action, row.ActionSource, names)); W("</div>\n");
                    W("<div class=\"expected\"><h5>"); T(L("ExpectedResult")); W("</h5>"); W(Text(row.ExpectedResult, row.ExpectedResultSource, names)); W("</div>\n</div>\n</div>\n");
                }
            }
            while (groups-- > 0) W("</div>\n");
            W("</section>\n");
        }

        // Formatted source keeps its structure (lists, tables, emphasis); other text keeps its lines.
        // Both link http, https and mailto addresses and mark the declared parameters.
        private string Text(string plain, string? source, IReadOnlyList<string> names) => RichTextHtmlRenderer.IsSourceOf(source, plain)
            ? "<div class=\"rich-text\">" + RichTextHtmlRenderer.Render(source!, Culture,
                run => ContentLinks.Html(run, segment => MarkParameters(segment, names, SinkEncoding.Attribute))) + "</div>"
            : ContentLinks.Html(plain, segment => MarkParameters(segment, names, SinkEncoding.Html));

        private void Links(AdoTestCaseDetail detail, string anchor)
        {
            W("<section class=\"links\">\n<h3 class=\"section-heading\" id=\"" + anchor + "-links\">"); T(L("Links")); W(" <span class=\"count\">");
            T((detail.Links.Count + detail.Hyperlinks.Count + detail.Attachments.Count).ToString(Culture)); W("</span></h3>\n");
            if (detail.Links.Count > 0)
            {
                W("<div class=\"table-scroll\"><table class=\"links-table\">\n<thead><tr>");
                foreach (string key in new[] { "LinkType", "WorkItem", "Title", "State" }) ColumnHead(L(key));
                W("</tr></thead>\n<tbody>\n");
                foreach (AdoLinkedWorkItem link in detail.Links)
                {
                    W("<tr data-link=\"" + N(link.Id) + "\"><td>"); T(link.LinkName ?? link.LinkType);
                    if (link.Comment is not null) { W(" <span class=\"text-muted\">"); H(link.Comment); W("</span>"); }
                    W("</td><td class=\"col-item\">"); Link(link.WebUrl, (link.WorkItemType ?? L("WorkItem")) + " #" + link.Id.ToString(Culture));
                    W("</td><td>"); if (link.Title is not null) H(link.Title);
                    W("</td><td>"); T(link.State); W("</td></tr>\n");
                }
                W("</tbody>\n</table></div>\n");
            }
            if (detail.Hyperlinks.Count > 0)
            {
                W("<div class=\"link-list\"><span class=\"strip-label\">"); T(L("Hyperlinks")); W("</span><ul>");
                foreach (AdoTestCaseHyperlink hyperlink in detail.Hyperlinks)
                {
                    // Linked only for http, https and mailto, with the whole address as the link text.
                    W("<li>"); W(ContentLinks.Html(hyperlink.Url));
                    if (hyperlink.Comment is not null) { W(" <span class=\"text-muted\">"); H(hyperlink.Comment); W("</span>"); }
                    W("</li>");
                }
                W("</ul></div>\n");
            }
            if (detail.Attachments.Count > 0)
            {
                // Names only: the files stay in Azure DevOps, on the Test Case.
                W("<div class=\"link-list\"><span class=\"strip-label\">"); T(M(AdoMessage.TestReportAttachments)); W("</span><ul>");
                foreach (AdoTestCaseAttachment attachment in detail.Attachments)
                {
                    W("<li>"); T(attachment.Name);
                    if (attachment.Size is { } size) { W(" <span class=\"attachment-size\">"); T(F(AdoMessage.TestReportBytes, size)); W("</span>"); }
                    if (attachment.Comment is not null) { W(" <span class=\"text-muted\">"); H(attachment.Comment); W("</span>"); }
                    W("</li>");
                }
                W("</ul></div>\n");
            }
            W("</section>\n");
        }

        private void Points(IReadOnlyList<AdoTestPoint> points, string anchor)
        {
            W("<section class=\"points\">\n<h3 class=\"section-heading\" id=\"" + anchor + "-points\">"); T(L("TestPoints")); W(" <span class=\"count\">");
            T(points.Count.ToString(Culture)); W("</span></h3>\n");
            if (points.Count == 0) { W("<p class=\"text-muted\">"); T(L("NoTestPoints")); W("</p>\n</section>\n"); return; }
            W("<div class=\"table-scroll\"><table class=\"points-table\">\n<thead><tr>");
            foreach (string label in new[] { L("TestPlan"), L("TestSuite"), L("Configuration"), L("LastOutcome"), L("Tester"), M(AdoMessage.TestReportRun), L("LastUpdated") })
                ColumnHead(label);
            W("</tr></thead>\n<tbody>\n");
            foreach (AdoTestPoint point in points)
            {
                AdoTestHistoryOutcome status = PointStatus(point);
                W("<tr data-point=\"" + N(point.Id) + "\" data-outcome=\"" + StatusPresentation.Css(status) + "\"><td>");
                Link(point.PlanWebUrl, point.PlanName ?? point.PlanId.ToString(Culture)); W("</td><td>");
                Link(point.SuiteWebUrl, point.SuiteName ?? point.SuiteId.ToString(Culture)); W("</td><td>"); T(point.ConfigurationName); W("</td><td class=\"col-outcome\">");
                StatusPresentation.Write(writer, status, Culture);
                // The server's outcome shows only when it adds to the badge, such as Blocked or Paused.
                if (point.Outcome is { } outcome && status == AdoTestHistoryOutcome.Other) { W(" <span class=\"text-muted\">"); T(outcome); W("</span>"); }
                W("</td><td>"); T(point.Tester?.DisplayName); W("</td><td>");
                if (point is { LastRunWebUrl: { } run, LastRunId: { } runId }) Link(run, runId.ToString(Culture));
                W("</td><td>"); T(Date(point.LastUpdated)); W("</td></tr>\n");
            }
            W("</tbody>\n</table></div>\n</section>\n");
        }

        private static AdoTestHistoryOutcome PointStatus(AdoTestPoint point) => !point.HasRun ? AdoTestHistoryOutcome.NotRun : point.OutcomeClass switch
        {
            AdoTestOutcomeClass.Pass => AdoTestHistoryOutcome.Passed, AdoTestOutcomeClass.Failure => AdoTestHistoryOutcome.Failed, _ => AdoTestHistoryOutcome.Other,
        };

        // One count per outcome, failures first. A point that was never run and one with another
        // outcome share a glyph, so the visible text names them.
        private void PointSummary(IReadOnlyList<AdoTestPoint>? points)
        {
            if (points is null || points.Count == 0) { W("<span class=\"text-muted\">—</span>"); return; }
            foreach (AdoTestHistoryOutcome status in new[] { AdoTestHistoryOutcome.Failed, AdoTestHistoryOutcome.Passed, AdoTestHistoryOutcome.Other, AdoTestHistoryOutcome.NotRun })
            {
                int count = points.Count(point => PointStatus(point) == status);
                if (count == 0) continue;
                bool named = status is AdoTestHistoryOutcome.Other or AdoTestHistoryOutcome.NotRun;
                W("<span class=\"point-count status-" + StatusPresentation.Css(status) + "\">");
                if (named) T(StatusPresentation.Label(status, Culture));
                else { W("<span aria-hidden=\"true\">" + StatusPresentation.Glyph(status) + "</span><span class=\"sr-only\">"); T(StatusPresentation.Label(status, Culture)); W("</span>"); }
                W(" "); T(count.ToString(Culture)); W("</span>");
            }
        }

        private void Diagnostics(TestCaseReportModel testCase, string anchor)
        {
            if (testCase.Diagnostics.Count + testCase.DetailDiagnostics.Count == 0) return;
            W("<section class=\"diagnostics\" aria-labelledby=\"" + anchor + "-diagnostics\">\n<h3 class=\"section-heading\" id=\"" + anchor + "-diagnostics\">"); T(L("Diagnostics")); W("</h3>\n");
            Dictionary<string, int> cards = new(StringComparer.Ordinal);
            for (int index = 0; index < testCase.Rows.Count; index++) cards.TryAdd(testCase.Rows[index].Number, index + 1);
            foreach (AdoDiagnostic diagnostic in testCase.Diagnostics.Concat(testCase.DetailDiagnostics))
            {
                W("<div class=\"diagnostic\" data-severity=\""); T(diagnostic.Severity.ToString().ToLowerInvariant()); W("\" data-diagnostic=\""); T(diagnostic.Code); W("\"><strong>");
                T(M(diagnostic.Severity switch
                {
                    AdoDiagnosticSeverity.Error => AdoMessage.TestReportError, AdoDiagnosticSeverity.Warning => AdoMessage.TestReportWarning, _ => AdoMessage.TestReportInfo,
                })); W(" · "); T(diagnostic.Code); W("</strong>");
                // A diagnostic of a step links to its card.
                if (diagnostic.StepNumber is { } number && cards.TryGetValue(number, out int card))
                { W(" <a href=\"#" + anchor + "-step-" + N(card) + "\">"); T(L("Step") + " " + number); W("</a>"); }
                W("<p>"); H(diagnostic.Message); W("</p></div>\n");
            }
            W("</section>\n");
        }

        // The server, the collection and the times close the page, as in the failed-test report.
        private void Footer(TestCaseReportModel? only)
        {
            W("<dl class=\"secondary-line\">");
            Field(L("GeneratedAt"), model.GeneratedAt.ToString("D", Culture) + " " + model.GeneratedAt.ToString("HH:mm zzz", Culture));
            if (only is not null) Field(L("RetrievedAt"), Date(only.RetrievedAt));
            Field(L("ToolkitVersion"), model.ToolkitVersion); Field(L("Server"), model.ServerUri.AbsoluteUri); Field(L("Collection"), model.CollectionUri.AbsoluteUri);
            W("</dl>\n");
        }

        private static string StatusCss(AdoTestCaseStatus status) => status == AdoTestCaseStatus.Partial ? "partial" : "complete";

        private void StatusBadge(AdoTestCaseStatus status)
        {
            W("<span class=\"status-badge status-" + StatusCss(status) + "\"><span aria-hidden=\"true\">" + (status == AdoTestCaseStatus.Partial ? "!" : "✓") + "</span> ");
            T(L(status.ToString())); W("</span>");
        }

        private void Chip(string css, string label, int count)
        {
            W("<span class=\"count-chip status-" + css + "\"><span class=\"count-label\">"); T(label);
            W("</span><strong class=\"count-value\">"); T(count.ToString(Culture)); W("</strong></span>");
        }

        private void ColumnHead(string label) { W("<th scope=\"col\">"); T(label); W("</th>"); }

        private void Field(string label, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            W("<div><dt>"); T(label); W("</dt><dd>"); T(value); W("</dd></div>");
        }

        private void FieldLink(string label, Uri uri, string text)
        { W("<div><dt>"); T(label); W("</dt><dd>"); Link(uri, text); W("</dd></div>"); }

        // A link into Azure DevOps, built from the connection and IDs (§12.2).
        private void Link(Uri uri, string text, bool glyph = true)
        {
            W("<a rel=\"noreferrer\" href=\""); T(uri.AbsoluteUri); W("\">"); H(text);
            if (glyph) StatusPresentation.ExternalGlyph(writer, Culture);
            W("</a>");
        }

        private void Button(string action, string label, string? description = null)
        {
            W("<button type=\"button\" class=\"interactive\" data-enhance hidden data-action=\"" + action + "\"");
            if (description is not null) { W(" aria-label=\""); T(label + " — " + description); W("\""); }
            W(">"); T(label); W("</button>");
        }

        // Shows or hides the body of a case, or the steps of a Shared Steps group.
        private void Collapse(string title)
        {
            W("<button type=\"button\" class=\"interactive collapse-toggle\" data-enhance hidden data-action=\"toggle\" aria-expanded=\"true\" aria-label=\"");
            T(L("ShowHide") + " — " + SinkEncoding.NormalizeLines(title).Replace('\n', ' '));
            W("\"><span class=\"when-open\" aria-hidden=\"true\">▾</span><span class=\"when-closed\" aria-hidden=\"true\">▸</span></button>");
        }

        private void Toggle(string name, string label)
        { W("<label><input type=\"checkbox\" data-toggle=\"" + name + "\">"); T(label); W("</label>\n"); }
    }
}
