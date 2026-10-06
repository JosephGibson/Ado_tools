using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.Errors;

// Classifies every failed attempt of a report's tests. It reads the form of each message, joins the
// English and French forms of one error, and gives each test its profile. Two forms join when one
// test failed with them at the same place, with the same exception type and the same frames of its
// own code, in an English group and a French group; a group's language is the one its framework
// texts show. A class holds at most one form per group, and a join that would break that is refused,
// so two forms seen in one group never become one error. Deterministic: tests and attempts are
// visited in order and no order comes from a hash. Linear in tests × attempts: a message is read
// once, however many attempts repeat it.
internal static class ErrorClassifier
{
    internal static ErrorClassification Classify(IReadOnlyList<AdoTestFailure> failures, PipelineGrouping grouping, IReadOnlyList<ErrorRule> rules)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(grouping);
        ArgumentNullException.ThrowIfNull(rules);
        Dictionary<string, (ErrorSignature Form, ErrorLanguage Evidence)> read = new(StringComparer.Ordinal);
        List<ErrorOccurrence>[] occurrences = new List<ErrorOccurrence>[failures.Count];
        for (int test = 0; test < failures.Count; test++)
        {
            AdoTestFailure failure = failures[test];
            List<ErrorOccurrence> list = occurrences[test] = [];
            foreach (AdoTestAttempt attempt in failure.Attempts.OrderBy(static attempt => attempt.Number))
                if (attempt.OutcomeClass == AdoTestOutcomeClass.Failure && Occurrence(test, failure, attempt, true) is { } occurrence) list.Add(occurrence);
            // No failed attempt has a message: the latest attempt with one stands for the test, as
            // the latest error does.
            if (list.Count == 0 && failure.Attempts.OrderBy(static attempt => attempt.Number)
                .LastOrDefault(static attempt => ErrorText.ScannedLines(attempt.ErrorMessage).Count > 0) is { } fallback)
                list.Add(Occurrence(test, failure, fallback, false)!);
        }

        // Forms in the order they first appear, and the groups each was seen in.
        List<ErrorSignature> forms = [];
        Dictionary<string, int> formIndex = new(StringComparer.Ordinal);
        foreach (ErrorOccurrence occurrence in occurrences.SelectMany(static list => list))
            if (formIndex.TryAdd(occurrence.Form.Key, forms.Count)) forms.Add(occurrence.Form);
        Unions unions = new(forms.Count);
        Dictionary<ErrorRule, int> order = new(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < rules.Count; index++) order.TryAdd(rules[index], index);
        foreach (ErrorOccurrence occurrence in occurrences.SelectMany(static list => list))
            if (occurrence.Group is int group) unions.See(formIndex[occurrence.Form.Key], group);

        Dictionary<int, ErrorLanguage> languages = GroupLanguages(occurrences);
        for (int test = 0; test < failures.Count; test++) Pair(test, occurrences[test], languages, forms, formIndex, unions);

        // Classes in the order their first form appears.
        List<ErrorClass> classes = [];
        Dictionary<int, ErrorClass> classOfRoot = [];
        List<int>[] members = [.. Enumerable.Range(0, forms.Count).Select(static _ => new List<int>())];
        for (int index = 0; index < forms.Count; index++) members[unions.Find(index)].Add(index);
        for (int index = 0; index < forms.Count; index++)
        {
            int root = unions.Find(index);
            if (classOfRoot.ContainsKey(root)) continue;
            ErrorSignature[] classForms = [.. members[root].Select(member => forms[member])];
            classOfRoot[root] = new ErrorClass
            {
                // Pairing can join two wordings that two rules named: the rule that comes first in the
                // list, a configured one before a built-in one, names the error.
                Id = classes.Count, Forms = classForms, Rule = classForms.Select(static form => form.Rule).OfType<ErrorRule>().MinBy(rule => order[rule]),
                Pairings = unions.Pairings(root),
            };
            classes.Add(classOfRoot[root]);
        }

