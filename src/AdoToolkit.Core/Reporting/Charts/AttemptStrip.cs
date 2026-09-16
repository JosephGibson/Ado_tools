using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.Charts;

public static class AttemptStrip
{
    public static void Write(TextWriter writer, IReadOnlyList<AdoTestAttempt> attempts, CultureInfo culture, string? failureAnchor = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(culture);
        writer.Write("<ol class=\"attempt-strip\" aria-label=\"");
        writer.Write(SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportAttempts, culture))); writer.Write("\">");
        foreach (AdoTestAttempt attempt in attempts)
        {
            AdoTestHistoryOutcome status = attempt.OutcomeClass switch
            {
                AdoTestOutcomeClass.Pass => AdoTestHistoryOutcome.Passed,
                AdoTestOutcomeClass.Failure => AdoTestHistoryOutcome.Failed,
                _ => AdoTestHistoryOutcome.Other,
            };
            writer.Write("<li>");
            if (failureAnchor is not null)
            {
                writer.Write("<a class=\"attempt-cell\" href=\"#"); writer.Write(SinkEncoding.Attribute(failureAnchor));
                writer.Write("-a"); writer.Write(attempt.Number.ToString(CultureInfo.InvariantCulture)); writer.Write("\">");
            }
            writer.Write("<span class=\"attempt-caption\">");
            writer.Write(SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportAttemptOf, culture, attempt.Number, attempts.Count)));
            writer.Write("</span> "); StatusPresentation.Write(writer, status, culture);
            if (!string.Equals(attempt.Outcome, StatusPresentation.Label(status, culture), StringComparison.Ordinal))
            { writer.Write(" <span class=\"attempt-outcome\">"); writer.Write(SinkEncoding.Attribute(attempt.Outcome)); writer.Write("</span>"); }
            if (failureAnchor is not null) writer.Write("</a>");
            writer.Write("</li>");
        }
        writer.Write("</ol>");
    }
}
