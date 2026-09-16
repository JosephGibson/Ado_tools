using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting.Markdown;

public static class MarkdownTestCaseRenderer
{
    public static void Render(ReportDocumentModel model, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);
        if (!model.IsMultiCase)
        {
            WriteCase(model.Cases[0], writer);
            return;
        }
        WriteCover(model, writer);
        int index = 0;
        foreach (TestCaseReportModel testCase in model.Cases)
        {
            // Explicit anchors keep TOC links independent of renderer-specific heading slugs.
            writer.Write("<a id=\"" + model.Contents[index++].Anchor + "\"></a>\n\n");
            WriteCase(testCase, writer);
            writer.Flush();
        }
    }

    private static void WriteCover(ReportDocumentModel model, TextWriter writer)
    {
        string Label(string key) => SinkEncoding.Markdown(model.Labels[key]);
        writer.Write("# " + Label("DocumentHeading") + "\n\n");
        foreach ((string label, string value, Uri? url) in ReportHeader.CoverFields(model))
            writer.Write("**" + Label(label) + ":** " + (url is null ? SinkEncoding.Markdown(value) : ContentLinks.MarkdownLink(value, url)) + "\n\n");
        writer.Write("## " + Label("Contents") + "\n\n");
        foreach ((string group, IReadOnlyList<ReportContentsEntry> entries) in ReportHeader.Groups(model))
        {
            writer.Write("**" + SinkEncoding.Markdown(group) + "**\n\n");
            int position = 0;
            foreach (ReportContentsEntry entry in entries)
            {
                string text = entry.Id.ToString(model.Culture) + " · " + SinkEncoding.NormalizeLines(entry.Title).Replace('\n', ' ');
                writer.Write((++position).ToString(CultureInfo.InvariantCulture) + ". [" + SinkEncoding.Markdown(text) + "](#" + entry.Anchor + ") — " +
                    Label(entry.Status.ToString()) + "\n");
            }
            writer.Write("\n");
        }
    }

    private static void WriteCase(TestCaseReportModel model, TextWriter writer)
    {
        string Label(string key) => SinkEncoding.Markdown(model.Labels[key]);
        writer.Write("# " + Label("Heading") + " — " + ContentLinks.MarkdownLink(model.Title, model.WebUrl).Replace("\n", "<br>", StringComparison.Ordinal) + "\n\n");
        foreach ((string label, string value, Uri? url) in ReportHeader.Fields(model))
        {
            if (value.Length > 0) writer.Write("**" + Label(label) + ":** " + (url is null ? SinkEncoding.Markdown(value) : ContentLinks.MarkdownLink(value, url)) + "\n\n");
        }
        if (model.Parameters.Names.Count > 0 || model.Parameters.SharedParameterSets.Count > 0)
        {
            writer.Write("## " + Label("Parameters") + "\n\n");
            foreach (AdoSharedParameterInfo shared in model.Parameters.SharedParameterSets)
            {
                string title = (shared.Title ?? model.Labels["SharedParameters"]) + " (#" + shared.Id.ToString(model.Culture) + ")";
                writer.Write((shared.WebUrl is null ? SinkEncoding.Markdown(title) : ContentLinks.MarkdownLink(title, shared.WebUrl)) + "\n\n");
            }
            if (model.Parameters.Names.Count > 0)
            {
                writer.Write("| " + string.Join(" | ", model.Parameters.Names.Select(TableText)) + " |\n");
                writer.Write("| " + string.Join(" | ", model.Parameters.Names.Select(_ => "---")) + " |\n");
                foreach (IReadOnlyDictionary<string, string> row in model.Parameters.Rows)
                    writer.Write("| " + string.Join(" | ", model.Parameters.Names.Select(name => TableText(row.GetValueOrDefault(name, "")))) + " |\n");
                writer.Write("\n");
            }
        }
        foreach (ReportRow row in model.Rows)
        {
            // Every row, including a banner, owns a heading for streaming count validation.
            writer.Write("### " + SinkEncoding.Markdown(row.Number) + " · " + Label("Step") + "\n\n");
            if (row.Kind is AdoTestStepKind.SharedStep or AdoTestStepKind.Truncated)
            {
                string title = row.SharedStep?.Title ?? model.Labels[row.Kind == AdoTestStepKind.SharedStep ? "SharedSteps" : "Diagnostics"];
                if (row.SharedStep is not null) title += " (#" + row.SharedStep.Id.ToString(model.Culture) + ")";
                string banner = row.SharedStep?.WebUrl is Uri url ? ContentLinks.MarkdownLink(title, url) : SinkEncoding.Markdown(title);
                writer.Write("> " + banner.Replace("\n", "\n> ", StringComparison.Ordinal) + "\n\n");
                if (row.DiagnosticMessage is not null) writer.Write("> " + SinkEncoding.Markdown(row.DiagnosticMessage).Replace("\n", "\n> ", StringComparison.Ordinal) + "\n\n");
            }
            else
            {
                writer.Write("**" + Label("Action") + "**\n\n" + ContentLinks.Markdown(row.Action) + "\n\n");
                writer.Write("**" + Label("ExpectedResult") + "**\n\n" + ContentLinks.Markdown(row.ExpectedResult) + "\n\n");
            }
        }
        if (model.Diagnostics.Count > 0)
        {
            writer.Write("## " + Label("Diagnostics") + "\n\n");
            foreach (AdoDiagnostic diagnostic in model.Diagnostics) writer.Write(SinkEncoding.Markdown(diagnostic.Code + ": " + diagnostic.Message) + "\n\n");
        }
    }

    private static string TableText(string text) => SinkEncoding.Markdown(text).Replace("\n", "<br>", StringComparison.Ordinal);
}
