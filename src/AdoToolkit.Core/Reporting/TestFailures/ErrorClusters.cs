using System.Text;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.Errors;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// The tests grouped by error, for By error, the Overview and Open bugs: one cluster for each error
// that the classifier found in a shown test's failed attempts, and one for the tests without an
// error message. A test is a member of the cluster of each error it had; it is the primary member
// of one of them. Everything comes from the model; nothing is requested.
//
// Key keeps the 0.8.5 key of one line, which the console's grouping and the tests still use: each
// URL, GUID, path, hexadecimal ID and number in it becomes a slot, so "after 30012 ms" and
// "after 30020 ms" share a key.
internal static partial class ErrorClusters
{
    // Text, or one slot of a key line: Slot is -1 for literal text.
    internal sealed record Part(string Text, int Slot);

    internal sealed class Member
    {
        internal required AdoTestFailure Failure { get; init; }
        internal required ErrorProfile Profile { get; init; }
        // The test's entry for the cluster's error; null in the cluster of tests without a message.
        internal ErrorProfileEntry? Entry { get; init; }
        // The attempt whose message the member shows: the latest one with the cluster's error.
        internal AdoTestAttempt? Attempt { get; init; }
        internal string? Frame { get; init; }
        // The test's primary error against its previous failed build, or null.
        internal ErrorComparison? Comparison { get; init; }
        internal bool IsPrimary => Entry is null || ReferenceEquals(Entry, Profile.Primary);
        internal IReadOnlyList<ErrorPart> Parts => Entry?.Latest.Form.Parts ?? [];
        // The leading "Type:" of the key line and its length with the space after it, or null and 0.
        internal string? PrefixType => Entry?.Latest.Form.PrefixType;
        internal int PrefixLength => Entry?.Latest.Form.PrefixLength ?? 0;
        internal string? ExceptionType => Entry?.Latest.ExceptionType;
    }

    internal sealed class Cluster
    {
        // From 1 in display order; the cluster's anchor is e-Number.
        internal required int Number { get; init; }
        // Null for the tests without an error message.
        internal required ErrorClass? Error { get; init; }
        // The primary members first, each group in report order.
        internal required IReadOnlyList<Member> Members { get; init; }
        internal required int PrimaryCount { get; init; }
        // Slots whose value is not the same in every member; empty unless the members share a layout.
        internal required IReadOnlySet<int> Varying { get; init; }
        // The members show one layout: one template or one tokenized line, whose slots line up.
        internal bool SingleLayout { get; init; } = true;
        internal string? ExceptionType { get; init; }
        internal string? Frame { get; init; }
        internal int FrameCount { get; init; }
        internal bool IsGeneric => Error?.IsGeneric == true;
        internal Member Representative => Members[0];

        // The values of one slot, each once, in member order.
        internal IReadOnlyList<string> Values(int slot) =>
            [.. Members.Select(member => member.Parts.FirstOrDefault(part => part.Slot == slot)?.Text).OfType<string>().Distinct(StringComparer.Ordinal)];

        // A member's values for the varying slots, in line order.
        internal IReadOnlyList<string> ValuesOf(Member member) =>
            [.. member.Parts.Where(part => part.Slot >= 0 && Varying.Contains(part.Slot)).Select(static part => part.Text)];
    }

    // The clusters of a report: specific errors, by the tests whose primary error they are, then by
    // all their tests; then the generic errors in the same order; then the tests without a message.
    // Ties follow the first test's ordinal.
    internal static IReadOnlyList<Cluster> Of(TestFailureReportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Of(model.Failures, model.Errors);
    }

