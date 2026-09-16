using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting.Html;

public static class HtmlTestCaseRenderer
{
    private static readonly string Css = ReadAsset("testcase-report.css");
    private static readonly string DocumentCss = ReadAsset("testcase-document.css");
    private static readonly string Logo = ReadAsset("adotoolkit-mark.svg");

    public static void Render(ReportDocumentModel model, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);
        bool multiple = model.IsMultiCase;
        writer.Write("<!DOCTYPE html>\n<html lang=\"");
        writer.Write(SinkEncoding.Attribute(model.Culture.Name));
        writer.Write("\" data-case-count=\"");
        writer.Write(model.Cases.Count.ToString(CultureInfo.InvariantCulture));
        writer.Write("\">\n<head>\n<meta charset=\"utf-8\">\n<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\">\n<meta name=\"generator\" content=\"AdoToolkit ");
        writer.Write(SinkEncoding.Attribute(model.ToolkitVersion));
        writer.Write("\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<title>");
        writer.Write(SinkEncoding.Html(model.Labels[multiple ? "DocumentHeading" : "Heading"]));
        writer.Write("</title>\n<style>\n");
        writer.Write(Css);
        if (multiple) writer.Write(DocumentCss);
        writer.Write("</style>\n</head>\n<body>\n");
        if (!multiple)
        {
            WriteCase(model.Cases[0], writer, "report-1", null);
            writer.Write("</body>\n</html>\n");
            return;
        }
        WriteCover(model, writer);
        writer.Write("<main>\n");
        // The case list builds each model on access; each body is written before the next is built.
        int index = 0;
        foreach (TestCaseReportModel testCase in model.Cases)
        {
            string anchor = model.Contents[index++].Anchor;
            WriteCase(testCase, writer, anchor, anchor);
            writer.Flush();
        }
        writer.Write("</main>\n</body>\n</html>\n");
    }

    private static void WriteCover(ReportDocumentModel model, TextWriter writer)
    {
        string Label(string key) => SinkEncoding.Html(model.Labels[key]);
        writer.Write("<header class=\"document-cover\">\n<div class=\"header-top\"><div class=\"brand\">");
        writer.Write(Logo);
        writer.Write("<div class=\"brand-text\"><p class=\"wordmark\">" + Label("Brand") + "</p><p class=\"eyebrow\">" + Label("Heading") + "</p></div></div>");
        writer.Write("<span class=\"status-badge" + (model.PartialCount > 0 ? " partial" : "") + "\">" + Label(model.PartialCount > 0 ? "Partial" : "Complete") + "</span></div>\n<h1>");
        writer.Write(Label("DocumentHeading"));
        writer.Write("</h1>\n<dl>\n");
        foreach ((string label, string value, Uri? url) in ReportHeader.CoverFields(model))
        {
            bool wide = label is "Server" or "Collection" or "TestSuite" or "Source";
            bool technical = label is "Server" or "Collection" or "GeneratedAt" or "CaseCount" or "Complete" or "Partial";
            writer.Write("<div class=\"metadata-field" + (wide ? " metadata-wide" : "") + "\"><dt>" + Label(label) + "</dt><dd" + (technical ? " class=\"technical\"" : "") + ">" +
                (url is null ? SinkEncoding.Html(value) : ContentLinks.HtmlLink(value, url)) + "</dd></div>\n");
        }
        writer.Write("</dl>\n</header>\n");
        writer.Write("<nav class=\"toc\" aria-labelledby=\"contents\">\n<h2 id=\"contents\">" + Label("Contents") + " <span class=\"count\">" +
            model.Contents.Count.ToString(model.Culture) + "</span></h2>\n");
        foreach ((string group, IReadOnlyList<ReportContentsEntry> entries) in ReportHeader.Groups(model))
        {
            writer.Write("<section class=\"toc-group\">\n<h3>" + SinkEncoding.Html(group) + "</h3>\n<ol>\n");
            foreach (ReportContentsEntry entry in entries)
            {
                bool partial = entry.Status == AdoTestCaseStatus.Partial;
                writer.Write("<li><a href=\"#" + entry.Anchor + "\"><span class=\"outline-number\">" + entry.Id.ToString(model.Culture) + "</span> " +
                    SinkEncoding.Html(entry.Title) + "</a><span class=\"status-badge" + (partial ? " partial" : "") + "\">" + Label(entry.Status.ToString()) + "</span></li>\n");
            }
            writer.Write("</ol>\n</section>\n");
        }
        writer.Write("</nav>\n");
    }

    private static void WriteCase(TestCaseReportModel model, TextWriter writer, string anchor, string? id)
    {
        string Label(string key) => SinkEncoding.Html(model.Labels[key]);
        bool hasParameters = model.Parameters.Names.Count > 0 || model.Parameters.SharedParameterSets.Count > 0;
        writer.Write("<article class=\"test-case\"" + (id is null ? "" : " id=\"" + id + "\"") + ">\n<a class=\"skip-link\" href=\"#" + anchor + "-steps\">" + Label("Steps") + "</a>\n");
        writer.Write("<nav class=\"report-nav\" aria-label=\"" + SinkEncoding.Attribute(model.Labels["Navigation"]) + "\">\n");
        writer.Write("<a href=\"#" + anchor + "-overview\">" + Label("Overview") + "</a>\n");
        if (hasParameters) writer.Write("<a href=\"#" + anchor + "-parameters\">" + Label("Parameters") + "</a>\n");
        writer.Write("<a href=\"#" + anchor + "-steps\">" + Label("Steps") + "</a>\n");
        if (model.Diagnostics.Count > 0) writer.Write("<a href=\"#" + anchor + "-diagnostics\">" + Label("Diagnostics") + "</a>\n");
        writer.Write("</nav>\n<header id=\"" + anchor + "-overview\">\n<div class=\"header-top\"><div class=\"brand\">");
        writer.Write(Logo);
        writer.Write("<div class=\"brand-text\"><p class=\"wordmark\">" + Label("Brand") + "</p><p class=\"eyebrow\">");
        writer.Write(Label("Heading"));
        writer.Write("</p></div></div><span class=\"status-badge" + (model.Status == AdoTestCaseStatus.Partial ? " partial" : "") + "\">" + Label(model.Status.ToString()) + "</span></div>\n<h1>");
        writer.Write(ContentLinks.HtmlLink(model.Title, model.WebUrl));
        writer.Write("</h1>\n<dl>\n");
        foreach ((string label, string value, Uri? url) in ReportHeader.Fields(model))
        {
            if (value.Length == 0) continue;
            bool wide = label is "Server" or "Collection" or "Diagnostics";
            bool technical = label is "Id" or "Revision" or "Priority" or "StepCount" or "ToolkitVersion" or "Server" or "Collection" or "AreaPath" or "IterationPath" or "ChangedDate" or "GeneratedAt";
            writer.Write("<div class=\"metadata-field" + (wide ? " metadata-wide" : "") + "\"><dt>" + Label(label) + "</dt><dd" + (technical ? " class=\"technical\"" : "") + ">" +
                (url is null ? SinkEncoding.Html(value) : ContentLinks.HtmlLink(value, url)) + "</dd></div>\n");
        }
        writer.Write("</dl>\n</header>\n");
        if (hasParameters)
        {
            writer.Write("<section class=\"parameters\" aria-labelledby=\"" + anchor + "-parameters\">\n<h2 id=\"" + anchor + "-parameters\">" + Label("Parameters") + "</h2>\n");
            foreach (AdoSharedParameterInfo shared in model.Parameters.SharedParameterSets)
            {
                string title = (shared.Title ?? model.Labels["SharedParameters"]) + " (#" + shared.Id.ToString(model.Culture) + ")";
                writer.Write("<p>" + (shared.WebUrl is null ? SinkEncoding.Html(title) : ContentLinks.HtmlLink(title, shared.WebUrl)) + "</p>\n");
            }
            if (model.Parameters.Names.Count > 0)
            {
                writer.Write("<div class=\"table-scroll\" role=\"region\" tabindex=\"0\" aria-labelledby=\"" + anchor + "-parameters\">\n<table>\n<thead><tr>");
                foreach (string name in model.Parameters.Names) writer.Write("<th scope=\"col\">" + SinkEncoding.Html(name) + "</th>");
                writer.Write("</tr></thead>\n<tbody>\n");
                foreach (IReadOnlyDictionary<string, string> row in model.Parameters.Rows)
                {
                    writer.Write("<tr>");
                    foreach (string name in model.Parameters.Names) writer.Write("<td>" + SinkEncoding.Html(row.GetValueOrDefault(name, "")) + "</td>");
                    writer.Write("</tr>\n");
                }
                writer.Write("</tbody>\n</table>\n</div>\n");
            }
            writer.Write("</section>\n");
        }
        writer.Write("<h2 class=\"section-heading\" id=\"" + anchor + "-steps\">" + Label("Steps") + " <span class=\"count\">" + model.StepCount.ToString(model.Culture) + "</span></h2>\n");
        writer.Write("<section class=\"steps\" data-step-count=\"" + model.Rows.Count.ToString(CultureInfo.InvariantCulture) + "\">\n");
        int groups = 0;
        foreach (ReportRow row in model.Rows)
        {
            while (groups > row.Depth) { writer.Write("</div>\n"); groups--; }
            string number = SinkEncoding.Html(row.Number);
            if (row.Kind is AdoTestStepKind.SharedStep or AdoTestStepKind.Truncated)
            {
                string title = row.SharedStep?.Title ?? model.Labels[row.Kind == AdoTestStepKind.SharedStep ? "SharedSteps" : "Diagnostics"];
                if (row.SharedStep is not null) title += " (#" + row.SharedStep.Id.ToString(model.Culture) + ")";
                writer.Write("<div class=\"step-card shared-banner" + (!row.IsExpanded ? " warning" : "") + "\"><h3><span class=\"outline-number\">" + number + "</span> ▸ " +
                    (row.SharedStep?.WebUrl is Uri url ? ContentLinks.HtmlLink(title, url) : SinkEncoding.Html(title)));
                writer.Write("</h3>");
                if (row.DiagnosticMessage is not null) writer.Write("<p>" + SinkEncoding.Html(row.DiagnosticMessage) + "</p>");
                writer.Write("</div>\n");
                if (row.IsExpanded) { writer.Write("<div class=\"shared-group\">\n"); groups++; }
            }
            else
            {
                writer.Write("<div class=\"step-card\">\n<h3 class=\"step-number\"><span class=\"outline-number\">" + number + "</span> " + Label("Step") + "</h3>\n<div class=\"step-columns\">\n");
                writer.Write("<div class=\"action\"><h4>" + Label("Action") + "</h4>" + ContentLinks.Html(row.Action) + "</div>\n");
                writer.Write("<div class=\"expected\"><h4>" + Label("ExpectedResult") + "</h4>" + ContentLinks.Html(row.ExpectedResult) + "</div>\n</div>\n</div>\n");
            }
        }
        while (groups-- > 0) writer.Write("</div>\n");
        writer.Write("</section>\n");
        if (model.Diagnostics.Count > 0)
        {
            writer.Write("<section class=\"diagnostics\" aria-labelledby=\"" + anchor + "-diagnostics\">\n<h2 id=\"" + anchor + "-diagnostics\">" + Label("Diagnostics") + "</h2>\n<ul>\n");
            foreach (AdoDiagnostic diagnostic in model.Diagnostics) writer.Write("<li><code>" + SinkEncoding.Html(diagnostic.Code) + "</code> " + SinkEncoding.Html(diagnostic.Message) + "</li>\n");
            writer.Write("</ul>\n</section>\n");
        }
        writer.Write("</article>\n");
    }

    private static string ReadAsset(string name)
    {
        using Stream stream = typeof(HtmlTestCaseRenderer).Assembly.GetManifestResourceStream("AdoToolkit.Core.Reporting.Assets." + name)!;
        using StreamReader reader = new(stream);
        return SinkEncoding.NormalizeLines(reader.ReadToEnd());
    }
}
