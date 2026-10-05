using System.Buffers;
using System.Text;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// One flat file per build for spreadsheets and scripts: a header, then one record per reported
// test in report order. Header names are English and numbers, booleans and dates invariant, so
// the file reads the same whatever the culture. The values are those of the HTML report: its
// classification, its trend signal and its open bug count. UTF-8 with a byte order mark, comma
// delimiter, CRLF after every record and RFC 4180 quoting.
public static class CsvTestFailureRenderer
{
    private const string BugSeparator = "; ";
    private static readonly SearchValues<char> Quoted = SearchValues.Create(",\"\r\n");

    // Fixed for machine use: never translated, never reordered.
    public static IReadOnlyList<string> Columns { get; } = Array.AsReadOnly(new[]
    {
        "Build", "Ordinal", "Test", "Title", "Classification", "Attempts", "Latest error", "Owner", "Priority",
        "Test case ID", "Test case state", "Open bugs", "Bug IDs", "Bug states", "New", "Since",
    });

    public static void Render(TestFailureReportModel model, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write('﻿');
        Record(writer, Columns);
        foreach (AdoTestFailure failure in model.Failures) Record(writer, Row(model, failure));
    }

    // Reads the written file back: the byte order mark, valid UTF-8, the header, then one record of
    // full width per reported test, in report order.
    internal static void Validate(string path, TestFailureReportModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(model);
        byte[] bytes = File.ReadAllBytes(path);
        if (!bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)) Invalid(model);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes, Encoding.UTF8.Preamble.Length, bytes.Length - Encoding.UTF8.Preamble.Length); }
        catch (DecoderFallbackException error) { throw new InvalidDataException(Messages.Get(AdoMessage.InvalidReportOutput, model.Culture), error); }
        List<string[]> records = Records(text, model);
        if (records.Count != model.Failures.Count + 1 || !records[0].SequenceEqual(Columns, StringComparer.Ordinal)) Invalid(model);
        for (int index = 1; index < records.Count; index++)
            if (records[index].Length != Columns.Count || records[index][1] != Integer(model.Failures[index - 1].Ordinal)) Invalid(model);
    }

    private static string[] Row(TestFailureReportModel model, AdoTestFailure failure)
    {
        TestFailureSignal? signal = TestFailureSignal.Of(failure.History);
        AdoTestCaseLink? testCase = failure.TestCase is { Id: > 0 } link ? link : null;
        // The bug list of the card: every bug with an ID, in its order; a bug that could not be read
        // keeps its place with an empty state.
        AdoTestBug[] bugs = [.. failure.Bugs.Where(static bug => bug.Id > 0)];
        return
        [
            Integer(model.Build.Id),
            Integer(failure.Ordinal),
            Text(failure.TestName ?? failure.ShortName),
            Text(failure.Title),
            Text(failure.Classification.ToString()),
            Integer(failure.Attempts.Count),
            Text(HtmlTestFailureRenderer.LatestErrorAttempt(failure)?.ErrorMessage),
            Text(HtmlTestFailureRenderer.Identity(failure.Owner)),
            Integer(failure.Priority),
            Integer(testCase?.Id),
            Text(testCase is { IsResolved: true } ? testCase.State : null),
            Integer(bugs.Count(static bug => bug.IsOpen == true)),
            Text(string.Join(BugSeparator, bugs.Select(static bug => Integer(bug.Id)))),
            Text(string.Join(BugSeparator, bugs.Select(static bug => bug.State ?? string.Empty))),
            signal is null ? string.Empty : signal.IsNew.ToString(CultureInfo.InvariantCulture),
            Since(model, failure, signal),
        ];
    }

    // The trend's Since: the day the first build of the current run of failures finished, in the
    // export's offset, else that build's number; nothing for a new failure or without a comparison.
    private static string Since(TestFailureReportModel model, AdoTestFailure failure, TestFailureSignal? signal)
    {
        if (signal is not { IsNew: false }) return string.Empty;
        AdoTestHistoryEntry first = failure.History[^signal.Streak];
        return HtmlTestFailureRenderer.FinishedOn(model, first.BuildId) is { } day
            ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Text(first.BuildNumber);
    }

    private static string Integer(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    // Remote text as sent, except that a lone surrogate half, which UTF-8 cannot hold, becomes
    // U+FFFD. A spreadsheet reads a cell that starts with =, +, -, @, a tab or a carriage return as
    // a formula, so such a cell starts with an apostrophe instead.
    private static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string text = WellFormed(value);
        return text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
    }

    private static string WellFormed(string text)
    {
        if (!text.AsSpan().ContainsAnyInRange('\uD800', '\uDFFF')) return text;
        char[] characters = text.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (char.IsHighSurrogate(characters[index]) && index + 1 < characters.Length && char.IsLowSurrogate(characters[index + 1])) index++;
            else if (char.IsSurrogate(characters[index])) characters[index] = '�';
        }
        return new string(characters);
    }

    private static void Record(TextWriter writer, IReadOnlyList<string> cells)
    {
        for (int index = 0; index < cells.Count; index++)
        {
            if (index > 0) writer.Write(',');
            string cell = cells[index];
            if (!cell.AsSpan().ContainsAny(Quoted)) { writer.Write(cell); continue; }
            writer.Write('"');
            writer.Write(cell.Replace("\"", "\"\"", StringComparison.Ordinal));
            writer.Write('"');
        }
        writer.Write("\r\n");
    }

    // RFC 4180 records, each ended by CRLF. A quote inside an unquoted field, a bare line break, or
    // anything but a delimiter after a closing quote makes the file invalid.
    private static List<string[]> Records(string text, TestFailureReportModel model)
    {
        List<string[]> records = [];
        List<string> fields = [];
        StringBuilder field = new();
        int index = 0;
        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                index++;
                while (true)
                {
                    if (index >= text.Length) Invalid(model);
                    if (text[index] != '"') { field.Append(text[index++]); continue; }
                    if (index + 1 < text.Length && text[index + 1] == '"') { field.Append('"'); index += 2; continue; }
                    index++;
                    break;
                }
            }
            else
            {
                while (index < text.Length && text[index] is not (',' or '\r'))
                {
                    if (text[index] is '"' or '\n') Invalid(model);
                    field.Append(text[index++]);
                }
            }
            fields.Add(field.ToString());
            field.Clear();
            if (index < text.Length && text[index] == ',') { index++; continue; }
            if (index + 1 < text.Length && text[index] == '\r' && text[index + 1] == '\n')
            {
                records.Add([.. fields]);
                fields.Clear();
                index += 2;
                continue;
            }
            Invalid(model);
        }
        if (fields.Count > 0) Invalid(model);
        return records;
    }

    private static void Invalid(TestFailureReportModel model) => throw new InvalidDataException(Messages.Get(AdoMessage.InvalidReportOutput, model.Culture));
}
