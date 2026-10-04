using System.Text;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.Highlighting;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// Failures grouped by their latest error, for By error, the Overview and Open bugs. The key line is
// the whole first line of the latest error or, after MSTest's "Test method X threw exception:", the
// exception header below it. Each URL, GUID, path, hexadecimal ID and number in it becomes a slot,
// and lines whose literal text and slot kinds match share a cluster, so "after 30012 ms" and
// "after 30020 ms" group together. Everything comes from the model; nothing is requested.
internal static partial class ErrorClusters
{
    // Error lines read for a key line or an exception type; the rest of a long message is detail.
    private const int MaximumScannedLines = 20;

    // Text, or one slot of a key line: Slot is -1 for literal text.
    internal sealed record Part(string Text, int Slot);

    internal sealed class Member
    {
        internal required AdoTestFailure Failure { get; init; }
        internal required IReadOnlyList<Part> Parts { get; init; }
        // The leading "Type:" of the key line and its length with the space after it, or null and 0.
        internal string? PrefixType { get; init; }
        internal int PrefixLength { get; init; }
        internal string? ExceptionType { get; init; }
        internal string? Frame { get; init; }
    }

    internal sealed class Cluster
    {
        // From 1 in display order; the cluster's anchor is e-Number.
        internal required int Number { get; init; }
        // Null for the tests without an error message.
        internal required string? Key { get; init; }
        internal required IReadOnlyList<Member> Members { get; init; }
        // Slots whose value is not the same in every member.
        internal required IReadOnlySet<int> Varying { get; init; }
        internal string? ExceptionType { get; init; }
        internal string? Frame { get; init; }
        internal int FrameCount { get; init; }
        internal Member Representative => Members[0];

        // The values of one slot, each once, in member order.
        internal IReadOnlyList<string> Values(int slot) =>
            [.. Members.Select(member => member.Parts.FirstOrDefault(part => part.Slot == slot)?.Text).OfType<string>().Distinct(StringComparer.Ordinal)];

        // A member's values for the varying slots, in line order.
        internal IReadOnlyList<string> ValuesOf(Member member) =>
            [.. member.Parts.Where(part => part.Slot >= 0 && Varying.Contains(part.Slot)).Select(static part => part.Text)];
    }

    internal static IReadOnlyList<Cluster> Of(IReadOnlyList<AdoTestFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        List<(string? Key, Member Member)> items = [];
        foreach (AdoTestFailure failure in failures)
        {
            AdoTestAttempt? attempt = HtmlTestFailureRenderer.LatestErrorAttempt(failure);
            string? line = KeyLine(attempt?.ErrorMessage);
            string? key = null;
            IReadOnlyList<Part> parts = [];
            if (line is not null) (key, parts) = Tokenize(line);
            Match prefix = line is null ? Match.Empty : Prefix().Match(line);
            items.Add((key, new Member
            {
                Failure = failure, Parts = parts,
                PrefixType = prefix.Success ? prefix.Groups["type"].Value : null, PrefixLength = prefix.Success ? prefix.Length : 0,
                ExceptionType = ExceptionType(attempt), Frame = RootFrame(failure, attempt),
            }));
        }
        var groups = items.GroupBy(static item => item.Key, StringComparer.Ordinal)
            .OrderBy(static group => group.Key is null ? 1 : 0).ThenByDescending(static group => group.Count())
            .ThenBy(static group => group.First().Member.Failure.Ordinal).ToArray();
        List<Cluster> clusters = [];
        foreach (var group in groups)
        {
            Member[] members = [.. group.Select(static item => item.Member)];
            HashSet<int> varying = [.. members[0].Parts.Where(static part => part.Slot >= 0).Select(static part => part.Slot)
                .Where(slot => members.Select(member => member.Parts.FirstOrDefault(part => part.Slot == slot)?.Text).Distinct(StringComparer.Ordinal).Skip(1).Any())];
            (string? frame, int frameCount) = MostCommon(members.Select(static member => member.Frame));
            clusters.Add(new Cluster
            {
                Number = clusters.Count + 1, Key = group.Key, Members = members, Varying = varying,
                ExceptionType = MostCommon(members.Select(static member => member.ExceptionType)).Value,
                Frame = frameCount >= 2 ? frame : null, FrameCount = frameCount >= 2 ? frameCount : 0,
            });
        }
        return clusters;
    }

    // The key of one line; equal keys mean one cluster.
    internal static string Key(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Tokenize(Clean(line)).Key;
    }

    // The first non-empty line, trimmed, without control characters. When it ends with a colon and
    // the next non-empty line is an exception header, as after MSTest's "Test method X threw
    // exception:", that header is the key line: the first line names the test, not the error.
    internal static string? KeyLine(string? message)
    {
        if (string.IsNullOrEmpty(message)) return null;
        string? first = null;
        foreach (string raw in Lines(message))
        {
            string line = Clean(raw).Trim();
            if (line.Length == 0) continue;
            if (first is null)
            {
                if (!line.EndsWith(':')) return line;
                first = line;
                continue;
            }
            return Header().IsMatch(line) ? line : first;
        }
        return first;
    }

