using System.Text;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.Reporting.Highlighting;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

public static class HtmlTestFailureRenderer
{
    public static void Render(TestFailureReportModel model, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);
        new Document(model, writer).Write();
    }

    private sealed class Document(TestFailureReportModel model, TextWriter writer)
    {
        private CultureInfo Culture => model.Culture;
        private Uri Collection => model.Build.CollectionUri;
        private string Project => model.Build.TeamProject;
        private void W(string value) => writer.Write(value);
        private void T(string? value) => W(SinkEncoding.Attribute(value ?? string.Empty));
        private string L(string key) => model.Labels[key];
        private string M(AdoMessage key) => Messages.Get(key, Culture);
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Anchor(AdoTestFailure failure) => "f-" + N(failure.Ordinal);
        private static string Anchor(AdoTestFailure failure, AdoTestAttempt attempt) => Anchor(failure) + "-a" + N(attempt.Number);
        private string? Duration(TimeSpan? value) => value.HasValue ? Messages.Get(AdoMessage.TestReportSeconds, Culture, value.Value.TotalSeconds) : null;
        private string? Date(DateTimeOffset? value) => value?.ToString("g", Culture);
        private static int Attachments(AdoTestFailure failure) => failure.Attempts.Sum(a => a.Attachments.Count);
        private static AdoTestHistoryOutcome Status(AdoTestFailure failure) => failure.Classification == AdoTestFailureClassification.Flaky
            ? AdoTestHistoryOutcome.Flaky : AdoTestHistoryOutcome.Failed;
        private void Badge(AdoTestFailure failure) => StatusPresentation.Write(writer, Status(failure), Culture);
        private void Heading(int level, string label) { W("<h" + N(level) + ">"); T(label); W("</h" + N(level) + ">\n"); }

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
            W("<main id=\"report-content\">\n<section class=\"history-panel\" id=\"history\">\n"); Heading(2, L("History"));
            RunHistoryChart.Write(writer, model.History, Collection, Project, Culture); W("\n</section>\n");
            Index();
            foreach (AdoTestFailure failure in model.Failures) Card(failure);
            Diagnostics();
            W("</main>\n<dialog class=\"image-viewer\" aria-label=\""); T(L("Image")); W("\">\n");
            Button("close", L("Close")); W("<div data-image-slot></div></dialog>\n");
            W("<p class=\"copy-feedback\" role=\"status\" data-copy-status hidden data-label-done=\""); T(L("Copied")); W("\" data-label-selected=\""); T(L("SelectCopy")); W("\"></p>\n");
            W("<script>"); W(script); W("</script>\n</body>\n</html>\n");
        }

        private void Header()
        {
            W("<a class=\"skip-link\" href=\"#failure-index\">"); T(L("Index")); W("</a>\n<header class=\"top-bar\"><div class=\"top-bar-inner\">\n");
            W("<div class=\"title-line\"><div><span class=\"report-brand\">"); T(M(AdoMessage.ReportBrand)); W("</span><h1>"); T(L("Heading")); W("</h1></div>");
            W("<nav class=\"section-links\" aria-label=\""); T(M(AdoMessage.ReportNavigation)); W("\"><a href=\"#history\">"); T(L("History"));
            W("</a><a href=\"#failure-index\">"); T(L("Index")); W("</a>");
            if (model.Diagnostics.Count > 0 || model.Status == AdoTestFailureStatus.Partial)
            { W("<a href=\"#diagnostics\">"); T(M(AdoMessage.ReportDiagnostics)); W("</a>"); }
            W("</nav></div>\n<div class=\"build-line\"><span class=\"pipeline-name\">");
            Link(model.DefinitionUrl, model.Build.Definition.Name); W("</span><span class=\"build-number\">"); Link(model.BuildUrl, model.Build.BuildNumber); W("</span></div>\n");
            W("<div class=\"build-context\">");
            if (model.Build.SourceBranch is not null) { W("<code>"); T(model.Build.SourceBranch); W("</code>"); }
            if (model.Build.SourceVersion is { } version)
            {
                W("<span class=\"commit\" title=\""); T(L("Commit")); W("\">");
                if (model.CommitUrl is not null) Link(model.CommitUrl, version[..7]); else T(version);
                W("</span>");
            }
            if (model.Build.Result is not null) { W("<span>"); T(model.Build.Result); W("</span>"); }
            if (model.Build.FinishTime is not null) { W("<span>"); T(L("Finished") + ": " + Date(model.Build.FinishTime)); W("</span>"); }
            W("</div>\n<div class=\"count-line\">");
            foreach ((string css, string glyph, string label, int count) in new[] { ("failed", "✕", L("Failed"), model.FailedCount),
                ("flaky", "≈", L("Flaky"), model.FlakyCount), ("attachments", "", L("Attachments"), model.Failures.Sum(Attachments)) })
            {
                W("<span class=\"count-chip status-" + css + "\"><span>"); T((glyph.Length > 0 ? glyph + " " : "") + label);
                W("</span><strong>"); T(count.ToString(Culture)); W("</strong></span>");
            }
            if (model.Status == AdoTestFailureStatus.Partial)
            { W("<a class=\"partial-link\" href=\"#diagnostics\"><span aria-hidden=\"true\">!</span> "); T(M(AdoMessage.ReportPartial)); W("</a>"); }
            W("<span class=\"results-link\">"); Link(model.ResultsUrl, L("Result")); W("</span></div>\n</div></header>\n");
            W("<div class=\"report-context\"><dl class=\"secondary-line\">");
            Field(M(AdoMessage.ReportGeneratedAt), Date(model.GeneratedAt)); Field(M(AdoMessage.ReportToolkitVersion), model.ToolkitVersion);
            Field(M(AdoMessage.ReportServer), Collection.GetLeftPart(UriPartial.Authority)); Field(M(AdoMessage.ReportCollection), Collection.AbsolutePath);
            Field(M(AdoMessage.ReportProject), Project); W("</dl></div>\n");
        }

        private void Index()
        {
            W("<nav class=\"failure-navigation\" id=\"failure-index\" aria-label=\""); T(L("Index")); W("\">\n"); Heading(2, L("Index"));
            W("<div class=\"interactive filter-controls\" data-enhance hidden>\n<label>"); T(L("Filter")); W(" <input type=\"search\" data-filter></label>\n");
            Toggle("failed", L("Failed"), true); Toggle("flaky", L("Flaky"), true); Toggle("attachments", L("HasAttachments"), false);
            Button("expand", L("ExpandAll")); Button("collapse", L("CollapseAll"));
            W("<span class=\"filter-result-count\" role=\"status\" aria-live=\"polite\" data-filter-count data-label-count=\""); T(L("FilterCount")); W("\"></span>");
            W("<p class=\"text-muted keyboard-hint\">"); T(L("NavigationHint")); W("</p></div>\n");
            W("<p data-no-matches hidden>"); T(L("NoMatches")); W("</p>\n");
            if (model.Failures.Count == 0) { W("<p>"); T(L("NoFailures")); W("</p>\n"); }
            W("<ol class=\"failure-index\">\n");
            foreach (AdoTestFailure failure in model.Failures)
            {
                W("<li data-index-for=\"" + Anchor(failure) + "\"><div class=\"index-heading\">"); Badge(failure);
                W(" <a href=\"#" + Anchor(failure) + "\">"); T(failure.ShortName); W("</a>");
                if (failure.TestName is { } name && name.LastIndexOf('.') is int dot && dot > 0)
                { W(" <span class=\"text-muted\">"); T(name[..dot]); W("</span>"); }
                W("</div><div class=\"index-strips\"><div><span class=\"strip-label\">"); T(L("Attempts")); W("</span>");
                AttemptStrip.Write(writer, failure.Attempts, Culture, Anchor(failure)); W("</div>");
                if (failure.History.Count > 0)
                { W("<div><span class=\"strip-label\">"); T(L("History")); W("</span>"); HistoryStrip.Write(writer, failure.History, Collection, Project, Culture); W("</div>"); }
                W("</div>");
                W("<div class=\"index-footer\">");
                if (failure.Attempts.Count > 0 && failure.Attempts[^1].Duration.HasValue)
                { W("<span>"); T(L("Duration") + " · " + Duration(failure.Attempts[^1].Duration)); W("</span>"); }
                TestCase(failure.TestCase);
                W("<span>"); T(L("Attachments") + " " + Attachments(failure).ToString(Culture)); W("</span></div></li>\n");
            }
            W("</ol>\n</nav>\n");
        }

        private void Card(AdoTestFailure failure)
        {
            string anchor = Anchor(failure);
            W("<article class=\"card failure-card\" id=\"" + anchor + "\" tabindex=\"-1\" data-attempt-count=\"" + N(failure.Attempts.Count)
                + "\" data-classification=\"" + StatusPresentation.Css(Status(failure)) + "\" data-attachment-count=\"" + N(Attachments(failure)) + "\">\n");
            W("<header><span class=\"failure-number\" aria-hidden=\"true\">" + N(failure.Ordinal) + "</span>"); Badge(failure);
            W("<h2 data-short-name>"); T(failure.ShortName); W("</h2></header>\n");
            if (failure.TestName is not null)
            { W("<div class=\"name-section\"><code data-full-name data-copy-value>"); T(failure.TestName); W("</code> "); Button("copy", L("Copy"), description: L("FullName")); W("</div>\n"); }
            W("<dl class=\"metadata-grid\">"); Field(L("Storage"), failure.Storage); Field(M(AdoMessage.ReportTitle), failure.Title);
            Field(L("Owner"), Identity(failure.Owner)); Field(M(AdoMessage.ReportPriority), failure.Priority);
            W("</dl>\n<div class=\"card-links\">"); TestCase(failure.TestCase);
            if (failure.Attempts.Count > 0)
            { AdoTestAttempt last = failure.Attempts[^1]; Link(Result(last.RunId, last.ResultId), L("Result")); Link(AdoWebLinks.TestRun(Collection, Project, last.RunId), L("Run")); }
            foreach (int bug in failure.Attempts.SelectMany(a => a.AssociatedBugIds).Distinct().Where(id => id > 0).Order())
                Link(AdoWebLinks.WorkItem(Collection, Project, bug), L("Bugs") + " #" + bug.ToString(Culture));
            W("</div>\n<div class=\"card-strips\"><div><span class=\"strip-label\">"); T(L("Attempts")); W("</span>");
            AttemptStrip.Write(writer, failure.Attempts, Culture, Anchor(failure)); W("</div>\n");
            if (failure.History.Count > 0)
            { W("<div><span class=\"strip-label\">"); T(L("History")); W("</span>"); HistoryStrip.Write(writer, failure.History, Collection, Project, Culture); W("</div>\n"); }
            W("</div>\n");
            foreach (AdoTestAttempt attempt in failure.Attempts) Attempt(failure, attempt);
            W("<footer class=\"card-footer\"><a href=\"#failure-index\">↑ "); T(L("Index")); W("</a></footer>\n</article>\n");
        }

        private void Attempt(AdoTestFailure failure, AdoTestAttempt attempt)
        {
            W("<details class=\"attempt\" id=\"" + Anchor(failure, attempt) + "\" data-failure-class=\"" + (attempt.OutcomeClass == AdoTestOutcomeClass.Failure ? "true" : "false") + "\"");
            if (attempt.OutcomeClass == AdoTestOutcomeClass.Failure) W(" open");
            W("><summary>"); T(Messages.Get(AdoMessage.TestReportAttemptOf, Culture, attempt.Number, failure.Attempts.Count)); W(" · ");
            Outcome(attempt.Outcome, attempt.OutcomeClass); if (attempt.Duration is not null) { W(" · "); T(Duration(attempt.Duration)); } W("</summary>\n");
            W("<dl class=\"metadata-grid\">");
            Field(L("Source"), L(attempt.Source.ToString())); Field(L("Started"), Date(attempt.StartedDate)); Field(L("Finished"), Date(attempt.CompletedDate));
            Field(L("Duration"), Duration(attempt.Duration)); Field(L("Machine"), attempt.ComputerName);
            FieldLink(L("Run"), AdoWebLinks.TestRun(Collection, Project, attempt.RunId), model.Runs.FirstOrDefault(r => r.Id == attempt.RunId)?.Name ?? attempt.RunId.ToString(Culture));
            FieldLink(L("Result"), Result(attempt.RunId, attempt.ResultId), attempt.ResultId.ToString(Culture));
            Field(M(AdoMessage.ReportId), attempt.SubResultId); Field(L("RunBy"), Identity(attempt.RunBy));
            Field(L("FailureType"), attempt.FailureType); Field(L("Resolution"), attempt.ResolutionState);
            if (attempt.FailingSinceBuildId is > 0) FieldLink(L("FailingSince"), AdoWebLinks.Build(Collection, Project, attempt.FailingSinceBuildId.Value), attempt.FailingSinceBuildId.Value.ToString(Culture));
            foreach (int bug in attempt.AssociatedBugIds.Where(id => id > 0).Distinct()) FieldLink(L("Bugs"), AdoWebLinks.WorkItem(Collection, Project, bug), "#" + bug.ToString(Culture));
            Field(L("Comment"), attempt.Comment); W("</dl>\n");
            Code(attempt.ErrorMessage, CodeLanguage.ErrorMessage); Code(attempt.StackTrace, CodeLanguage.StackTrace);
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
                W("<li id=\"" + Anchor(failure, attempt) + "-att" + N(attachment.Id) + "\" data-attachment=\"" + N(attachment.Id) + "\"");
                if (attachment.DownloadStatus != AdoTestAttachmentStatus.NotRequested)
                    W(" data-download-status=\"" + attachment.DownloadStatus.ToString().ToLowerInvariant() + "\"");
                W(">");
                // The ADO name and size always show and link to the result, so the original stays reachable.
                Link(Result(attachment.RunId, attachment.ResultId), attachment.FileName);
                if (attachment.Size.HasValue) { W(" <span>"); T(Messages.Get(AdoMessage.TestReportBytes, Culture, attachment.Size.Value)); W("</span>"); }
                string? json = LocalAttachment(attachment);
                W("<dl class=\"metadata-grid\">"); Field(L("Comment"), attachment.Comment); Field(L("AttachmentType"), attachment.AttachmentType);
                Field(M(AdoMessage.ReportId), attachment.SubResultId); W("</dl>");
                if (json is not null) Code(json, CodeLanguage.Json);
                W("</li>\n");
            }
            W("</ul>\n");
        }

        // Writes the local link, thumbnail or status note and returns JSON text to show inline.
        // Local names and hrefs are toolkit-generated; the remote name appears only as encoded text.
        private string? LocalAttachment(AdoTestAttachment attachment)
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
            if (file is null && note is null) return null;
            bool verified = attachment.DownloadStatus == AdoTestAttachmentStatus.Downloaded;
            W("<div class=\"attachment-local\">");
            if (file is { } local && verified && attachment.Kind == AdoTestAttachmentKind.Png)
            {
                W("<div class=\"thumbnail-grid\"><a class=\"thumbnail\" data-local-file href=\""); T(local.Href);
                W("\"><img data-local-file src=\""); T(local.Href); W("\" loading=\"lazy\" alt=\""); T(Messages.Get(AdoMessage.TestReportImageOf, Culture, attachment.FileName)); W("\"></a></div>");
            }
            else if (file is { } other)
            {
                W("<a class=\"local-file\" data-local-file href=\""); T(other.Href); W("\">");
                T(verified && attachment.Kind == AdoTestAttachmentKind.Html ? L("TestOutputHtml") : L("LocalCopy"));
                W("</a> <code>"); T(other.Name); W("</code> <span>"); T(Messages.Get(AdoMessage.TestReportBytes, Culture, other.Length)); W("</span>");
            }
            if (note is not null) { W("<p class=\"attachment-status\">"); T(note); W("</p>"); }
            W("</div>");
            if (file is not { } source || !verified || attachment.Kind != AdoTestAttachmentKind.Json
                || source.Length > model.LocalAttachments!.MaximumInlineJsonBytes) return null;
            // Display only: replacement decoding, and a failed format falls back to the lexer's plain tokens.
            string text = File.ReadAllText(Path.Combine(model.LocalAttachments.SourceFolder, source.Name), new UTF8Encoding(false, false));
            JsonLexer.TryFormat(text, (int)Math.Min(int.MaxValue, model.LocalAttachments.MaximumInlineJsonBytes), out string formatted);
            return formatted;
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
            Button("copy", L("Copy"), description: L(language switch { CodeLanguage.StackTrace => "StackTrace", CodeLanguage.Json => "Json", _ => "ErrorMessage" }));
            Button("wrap", L("Wrap"), true);
            if (language == CodeLanguage.StackTrace) Button("framework", L("Framework"), true);
            W("</div>"); HighlightedCodeWriter.Write(writer, text, language, Culture); W("</div>\n");
        }

        private void TestCase(AdoTestCaseLink? testCase)
        {
            if (testCase is not { Id: > 0 }) return;
            W("<span class=\"test-case-link\">");
            Link(AdoWebLinks.WorkItem(Collection, Project, testCase.Id), "#" + testCase.Id.ToString(Culture)
                + (testCase.IsResolved && testCase.Title is not null ? " " + testCase.Title : ""));
            if (testCase.IsResolved && testCase.State is not null) { W(" <span>"); T(testCase.State); W("</span>"); }
            W("</span>");
        }

        private void Diagnostics()
        {
            if (model.Diagnostics.Count == 0 && model.Status != AdoTestFailureStatus.Partial) return;
            W("<section id=\"diagnostics\">\n"); Heading(2, M(AdoMessage.ReportDiagnostics));
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
            W("<div class=\"metadata-item\"><dt>"); T(label); W("</dt><dd>");
            T(value is IFormattable formatted ? formatted.ToString(null, Culture) : value.ToString()); W("</dd></div>");
        }

        private void FieldLink(string label, Uri uri, string text)
        { W("<div class=\"metadata-item\"><dt>"); T(label); W("</dt><dd>"); Link(uri, text); W("</dd></div>"); }

        private void Link(Uri uri, string text)
        { W("<a rel=\"noreferrer\" href=\""); T(uri.AbsoluteUri); W("\">"); T(text); StatusPresentation.ExternalGlyph(writer, Culture); W("</a>"); }

        private Uri Result(int run, int result) => AdoWebLinks.BuildTestResult(Collection, Project, model.Build.Id, run, result);
        private static string? Identity(AdoIdentityRef? identity) => identity is null ? null : identity.DisplayName
            + (identity.UniqueName is null ? "" : " <" + identity.UniqueName + ">");

        private void Outcome(string outcome, AdoTestOutcomeClass outcomeClass)
        {
            AdoTestHistoryOutcome status = outcomeClass switch { AdoTestOutcomeClass.Pass => AdoTestHistoryOutcome.Passed,
                AdoTestOutcomeClass.Failure => AdoTestHistoryOutcome.Failed, _ => AdoTestHistoryOutcome.Other };
            StatusPresentation.Write(writer, status, Culture);
            if (!string.Equals(outcome, StatusPresentation.Label(status, Culture), StringComparison.Ordinal))
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
