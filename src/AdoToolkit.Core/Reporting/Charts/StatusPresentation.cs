using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.Charts;

public static class StatusPresentation
{
    public static string Label(AdoTestHistoryOutcome status, CultureInfo culture) => Messages.Get(status switch
    {
        AdoTestHistoryOutcome.Passed => AdoMessage.TestReportPassed,
        AdoTestHistoryOutcome.Failed => AdoMessage.TestReportFailed,
        AdoTestHistoryOutcome.Flaky => AdoMessage.TestReportFlaky,
        AdoTestHistoryOutcome.NotRun => AdoMessage.TestReportNotRun,
        AdoTestHistoryOutcome.Unavailable => AdoMessage.TestReportUnavailable,
        _ => AdoMessage.TestReportOther,
    }, culture);

    public static string Glyph(AdoTestHistoryOutcome status) => status switch
    {
        AdoTestHistoryOutcome.Passed => "✓", AdoTestHistoryOutcome.Failed => "✕", AdoTestHistoryOutcome.Flaky => "≈",
        AdoTestHistoryOutcome.Unavailable => "?", _ => "–",
    };

    internal static string Css(AdoTestHistoryOutcome status) => status switch
    {
        AdoTestHistoryOutcome.Passed => "passed", AdoTestHistoryOutcome.Failed => "failed", AdoTestHistoryOutcome.Flaky => "flaky",
        AdoTestHistoryOutcome.NotRun => "notrun", AdoTestHistoryOutcome.Unavailable => "unavailable", _ => "other",
    };

    public static void Write(TextWriter writer, AdoTestHistoryOutcome status, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write("<span class=\"status-badge status-"); writer.Write(Css(status)); writer.Write("\"><span aria-hidden=\"true\">");
        writer.Write(Glyph(status)); writer.Write("</span> "); writer.Write(SinkEncoding.Attribute(Label(status, culture))); writer.Write("</span>");
    }

    internal static void ExternalGlyph(TextWriter writer, CultureInfo culture)
    {
        writer.Write(" <span role=\"img\" aria-label=\"");
        writer.Write(SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportOpenAdo, culture)));
        writer.Write("\">↗</span>");
    }
}
