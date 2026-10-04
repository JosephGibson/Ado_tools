using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Reporting.Charts;

// One bar per build, oldest first, with a legend and the same numbers as a table below it. The
// chart is drawn at its natural size, one slot per build: a few builds stay small and to the
// left, and many builds scroll sideways instead of shrinking until their labels cannot be read.
public static class RunHistoryChart
{
    private const double BarWidth = 52, BarTop = 56, BarHeight = 120, BarBottom = BarTop + BarHeight, ChartHeight = BarBottom + 36;
    private const double MinimumSlot = 80, CharacterWidth = 7, MinimumSegment = 6;
    // A build number longer than this is cut in the bar label; the bar's title and the table hold it whole.
    private const int MaximumLabelCharacters = 28;

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
        // Every slot is as wide as the longest label needs, so labels never run into each other.
        string[] labels = [.. history.Select(static item => Label(item.BuildNumber))];
        double slot = Math.Max(MinimumSlot, labels.Select(static label => label.Length).DefaultIfEmpty().Max() * CharacterWidth + 16);
        double width = Math.Max(320, history.Count * slot + 32);
        Legend(writer, culture);
        // The width and height attributes give the drawing its natural size; the stylesheet does not stretch it.
        writer.Write("<div class=\"chart-scroll\"><svg class=\"history-chart\" xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" width=\"");
        writer.Write(N(width)); writer.Write("\" height=\""); writer.Write(N(ChartHeight)); writer.Write("\" viewBox=\"0 0 ");
        writer.Write(N(width)); writer.Write(' '); writer.Write(N(ChartHeight)); writer.Write("\"><title>");
        writer.Write(E(Messages.Get(AdoMessage.TestReportHistory, culture))); writer.Write("</title>");
        for (int i = 0; i < history.Count; i++)
        {
            AdoBuildTestSummary item = history[i];
            double center = 16 + i * slot + slot / 2, x = center - BarWidth / 2, top = BarTop;
            writer.Write("<a rel=\"noreferrer\" href=\""); writer.Write(E(AdoWebLinks.BuildTestResult(collectionUri, teamProject, item.BuildId).AbsoluteUri)); writer.Write("\" data-build-id=\"");
            writer.Write(N(item.BuildId)); writer.Write('"');
            if (item.IsCurrent) writer.Write(" aria-current=\"true\"");
            writer.Write("><title>"); writer.Write(E(Title(item, culture, offset))); writer.Write("</title>");
            if (!item.IsAvailable)
            {
                Rect(writer, x, BarTop, BarWidth, BarHeight, "chart-unavailable");
                // Explicit short hatch lines avoid document-wide SVG ids and collisions between charts.
                for (double y = BarTop; y < BarBottom; y += 12)
                {
                    writer.Write("<path class=\"chart-hatch\" d=\"M "); writer.Write(N(x)); writer.Write(' '); writer.Write(N(y + 8));
                    writer.Write(" l "); writer.Write(N(BarWidth)); writer.Write(" -8\"/>");
                }
                Text(writer, center, BarTop + BarHeight / 2 + 8, "?", "chart-caption");
            }
            else
            {
                // A segment that would be a hair's breadth still shows: one failure among thousands of
                // tests gets the minimum height, taken from the largest segment so the bar keeps its total.
                double[] heights = SegmentHeights([item.Passed, item.Failed, item.Other], maximum);
                double bottom = BarBottom;
                Segment(writer, item.Passed, heights[0], AdoTestHistoryOutcome.Passed, "chart-pass", x, ref bottom, culture);
                Segment(writer, item.Failed, heights[1], AdoTestHistoryOutcome.Failed, "chart-fail", x, ref bottom, culture);
                Segment(writer, item.Other, heights[2], AdoTestHistoryOutcome.Other, "chart-other", x, ref bottom, culture);
                top = bottom;
                if (Total(item) == 0)
                {
                    writer.Write("<path class=\"chart-zero\" d=\"M "); writer.Write(N(x)); writer.Write(' '); writer.Write(N(BarBottom)); writer.Write(" h "); writer.Write(N(BarWidth)); writer.Write("\"/>");
                }
                // The failed count is the number this chart is read for, so it stands above every bar.
                Text(writer, center, top - 8, StatusPresentation.Glyph(AdoTestHistoryOutcome.Failed) + " " + item.Failed.ToString(culture), "chart-failed");
            }
            if (item.IsCurrent)
            {
                Rect(writer, x - 3, top - 3, BarWidth + 6, BarBottom + 6 - top, "chart-current");
                Text(writer, center, 16, Messages.Get(AdoMessage.TestReportThisRun, culture), "chart-caption");
            }
            Text(writer, center, BarBottom + 22, labels[i], "chart-caption");
            writer.Write("</a>");
        }
        writer.Write("</svg></div>");
        Table(writer, history, collectionUri, teamProject, culture, offset);
    }

    private static long Total(AdoBuildTestSummary item) => (long)item.Passed + item.Failed + item.Other;
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string value) => SinkEncoding.Attribute(value);

    // Shortened without splitting a surrogate pair: the sink encodes a lone half as the replacement
    // character, so the caption would contradict the title, which carries the whole build number.
    private static string Label(string buildNumber)
    {
        if (buildNumber.Length <= MaximumLabelCharacters) return buildNumber;
        int length = MaximumLabelCharacters - 1;
        if (char.IsHighSurrogate(buildNumber[length - 1])) length--;
        return string.Concat(buildNumber.AsSpan(0, length), "…");
    }

    private static string? Date(DateTimeOffset? value, CultureInfo culture, TimeSpan? offset) =>
        (value is { } time && offset is { } shift ? ReportTime.InOffset(time, shift) : value)?.ToString("g", culture);

    private static string Title(AdoBuildTestSummary item, CultureInfo culture, TimeSpan? offset) => Messages.Get(AdoMessage.TestReportHistoryTitle, culture,
        item.BuildNumber, item.SourceBranch ?? "–", Date(item.FinishTime, culture, offset) ?? "–",
        item.Passed, item.Failed, item.Flaky, item.Other,
        item.IsAvailable ? Messages.Get(AdoMessage.TestReportAvailable, culture) : StatusPresentation.Label(AdoTestHistoryOutcome.Unavailable, culture),
        Messages.Get(AdoMessage.TestReportOpenAdo, culture));

    // Heights in proportion to the counts. A count above zero is drawn at least MinimumSegment high;
    // what that adds is taken from the tallest segment while it stays at least as high itself.
    private static double[] SegmentHeights(int[] counts, double maximum)
    {
        double[] heights = [.. counts.Select(count => count / maximum * BarHeight)];
        double added = 0;
        for (int index = 0; index < heights.Length; index++)
        {
            if (counts[index] == 0 || heights[index] >= MinimumSegment) continue;
            added += MinimumSegment - heights[index];
            heights[index] = MinimumSegment;
        }
        int tallest = Array.IndexOf(heights, heights.Max());
        if (added > 0 && heights[tallest] - added >= MinimumSegment) heights[tallest] -= added;
        return heights;
    }

    private static void Segment(TextWriter writer, int count, double height, AdoTestHistoryOutcome status, string css, double x, ref double bottom,
        CultureInfo culture)
    {
        if (count == 0) return;
        bottom -= height;
        writer.Write("<g data-outcome=\""); writer.Write(StatusPresentation.Css(status)); writer.Write("\" data-count=\""); writer.Write(N(count)); writer.Write("\"><title>");
        writer.Write(E(Messages.Get(AdoMessage.TestReportLabelValue, culture, StatusPresentation.Glyph(status) + " " + StatusPresentation.Label(status, culture),
            count.ToString(culture))));
        writer.Write("</title>");
        Rect(writer, x, bottom, BarWidth, height, css);
        if (height >= 20 && count.ToString(culture).Length <= 6) Text(writer, x + BarWidth / 2, bottom + height / 2 + 4, count.ToString(culture), "chart-count");
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

    // What each colour of a bar means, in the words the rest of the report uses.
    private static void Legend(TextWriter writer, CultureInfo culture)
    {
        writer.Write("<ul class=\"chart-legend\">");
        foreach ((AdoTestHistoryOutcome status, string css) in new[] { (AdoTestHistoryOutcome.Passed, "chart-pass"), (AdoTestHistoryOutcome.Failed, "chart-fail"),
            (AdoTestHistoryOutcome.Other, "chart-other"), (AdoTestHistoryOutcome.Unavailable, "chart-unavailable") })
        {
            writer.Write("<li><svg class=\"legend-swatch\" xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 12 12\" aria-hidden=\"true\">");
            Rect(writer, 0, 0, 12, 12, css);
            if (status == AdoTestHistoryOutcome.Unavailable) writer.Write("<path class=\"chart-hatch\" d=\"M 0 9 l 12 -6\"/>");
            writer.Write("</svg> "); writer.Write(E(StatusPresentation.Label(status, culture))); writer.Write("</li>");
        }
        writer.Write("</ul>");
    }

    // Always visible: a table inside a closed <details> could not be opened without the script.
    private static void Table(TextWriter writer, IReadOnlyList<AdoBuildTestSummary> history, Uri collectionUri, string teamProject, CultureInfo culture,
        TimeSpan? offset)
    {
        writer.Write("<div class=\"history-data\"><div class=\"table-scroll\"><table><caption>"); writer.Write(E(Messages.Get(AdoMessage.TestReportHistoryData, culture)));
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
            writer.Write('>'); writer.Write(E(item.BuildNumber)); writer.Write("</a>");
            if (item.IsCurrent) { writer.Write(" <span class=\"this-run\">"); writer.Write(E(Messages.Get(AdoMessage.TestReportThisRun, culture))); writer.Write("</span>"); }
            writer.Write("</th>");
            foreach (string value in new[] { item.SourceBranch ?? "–", Date(item.FinishTime, culture, offset) ?? "–",
                item.Passed.ToString(culture), item.Failed.ToString(culture), item.Flaky.ToString(culture), item.Other.ToString(culture) })
            { writer.Write("<td>"); writer.Write(E(value)); writer.Write("</td>"); }
            writer.Write("<td>");
            if (!item.IsAvailable) StatusPresentation.Write(writer, AdoTestHistoryOutcome.Unavailable, culture);
            else writer.Write(E(Messages.Get(AdoMessage.TestReportAvailable, culture)));
            writer.Write("</td></tr>");
        }
        writer.Write("</tbody></table></div></div>");
    }
}
