namespace AdoToolkit.Core.Reporting;

internal static class ReportTime
{
    // Server times are UTC; a report shows every time in the offset of its export. A time too
    // close to the limits of the calendar to be shifted keeps its own offset.
    internal static DateTimeOffset InOffset(DateTimeOffset time, TimeSpan offset)
    {
        long shifted = time.UtcTicks + offset.Ticks;
        return shifted < DateTime.MinValue.Ticks || shifted > DateTime.MaxValue.Ticks ? time : time.ToOffset(offset);
    }
}