    // errors classified a list whose first entries are failures, in that order.
    internal static IReadOnlyList<Cluster> Of(IReadOnlyList<AdoTestFailure> failures, ErrorClassification errors)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Profiles.Count < failures.Count) throw new ArgumentException(null, nameof(errors));
        Dictionary<ErrorClass, List<Member>> byError = [];
        List<Member> silent = [];
        for (int index = 0; index < failures.Count; index++)
        {
            AdoTestFailure failure = failures[index];
            ErrorProfile profile = errors.Profiles[index];
            if (profile.Primary is null) { silent.Add(new Member { Failure = failure, Profile = profile }); continue; }
            ErrorComparison? comparison = index < errors.Comparisons.Count ? errors.Comparisons[index] : null;
            foreach (ErrorProfileEntry entry in profile.Entries)
            {
                AdoTestAttempt? attempt = failure.Attempts.FirstOrDefault(candidate => candidate.Number == entry.Latest.AttemptNumber);
                if (!byError.TryGetValue(entry.Class, out List<Member>? members)) byError[entry.Class] = members = [];
                members.Add(new Member
                {
                    Failure = failure, Profile = profile, Entry = entry, Attempt = attempt, Frame = ErrorText.RootFrame(failure, attempt), Comparison = comparison,
                });
            }
        }
        List<(ErrorClass? Error, Member[] Members)> ordered = [.. byError
            .Select(static pair => (Error: pair.Key, Members: (Member[])[.. pair.Value.Where(static member => member.IsPrimary), .. pair.Value.Where(static member => !member.IsPrimary)]))
            .OrderBy(static item => item.Error.IsGeneric ? 1 : 0)
            .ThenByDescending(static item => item.Members.Count(static member => member.IsPrimary)).ThenByDescending(static item => item.Members.Length)
            .ThenBy(static item => item.Members.Min(static member => member.Failure.Ordinal)).ThenBy(static item => item.Error.Id)
            .Select(static item => ((ErrorClass?)item.Error, item.Members))];
        if (silent.Count > 0) ordered.Add((null, [.. silent]));
        List<Cluster> clusters = [];
        foreach ((ErrorClass? error, Member[] members) in ordered)
        {
            bool single = members.Select(static member => member.Entry?.Latest.Form.Layout).Distinct(StringComparer.Ordinal).Count() == 1;
            HashSet<int> varying = !single ? [] : [.. members[0].Parts.Where(static part => part.Slot >= 0).Select(static part => part.Slot)
                .Where(slot => members.Select(member => member.Parts.FirstOrDefault(part => part.Slot == slot)?.Text).Distinct(StringComparer.Ordinal).Skip(1).Any())];
            (string? frame, int frameCount) = MostCommon(members.Select(static member => member.Frame));
            clusters.Add(new Cluster
            {
                Number = clusters.Count + 1, Error = error, Members = members, PrimaryCount = members.Count(static member => member.IsPrimary),
                Varying = varying, SingleLayout = single,
                ExceptionType = MostCommon(members.Select(static member => member.ExceptionType)).Value,
                Frame = frameCount >= 2 ? frame : null, FrameCount = frameCount >= 2 ? frameCount : 0,
            });
        }
        return clusters;
    }

    // The cluster of each entry's error, in the entries' order.
    internal static IReadOnlyList<Cluster> ClustersOf(IReadOnlyList<Cluster> clusters, IEnumerable<ErrorProfileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        ArgumentNullException.ThrowIfNull(entries);
        return [.. entries.Select(entry => clusters.First(cluster => ReferenceEquals(cluster.Error, entry.Class)))];
    }

    // The key of one line; equal keys mean one cluster.
    internal static string Key(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Tokenize(Clean(line)).Key;
    }

    // The key line of a message (ErrorText.KeyLine).
    internal static string? KeyLine(string? message) => ErrorText.KeyLine(message);

    // The innermost exception type of the message, else of the trace (ErrorText.ExceptionType).
    internal static string? ExceptionType(AdoTestAttempt? attempt) => ErrorText.ExceptionType(attempt);

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

    private static string Clean(string line) => ErrorText.Clean(line);

    [GeneratedRegex(@"(?<url>https?://[^\s""'<>]+)|(?<guid>\b[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\b)|(?<path>\b[A-Za-z]:\\[^\s""'<>|*?]*|\\\\[^\s""'<>|*?]+|(?<![\w.:/~-])/(?:[\w.@%+~-]+/)+[\w.@%+~-]*)|(?<hex>\b0[xX][0-9a-fA-F]+\b|\b(?=[0-9a-fA-F]*[0-9])(?=[0-9a-fA-F]*[a-fA-F])[0-9a-fA-F]{8,}\b)|(?<number>[0-9]+(?:[.,][0-9]+)*)", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();

    [GeneratedRegex(@"\.<(?<name>[^>]+)>d__[0-9]+\.MoveNext$", RegexOptions.CultureInvariant)]
    private static partial Regex AsyncStateMachine();
}