        ErrorProfile[] profiles = new ErrorProfile[failures.Count];
        for (int test = 0; test < failures.Count; test++)
        {
            ErrorProfileEntry[] entries = [.. occurrences[test]
                .GroupBy(occurrence => classOfRoot[unions.Find(formIndex[occurrence.Form.Key])])
                .Select(static group => new ErrorProfileEntry
                {
                    Class = group.Key, Count = group.Count(), Attempts = [.. group.Select(static occurrence => occurrence.AttemptNumber)],
                    Groups = [.. group.Select(static occurrence => occurrence.Group).OfType<int>().Distinct().Order()],
                    Latest = group.MaxBy(static occurrence => occurrence.AttemptNumber)!,
                })
                .OrderByDescending(static entry => entry.Count).ThenByDescending(static entry => entry.Latest.AttemptNumber)];
            ErrorProfileEntry[] candidates = entries.Any(static entry => !entry.Class.IsGeneric) ? [.. entries.Where(static entry => !entry.Class.IsGeneric)] : entries;
            ErrorProfileEntry? primary = candidates.FirstOrDefault();
            profiles[test] = new ErrorProfile
            {
                Test = test, Entries = entries, Primary = primary,
                FailedAttempts = failures[test].Attempts.Count(static attempt => attempt.OutcomeClass == AdoTestOutcomeClass.Failure),
                IsTie = primary is not null && candidates.Count(entry => entry.Count == primary.Count) > 1,
            };
        }
        ErrorComparison?[] comparisons = new ErrorComparison?[failures.Count];
        for (int test = 0; test < failures.Count; test++) comparisons[test] = Compare(failures[test], profiles[test], grouping, scanned => Form(scanned, null).Form);
        return new ErrorClassification { Profiles = profiles, Classes = classes, Tests = failures, Comparisons = comparisons };

        // Each distinct message is read once, with its frame.
        (ErrorSignature Form, ErrorLanguage Evidence) Form(IReadOnlyList<string> scanned, string? frame)
        {
            string memo = string.Join('\n', scanned) + "\u0000" + frame;
            if (!read.TryGetValue(memo, out (ErrorSignature Form, ErrorLanguage Evidence) form)) read[memo] = form = ErrorForms.Read(scanned, frame, rules);
            return form;
        }

