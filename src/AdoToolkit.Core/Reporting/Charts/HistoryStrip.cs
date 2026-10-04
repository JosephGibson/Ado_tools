using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Reporting.Charts;

// One cell per build, oldest first, told apart by shape and glyph as well as colour: filled when
// the test failed, tinted when it was flaky, hollow when it passed, dashed when it did not run,
// dotted for any other outcome and hatched when the build could not be read. The build number and
// the status are the cell's accessible name and its title; label, when given, is the cell's text,
// such as the day the build finished. The stylesheet outlines the current build.
public static class HistoryStrip
{
    public static void Write(TextWriter writer, IReadOnlyList<AdoTestHistoryEntry> entries, Uri collectionUri, string teamProject, CultureInfo culture) =>
        Write(writer, entries, collectionUri, teamProject, culture, null);

    public static void Write(TextWriter writer, IReadOnlyList<AdoTestHistoryEntry> entries, Uri collectionUri, string teamProject, CultureInfo culture,
        Func<AdoTestHistoryEntry, string?>? label)
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
            string name = SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportLabelValue, culture, entry.BuildNumber, StatusPresentation.Label(entry.Outcome, culture)));
            writer.Write("<li><a class=\"history-cell status-"); writer.Write(StatusPresentation.Css(entry.Outcome)); writer.Write("\" rel=\"noreferrer\" href=\"");
            writer.Write(SinkEncoding.Attribute(AdoWebLinks.BuildTestResult(collectionUri, teamProject, entry.BuildId).AbsoluteUri));
            writer.Write("\" data-build-id=\""); writer.Write(entry.BuildId.ToString(CultureInfo.InvariantCulture));
            writer.Write("\" title=\""); writer.Write(name); writer.Write("\" aria-label=\""); writer.Write(name); writer.Write("\"");
            if (entry.IsCurrent) writer.Write(" aria-current=\"true\"");
            writer.Write("><span class=\"history-glyph\" aria-hidden=\"true\">"); writer.Write(StatusPresentation.Glyph(entry.Outcome)); writer.Write("</span>");
            if (label?.Invoke(entry) is { } text) { writer.Write("<span class=\"history-date\">"); writer.Write(SinkEncoding.Attribute(text)); writer.Write("</span>"); }
            writer.Write("</a></li>");
        }
        writer.Write("</ol>");
    }
}
