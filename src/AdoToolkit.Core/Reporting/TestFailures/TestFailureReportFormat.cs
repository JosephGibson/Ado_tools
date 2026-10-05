namespace AdoToolkit.Core.Reporting.TestFailures;

// The formats of Export-AdoBuildTestFailure: the HTML report with its attachment folder, or one
// flat CSV file with a row per reported test. ReportFormat stays the formats of Export-AdoTestCase.
public enum TestFailureReportFormat
{
    Html,
    Csv,
}