        ErrorOccurrence? Occurrence(int test, AdoTestFailure failure, AdoTestAttempt attempt, bool failed)
        {
            IReadOnlyList<string> scanned = ErrorText.ScannedLines(attempt.ErrorMessage);
            if (scanned.Count == 0) return null;
            string? frame = ErrorText.RootFrame(failure, attempt);
            (ErrorSignature Form, ErrorLanguage Evidence) form = Form(scanned, frame);
            return new ErrorOccurrence
            {
                Test = test, AttemptNumber = attempt.Number, Group = grouping.GroupOf(attempt.RunId), Failed = failed, Form = form.Form,
                Evidence = form.Evidence, ExceptionType = ErrorText.ExceptionType(attempt), Fingerprint = failed ? ErrorText.Fingerprint(failure, attempt) : null,
            };
        }
    }

    // The test's primary error against the latest earlier build where it failed or was flaky (D-8);
    // nothing when that build's listing kept no message, since an older build is not the previous
    // one. The listed starts are read as the attempts are, without a trace. Same when one has a form
    // of the primary error. Another error needs evidence: a start that was not cut, from a stage or
    // job where the test had its primary error in this build, and no start dropped. A primary error
    // keyed on the test's own frame cannot be compared.
    private static ErrorComparison? Compare(AdoTestFailure failure, ErrorProfile profile, PipelineGrouping grouping,
        Func<IReadOnlyList<string>, ErrorSignature> read)
    {
        if (profile.Primary is not { Latest.Failed: true } primary || primary.Class.Forms.Any(static form => form.IsLocated)) return null;
        AdoTestHistoryEntry? previous = failure.History.TakeWhile(static entry => !entry.IsCurrent)
            .LastOrDefault(static entry => entry.Outcome is AdoTestHistoryOutcome.Failed or AdoTestHistoryOutcome.Flaky);
        if (previous is not { ErrorMessages.Count: > 0 }) return null;
        HashSet<string> keys = new(primary.Class.Forms.Select(static form => form.Key), StringComparer.Ordinal);
        HashSet<string> places = new(primary.Attempts.Select(number => failure.Attempts.FirstOrDefault(attempt => attempt.Number == number)?.RunId)
            .OfType<int>().Select(grouping.TryKeyOf).OfType<string>(), StringComparer.Ordinal);
        bool evidence = false;
        for (int index = 0; index < previous.ErrorMessages.Count; index++)
        {
            string start = previous.ErrorMessages[index];
            IReadOnlyList<string> scanned = ErrorText.ScannedLines(start);
            if (scanned.Count == 0) continue;
            if (keys.Contains(read(scanned).Key)) return new ErrorComparison(previous.BuildId, previous.BuildNumber, true);
            if (start.Length < ErrorMessageStart.ListingCut && index < previous.ErrorMessageKeys.Count && places.Contains(previous.ErrorMessageKeys[index])) evidence = true;
        }
        return evidence && !previous.ErrorMessagesDropped ? new ErrorComparison(previous.BuildId, previous.BuildNumber, false) : null;
    }

    // English or French for a group whose framework texts show that language only; Neutral otherwise.
    private static Dictionary<int, ErrorLanguage> GroupLanguages(IEnumerable<List<ErrorOccurrence>> occurrences)
    {
        Dictionary<int, HashSet<ErrorLanguage>> seen = [];
        foreach (ErrorOccurrence occurrence in occurrences.SelectMany(static list => list))
        {
            if (occurrence.Group is not int group || occurrence.Evidence == ErrorLanguage.Neutral) continue;
            if (!seen.TryGetValue(group, out HashSet<ErrorLanguage>? languages)) seen[group] = languages = [];
            languages.Add(occurrence.Evidence);
        }
        return seen.ToDictionary(static pair => pair.Key, static pair => pair.Value.Count == 1 ? pair.Value.First() : ErrorLanguage.Neutral);
    }

    // Joins the forms one test failed with at the same place in an English group and a French group.
    private static void Pair(int test, List<ErrorOccurrence> occurrences, Dictionary<int, ErrorLanguage> languages, List<ErrorSignature> forms,
        Dictionary<string, int> formIndex, Unions unions)
    {
        foreach (var bucket in occurrences.Where(static occurrence => occurrence.Failed && occurrence.Fingerprint is not null && occurrence.Group is not null)
            .GroupBy(static occurrence => (Type: occurrence.ExceptionType ?? string.Empty, Fingerprint: occurrence.Fingerprint!)))
        {
            // Two forms in one group at the same place: which one is the other group's is unknown.
            var byGroup = bucket.GroupBy(static occurrence => occurrence.Group!.Value)
                .Select(group => (Group: group.Key, Forms: group.Select(occurrence => formIndex[occurrence.Form.Key]).Distinct().ToArray()))
                .OrderBy(static group => group.Group).ToArray();
            if (byGroup.Any(static group => group.Forms.Length > 1)) continue;
            foreach (var english in byGroup.Where(group => languages.GetValueOrDefault(group.Group) == ErrorLanguage.English))
                foreach (var french in byGroup.Where(group => languages.GetValueOrDefault(group.Group) == ErrorLanguage.French))
                {
                    int first = english.Forms[0], second = french.Forms[0];
                    if (first == second) continue;
                    // A template written in both languages, read in the same language on both sides:
                    // its identity differs in that language, so these are two errors.
                    if (forms[first].TemplateId is { } id && id == forms[second].TemplateId && forms[first].Language != ErrorLanguage.Neutral
                        && forms[first].Language == forms[second].Language) continue;
                    unions.Join(first, second, new ErrorPairing(test, english.Group, french.Group));
                }
        }
    }

    // Disjoint sets of forms, with the groups each set was seen in, by form.
    private sealed class Unions
    {
        private readonly int[] parent;
        private readonly Dictionary<int, int>[] groups;
        private readonly List<ErrorPairing>[] pairings;

        internal Unions(int count)
        {
            parent = [.. Enumerable.Range(0, count)];
            groups = [.. Enumerable.Range(0, count).Select(static _ => new Dictionary<int, int>())];
            pairings = [.. Enumerable.Range(0, count).Select(static _ => new List<ErrorPairing>())];
        }

        internal void See(int form, int group) => groups[form].TryAdd(group, form);

        internal int Find(int form)
        {
            while (parent[form] != form) form = parent[form] = parent[parent[form]];
            return form;
        }

        internal ErrorPairing[] Pairings(int root) => [.. pairings[root]];

        // Joins the sets of two forms, unless a group saw one form of each: the earlier set keeps its root.
        internal void Join(int first, int second, ErrorPairing pairing)
        {
            int left = Find(first), right = Find(second);
            if (left == right) return;
            foreach ((int group, int form) in groups[right])
                if (groups[left].TryGetValue(group, out int other) && other != form) return;
            (int root, int child) = left < right ? (left, right) : (right, left);
            foreach ((int group, int form) in groups[child]) groups[root].TryAdd(group, form);
            pairings[root].AddRange(pairings[child]);
            pairings[root].Add(pairing);
            parent[child] = root;
        }
    }
}
