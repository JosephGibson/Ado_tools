using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Reporting.Charts;

public static class HistoryStrip
{
    public static void Write(TextWriter writer, IReadOnlyList<AdoTestHistoryEntry> entries, Uri collectionUri, string teamProject, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(collectionUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamProject);
        writer.Write("<ol class=\"history-strip\" aria-label=\"");
        writer.Write(SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportHistory, culture))); writer.Write("\">");
        foreach (AdoTestHistoryEntry entry in entries)
        {
            writer.Write("<li><a class=\"history-cell\" rel=\"noreferrer\" href=\"");
            writer.Write(SinkEncoding.Attribute(AdoWebLinks.BuildTestResult(collectionUri, teamProject, entry.BuildId).AbsoluteUri));
            writer.Write("\" data-build-id=\""); writer.Write(entry.BuildId.ToString(CultureInfo.InvariantCulture)); writer.Write("\"");
            if (entry.IsCurrent) writer.Write(" aria-current=\"true\"");
            writer.Write("><span class=\"history-build\">"); writer.Write(SinkEncoding.Attribute(entry.BuildNumber)); writer.Write("</span> ");
            StatusPresentation.Write(writer, entry.Outcome, culture);
            if (entry.IsCurrent) { writer.Write(" <span class=\"history-current\">"); writer.Write(SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportThisRun, culture))); writer.Write("</span>"); }
            StatusPresentation.ExternalGlyph(writer, culture);
            writer.Write("</a></li>");
        }
        writer.Write("</ol><ul class=\"status-legend\">");
        foreach (AdoTestHistoryOutcome status in Enum.GetValues<AdoTestHistoryOutcome>())
        {
            writer.Write("<li>"); StatusPresentation.Write(writer, status, culture); writer.Write("</li>");
        }
        writer.Write("</ul>");
    }
}
