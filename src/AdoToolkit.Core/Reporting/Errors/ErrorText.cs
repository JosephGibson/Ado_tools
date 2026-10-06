using System.Text;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.Highlighting;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.Errors;

// The text of an error as the classifier reads it: the first lines of its message, its key line,
// its exception type and the frames of the test's own code. Each URL, GUID, path, hexadecimal ID
// and number of a line is a slot, so "after 30012 ms" and "after 30020 ms" compare alike.
internal static partial class ErrorText
{
    // Error lines read for a key line or an exception type; the rest of a long message is detail.
    // History keeps as many lines of a listed message, so it can read it the same way.
    internal const int MaximumScannedLines = ErrorMessageStart.MaximumLines;
    // Frames of the test's own code that make a fingerprint, read from the top of the trace.
    internal const int MaximumFingerprintFrames = 8;

    // The first non-empty line, trimmed, without control characters. When it ends with a colon and
    // the next non-empty line is an exception header, as after MSTest's "Test method X threw
    // exception:", that header is the key line: the first line names the test, not the error.
    internal static string? KeyLine(string? message)
    {
        IReadOnlyList<string> lines = ScannedLines(message);
        return lines.Count == 0 ? null : lines[KeyLineIndex(lines)];
    }

    // The index in lines of the key line that KeyLine returns.
    internal static int KeyLineIndex(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Count > 1 && lines[0].EndsWith(':') && Header().IsMatch(lines[1]) ? 1 : 0;
    }

    // The non-empty lines among the first lines of a message, trimmed, without control characters.
    internal static IReadOnlyList<string> ScannedLines(string? message)
    {
        if (string.IsNullOrEmpty(message)) return [];
        List<string> lines = [];
        foreach (string raw in Lines(message))
        {
            string line = Clean(raw).Trim();
            if (line.Length > 0) lines.Add(line);
        }
        return lines;
    }

    // The innermost exception type of the message, else of the trace: the last "Type:" at the start
    // of a line or after "--->".
    internal static string? ExceptionType(AdoTestAttempt? attempt) =>
        attempt is null ? null : LastType(attempt.ErrorMessage) ?? LastType(attempt.StackTrace);

    // The first frame of the trace in the test's own root namespace, the first segment of its
    // automated name: library frames such as Selenium's are not in the framework list.
    internal static string? RootFrame(AdoTestFailure failure, AdoTestAttempt? attempt) =>
        RootPrefix(failure) is { } prefix && attempt?.StackTrace is { Length: > 0 } trace ? StackTraceLexer.FirstFrame(trace, prefix) : null;

    // The frames of the test's own code, method and line, from the top of the trace: two attempts
    // with the same fingerprint failed at the same place. Null without such a frame.
    internal static string? Fingerprint(AdoTestFailure failure, AdoTestAttempt attempt)
    {
        if (RootPrefix(failure) is not { } prefix || attempt.StackTrace is not { Length: > 0 } trace) return null;
        IReadOnlyList<(string Method, int? Line)> frames = StackTraceLexer.Frames(trace, prefix, MaximumFingerprintFrames);
        return frames.Count == 0 ? null
            : string.Join('\n', frames.Select(static frame => frame.Line is int line ? frame.Method + ":" + line.ToString(CultureInfo.InvariantCulture) : frame.Method));
    }

    // The leading "Type:" of a line and its length with the space after it, or null and 0.
    internal static (string? Type, int Length) Prefix(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Match prefix = PrefixPattern().Match(line);
        return prefix.Success ? (prefix.Groups["type"].Value, prefix.Length) : (null, 0);
    }

    // A line split into text and slots, with the key of its text: equal keys mean one form. The
    // slots and the literal text are found in the folded line; every part is cut from the line as
    // sent. Slots are numbered from first.
    internal static (string Key, IReadOnlyList<ErrorPart> Parts) Tokenize(FoldedText line, int first = 0)
    {
        ArgumentNullException.ThrowIfNull(line);
        List<ErrorPart> parts = [];
        StringBuilder key = new();
        string text = line.Text;
        int position = 0, slot = first, original = 0;
        foreach (Match match in Variable().Matches(text))
        {
            if (match.Index > position) key.Append(text, position, match.Index - position);
            char kind = match.Groups["url"].Success ? 'u' : match.Groups["guid"].Success ? 'g' : match.Groups["path"].Success ? 'p'
                : match.Groups["hex"].Success ? 'h' : 'n';
            key.Append('\u0001').Append(kind);
            int start = line.OriginalStart(match.Index), end = line.OriginalEnd(match.Index + match.Length);
            if (start > original) parts.Add(new ErrorPart(line.Original[original..start], -1));
            parts.Add(new ErrorPart(line.Original[start..end], slot++));
            original = end;
            position = match.Index + match.Length;
        }
        if (position < text.Length) key.Append(text, position, text.Length - position);
        if (original < line.Original.Length) parts.Add(new ErrorPart(line.Original[original..], -1));
        return (key.ToString(), parts);
    }

    // The first lines of a text, without their line breaks.
    internal static IEnumerable<string> Lines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int start = 0;
        for (int line = 0; line < MaximumScannedLines && start < text.Length; line++)
        {
            int end = text.IndexOfAny(['\r', '\n'], start);
            if (end < 0) end = text.Length;
            yield return text[start..end];
            start = end < text.Length && text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? end + 2 : end + 1;
        }
    }

    internal static string Clean(string line) => Control().Replace(line, string.Empty);

    // The test's root namespace and a dot, the first segment of its automated name, or null.
    private static string? RootPrefix(AdoTestFailure failure)
    {
        if (failure.TestName is not { } name) return null;
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 && Identifier().IsMatch(name[..dot]) ? name[..(dot + 1)] : null;
    }

    private static string? LastType(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string? type = null;
        foreach (string line in Lines(text))
            foreach (Match match in TypeHeader().Matches(line.Trim())) type = match.Groups["type"].Value;
        return type;
    }

    [GeneratedRegex(@"(?<url>https?://[^\s""'<>]+)|(?<guid>\b[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\b)|(?<path>\b[A-Za-z]:\\[^\s""'<>|*?]*|\\\\[^\s""'<>|*?]+|(?<![\w.:/~-])/(?:[\w.@%+~-]+/)+[\w.@%+~-]*)|(?<hex>\b0[xX][0-9a-fA-F]+\b|\b(?=[0-9a-fA-F]*[0-9])(?=[0-9a-fA-F]*[a-fA-F])[0-9a-fA-F]{8,}\b)|(?<number>[0-9]+(?:[.,][0-9]+)*)", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();

    [GeneratedRegex(@"^(?:--->\s*)?[\p{L}_][\p{L}\p{N}_.+`]*(?:Exception|Error)\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex Header();

    [GeneratedRegex(@"(?:^|--->\s*)(?<type>[\p{L}_][\p{L}\p{N}_.+`]*(?:Exception|Error))\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex TypeHeader();

    [GeneratedRegex(@"^(?<type>[\p{L}_][\p{L}\p{N}_.+`]*(?:Exception|Error))\s*:\s*", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixPattern();

    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"[\p{Cc}-[\t]]", RegexOptions.CultureInvariant)]
    private static partial Regex Control();
}