    // The innermost exception type of the message, else of the trace: the last "Type:" at the start
    // of a line or after "--->".
    internal static string? ExceptionType(AdoTestAttempt? attempt) =>
        attempt is null ? null : LastType(attempt.ErrorMessage) ?? LastType(attempt.StackTrace);

    // The type name without its namespace.
    internal static string ShortType(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        int dot = type.LastIndexOf('.');
        return dot >= 0 && dot < type.Length - 1 ? type[(dot + 1)..] : type;
    }

    // Type and method of a frame, without the namespace: Pages.CheckoutPage.Submit becomes
    // CheckoutPage.Submit(), and an async state machine's <SubmitAsync>d__4.MoveNext becomes SubmitAsync.
    internal static string ShortFrame(string method)
    {
        ArgumentNullException.ThrowIfNull(method);
        string name = AsyncStateMachine().Replace(method, ".${name}");
        int last = name.LastIndexOf('.');
        int previous = last > 0 ? name.LastIndexOf('.', last - 1) : -1;
        return name[(previous + 1)..] + "()";
    }

    // The first frame of the trace in the test's own root namespace, the first segment of its
    // automated name: library frames such as Selenium's are not in the framework list.
    private static string? RootFrame(AdoTestFailure failure, AdoTestAttempt? attempt)
    {
        if (attempt?.StackTrace is not { Length: > 0 } trace || failure.TestName is not { } name) return null;
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || !Identifier().IsMatch(name[..dot])) return null;
        return StackTraceLexer.FirstFrame(trace, name[..(dot + 1)]);
    }

    private static string? LastType(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string? type = null;
        foreach (string line in Lines(text))
            foreach (Match match in TypeHeader().Matches(line.Trim())) type = match.Groups["type"].Value;
        return type;
    }

    private static (string? Value, int Count) MostCommon(IEnumerable<string?> values)
    {
        (string? Value, int Count) best = (null, 0);
        foreach (var group in values.OfType<string>().GroupBy(static value => value, StringComparer.Ordinal))
            if (group.Count() > best.Count) best = (group.Key, group.Count());
        return best;
    }

    private static (string Key, IReadOnlyList<Part> Parts) Tokenize(string line)
    {
        List<Part> parts = [];
        StringBuilder key = new();
        int position = 0, slot = 0;
        foreach (Match match in Variable().Matches(line))
        {
            if (match.Index > position)
            {
                parts.Add(new Part(line[position..match.Index], -1));
                key.Append(line, position, match.Index - position);
            }
            char kind = match.Groups["url"].Success ? 'u' : match.Groups["guid"].Success ? 'g' : match.Groups["path"].Success ? 'p'
                : match.Groups["hex"].Success ? 'h' : 'n';
            parts.Add(new Part(match.Value, slot++));
            key.Append('\u0001').Append(kind);
            position = match.Index + match.Length;
        }
        if (position < line.Length)
        {
            parts.Add(new Part(line[position..], -1));
            key.Append(line, position, line.Length - position);
        }
        return (key.ToString(), parts);
    }

    // The first lines of a text, without their line breaks.
    private static IEnumerable<string> Lines(string text)
    {
        int start = 0;
        for (int line = 0; line < MaximumScannedLines && start < text.Length; line++)
        {
            int end = text.IndexOfAny(['\r', '\n'], start);
            if (end < 0) end = text.Length;
            yield return text[start..end];
            start = end < text.Length && text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? end + 2 : end + 1;
        }
    }

    private static string Clean(string line) => Control().Replace(line, string.Empty);

    [GeneratedRegex(@"(?<url>https?://[^\s""'<>]+)|(?<guid>\b[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\b)|(?<path>\b[A-Za-z]:\\[^\s""'<>|*?]*|\\\\[^\s""'<>|*?]+|(?<![\w.:/~-])/(?:[\w.@%+~-]+/)+[\w.@%+~-]*)|(?<hex>\b0[xX][0-9a-fA-F]+\b|\b(?=[0-9a-fA-F]*[0-9])(?=[0-9a-fA-F]*[a-fA-F])[0-9a-fA-F]{8,}\b)|(?<number>[0-9]+(?:[.,][0-9]+)*)", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();

    [GeneratedRegex(@"^(?:--->\s*)?[\p{L}_][\p{L}\p{N}_.+`]*(?:Exception|Error)\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex Header();

    [GeneratedRegex(@"(?:^|--->\s*)(?<type>[\p{L}_][\p{L}\p{N}_.+`]*(?:Exception|Error))\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex TypeHeader();

    [GeneratedRegex(@"^(?<type>[\p{L}_][\p{L}\p{N}_.+`]*(?:Exception|Error))\s*:\s*", RegexOptions.CultureInvariant)]
    private static partial Regex Prefix();

    [GeneratedRegex(@"\.<(?<name>[^>]+)>d__[0-9]+\.MoveNext$", RegexOptions.CultureInvariant)]
    private static partial Regex AsyncStateMachine();

    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"[\p{Cc}-[\t]]", RegexOptions.CultureInvariant)]
    private static partial Regex Control();
}
