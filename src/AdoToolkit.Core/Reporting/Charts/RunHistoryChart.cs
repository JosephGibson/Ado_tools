using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Reporting.Charts;

public static class RunHistoryChart
{
    public static void Write(TextWriter writer, IReadOnlyList<AdoBuildTestSummary> history, Uri collectionUri, string teamProject, CultureInfo culture)
        => Write(writer, history, collectionUri, teamProject, culture, null);

    // offset, when given, is the offset finish times are shown in, such as the report's export time.
    public static void Write(TextWriter writer, IReadOnlyList<AdoBuildTestSummary> history, Uri collectionUri, string teamProject, CultureInfo culture,
        TimeSpan? offset)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(collectionUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamProject);
        double maximum = Math.Max(1, history.Where(item => item.IsAvailable).Select(Total).DefaultIfEmpty().Max());
        writer.Write("<div class=\"chart-scroll\"><svg class=\"history-chart\" xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" viewBox=\"0 0 ");
        writer.Write(N(Math.Max(320, history.Count * 80 + 32))); writer.Write(" 260\"><title>");
        writer.Write(E(Messages.Get(AdoMessage.TestReportHistory, culture))); writer.Write("</title>");
        for (int i = 0; i < history.Count; i++)
        {
            AdoBuildTestSummary item = history[i];
            double x = 16 + i * 80, top = 220 - Total(item) / maximum * 180;
            writer.Write("<a rel=\"noreferrer\" href=\""); writer.Write(E(AdoWebLinks.BuildTestResult(collectionUri, teamProject, item.BuildId).AbsoluteUri)); writer.Write("\" data-build-id=\"");
            writer.Write(N(item.BuildId)); writer.Write('"');
            if (item.IsCurrent) writer.Write(" aria-current=\"true\"");
            writer.Write("><title>"); writer.Write(E(Title(item, culture, offset))); writer.Write("</title>");
            if (!item.IsAvailable)
            {
                top = 40;
                Rect(writer, x, top, 52, 180, "chart-unavailable");
                // Explicit short hatch lines avoid document-wide SVG ids and collisions between charts.
                for (int y = 40; y < 220; y += 12)
                {
                    writer.Write("<path class=\"chart-hatch\" d=\"M "); writer.Write(N(x)); writer.Write(' '); writer.Write(N(y + 8));
                    writer.Write(" l 52 -8\"/>");
                }
                Text(writer, x + 26, 138, "?", "chart-caption");
            }
            else
            {
                double bottom = 220;
                Segment(writer, item.Passed, AdoTestHistoryOutcome.Passed, "chart-pass", x, ref bottom, maximum, culture);
                Segment(writer, item.Failed, AdoTestHistoryOutcome.Failed, "chart-fail", x, ref bottom, maximum, culture);
                Segment(writer, item.Other, AdoTestHistoryOutcome.Other, "chart-other", x, ref bottom, maximum, culture);
                if (Total(item) == 0)
                {
                    writer.Write("<path class=\"chart-zero\" d=\"M "); writer.Write(N(x)); writer.Write(" 220 h 52\"/>");
                }
            }
            if (item.IsCurrent)
            {
                Rect(writer, x - 3, top - 3, 58, 226 - top, "chart-current");
                Text(writer, x + 26, 20, Messages.Get(AdoMessage.TestReportThisRun, culture), "chart-caption");
            }
            Text(writer, x + 26, 244, (i + 1).ToString(culture) + " ↗", "chart-caption");
            writer.Write("</a>");
        }
        writer.Write("</svg></div>");
        Table(writer, history, collectionUri, teamProject, culture, offset);
    }

    private static long Total(AdoBuildTestSummary item) => (long)item.Passed + item.Failed + item.Other;
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string value) => SinkEncoding.Attribute(value);

    private static string? Date(DateTimeOffset? value, CultureInfo culture, TimeSpan? offset) =>
        (offset is TimeSpan shift ? value?.ToOffset(shift) : value)?.ToString("g", culture);

    private static string Title(AdoBuildTestSummary item, CultureInfo culture, TimeSpan? offset) => Messages.Get(AdoMessage.TestReportHistoryTitle, culture,
        item.BuildNumber, item.SourceBranch ?? "–", Date(item.FinishTime, culture, offset) ?? "–",
        item.Passed, item.Failed, item.Flaky, item.Other,
        item.IsAvailable ? Messages.Get(AdoMessage.TestReportAvailable, culture) : StatusPresentation.Label(AdoTestHistoryOutcome.Unavailable, culture),
        Messages.Get(AdoMessage.TestReportOpenAdo, culture));

    private static void Segment(TextWriter writer, int count, AdoTestHistoryOutcome status, string css, double x, ref double bottom, double maximum, CultureInfo culture)
    {
        if (count == 0) return;
        double height = count / maximum * 180;
        bottom -= height;
        writer.Write("<g data-outcome=\""); writer.Write(StatusPresentation.Css(status)); writer.Write("\" data-count=\""); writer.Write(N(count)); writer.Write("\"><title>");
        writer.Write(E(StatusPresentation.Glyph(status) + " " + StatusPresentation.Label(status, culture) + ": " + count.ToString(culture)));
        writer.Write("</title>");
        Rect(writer, x, bottom, 52, height, css);
        if (height >= 20 && count.ToString(culture).Length <= 6) Text(writer, x + 26, bottom + height / 2 + 4, count.ToString(culture), "chart-count");
        writer.Write("</g>");
    }

    private static void Rect(TextWriter writer, double x, double y, double width, double height, string css)
    {
        writer.Write("<rect class=\""); writer.Write(css); writer.Write("\" x=\""); writer.Write(N(x)); writer.Write("\" y=\""); writer.Write(N(y));
        writer.Write("\" width=\""); writer.Write(N(width)); writer.Write("\" height=\""); writer.Write(N(height)); writer.Write("\"/>");
    }

    private static void Text(TextWriter writer, double x, double y, string text, string css)
    {
        writer.Write("<text class=\""); writer.Write(css); writer.Write("\" x=\""); writer.Write(N(x)); writer.Write("\" y=\""); writer.Write(N(y));
        writer.Write("\">"); writer.Write(E(text)); writer.Write("</text>");
    }

    private static void Table(TextWriter writer, IReadOnlyList<AdoBuildTestSummary> history, Uri collectionUri, string teamProject, CultureInfo culture,
        TimeSpan? offset)
    {
        writer.Write("<details class=\"history-data\"><summary>"); writer.Write(E(Messages.Get(AdoMessage.TestReportHistoryData, culture)));
        writer.Write("</summary><div class=\"table-scroll\"><table><caption>"); writer.Write(E(Messages.Get(AdoMessage.TestReportHistory, culture)));
        writer.Write("</caption><thead><tr>");
        foreach (AdoMessage header in new[] { AdoMessage.TestReportBuild, AdoMessage.TestReportBranch, AdoMessage.TestReportFinished, AdoMessage.TestReportPassed,
            AdoMessage.TestReportFailed, AdoMessage.TestReportFlaky, AdoMessage.TestReportOther, AdoMessage.ReportStatus })
        { writer.Write("<th scope=\"col\">"); writer.Write(E(Messages.Get(header, culture))); writer.Write("</th>"); }
        writer.Write("</tr></thead><tbody>");
        foreach (AdoBuildTestSummary item in history)
        {
            writer.Write("<tr data-build-id=\""); writer.Write(N(item.BuildId)); writer.Write("\"><th scope=\"row\"><a rel=\"noreferrer\" href=\"");
            writer.Write(E(AdoWebLinks.BuildTestResult(collectionUri, teamProject, item.BuildId).AbsoluteUri)); writer.Write("\"");
            if (item.IsCurrent) writer.Write(" aria-current=\"true\"");
            writer.Write('>'); writer.Write(E(item.BuildNumber)); StatusPresentation.ExternalGlyph(writer, culture); writer.Write("</a>");
            if (item.IsCurrent) { writer.Write(' '); writer.Write(E(Messages.Get(AdoMessage.TestReportThisRun, culture))); }
            writer.Write("</th>");
            foreach (string value in new[] { item.SourceBranch ?? "–", Date(item.FinishTime, culture, offset) ?? "–",
                item.Passed.ToString(culture), item.Failed.ToString(culture), item.Flaky.ToString(culture), item.Other.ToString(culture) })
            { writer.Write("<td>"); writer.Write(E(value)); writer.Write("</td>"); }
            writer.Write("<td>");
            if (!item.IsAvailable) StatusPresentation.Write(writer, AdoTestHistoryOutcome.Unavailable, culture);
            else writer.Write(E(Messages.Get(AdoMessage.TestReportAvailable, culture)));
            writer.Write("</td></tr>");
        }
        writer.Write("</tbody></table></div></details>");
    }
}
