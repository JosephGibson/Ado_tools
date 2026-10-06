using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting.Errors;

// The catalog: the error texts of the test frameworks and of Selenium that the report recognizes, in
// English and in French where the framework writes both. Each form was read from a package, never
// written from memory: MSTest.TestFramework and MSTest.TestAdapter 2.2.10, 3.11.1 and 4.4.1 (their
// fr resources, and messages captured under en-US and fr-FR), NUnit 3.14.0 and 4.6.1, xunit.assert
// 2.9.3, xunit.v3.assert 4.0.1, and Selenium.WebDriver and Selenium.Support 4.50.0. NUnit, xUnit and
// Selenium write English only. Patterns run on folded lines (FoldedText): lower case, no accents,
// one space for a run of spaces.
internal static class ErrorTemplates
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    // Lines after the head that a multi-line template reads.
    private const int MaximumFollowingLines = 10;
    // Slots by role, the same in every form of a template so that their values line up.
    private const int ExpectedSlot = 0, ActualSlot = 1, MessageSlot = 2, ExtraSlot = 3, SecondExtraSlot = 4, ExpressionSlot = 5;
    private static readonly (string Group, int Slot)[] Holes =
        [("e", ExpectedSlot), ("a", ActualSlot), ("m", MessageSlot), ("d", ExtraSlot), ("x", ExtraSlot), ("y", SecondExtraSlot), ("n", ExtraSlot), ("t", SecondExtraSlot)];

    // MSTest's "Test method {0}.{1} threw exception:" (UTA_TestMethodThrows), which names the test, not
    // the error: the error is on the lines below it. 2.2.10 and 3.11.1 keep a space before the line
    // break, 4.4.1 does not; folding trims it.
    private static readonly Regex WrapperEnglish = new(@"^test method .+ threw exception:$", Options);
    private static readonly Regex WrapperFrench = new(@"^la methode de test .+ a leve une exception :$", Options);

    // The language of MSTest's wrapper on the first line, or null when there is none.
    internal static ErrorLanguage? Wrapper(ErrorLines lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count < 2) return null;
        string first = lines.Folded(0).Text;
        return WrapperEnglish.IsMatch(first) ? ErrorLanguage.English : WrapperFrench.IsMatch(first) ? ErrorLanguage.French : null;
    }

    // The template that the line at start begins, or null.
    internal static TemplateMatch? Match(ErrorLines lines, int start)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (start < 0 || start >= lines.Count) return null;
        return Adapter(lines, start) ?? Classic(lines, start) ?? Modern(lines, start) ?? Xunit(lines, start) ?? NUnit(lines, start) ?? Wait(lines, start);
    }

    // MSTest adapter failures around a test (Resource UTA_*): initialization, cleanup and timeout,
    // identical in the three versions except where a second form is listed.
    private static readonly (string Id, Regex English, Regex French, string[] Identity)[] AdapterForms =
    [
        ("MsTestClassInitialize", new(@"^class initialization method (?<method>.+?) threw exception\. (?<type>[^ ]+?): (?<m>.*?)\.?$", Options),
            new(@"^la methode de classe initialization (?<method>.+?) a leve une exception\. (?<type>[^ ]+?) ?: (?<m>.*?)\.?$", Options), ["method", "type", "m"]),
        ("MsTestAssemblyInitialize", new(@"^assembly initialization method (?<method>.+?) threw exception\. (?<type>[^ ]+?): (?<m>.*?)\. aborting test execution\.$", Options),
            new(@"^la methode d'assembly initialization (?<method>.+?) a leve une exception\. (?<type>[^ ]+?) ?: (?<m>.*?)\. abandon de l'execution de tests\.$", Options),
            ["method", "type", "m"]),
        ("MsTestTestInitialize", new(@"^initialization method (?<method>.+?) threw exception\. (?<m>.*?)\.?$", Options),
            new(@"^la methode initialization (?<method>.+?) a leve une exception\. (?<m>.*?)\.?$", Options), ["method", "m"]),
        ("MsTestTestCleanup", new(@"^testcleanup method (?<method>.+?) threw exception\. (?<m>.*?)\.?$", Options),
            new(@"^la methode testcleanup (?<method>.+?) a leve une exception\. (?<m>.*?)\.?$", Options), ["method", "m"]),
        ("MsTestClassCleanup", new(@"^class cleanup method (?<method>.+?) failed\. error message: (?<m>.*?)\. stack trace: (?<t>.*)$", Options),
            new(@"^la methode de classe cleanup (?<method>.+?) a echoue\. message d'erreur : (?<m>.*?)\. trace de la pile : (?<t>.*)$", Options), ["method", "m"]),
        ("MsTestAssemblyCleanup", new(@"^assembly cleanup method (?<method>.+?) failed\. error message: (?<m>.*?)\. stacktrace: (?<t>.*)$", Options),
            new(@"^la methode cleanup d'assembly (?<method>.+?) a echoue\. message d'erreur : (?<m>.*?)\. stacktrace : (?<t>.*)$", Options), ["method", "m"]),
        // 2.2.10: Execution_Test_Timeout before 3.x.
        ("MsTestTimeout", new(@"^test '(?<t>.*)' exceeded execution timeout period\.$", Options),
            new(@"^le test '(?<t>.*)' a depasse le delai d'attente de l'execution\.$", Options), []),
        // 3.11.1 and 4.4.1.
        ("MsTestTimedOut", new(@"^test '(?<t>.*)' timed out after (?<n>[0-9]+)ms$", Options),
            new(@"^le test « (?<t>.*) » a expire apres (?<n>[0-9]+) ms$", Options), []),
    ];

    private static TemplateMatch? Adapter(ErrorLines lines, int start)
    {
        FoldedText line = lines.Folded(start);
        foreach ((string id, Regex english, Regex french, string[] identity) in AdapterForms)
        {
            ErrorLanguage language = ErrorLanguage.English;
            Match match = english.Match(line.Text);
            if (!match.Success) { match = french.Match(line.Text); language = ErrorLanguage.French; }
            if (!match.Success) continue;
            List<ErrorPart> parts = [];
            AddParts(parts, line, match);
            return new TemplateMatch
            {
                Id = id, Language = language, Parts = parts, Identity = [.. identity.Select(group => Key(match.Groups[group]))],
                IsLocated = identity.Length == 0,
            };
        }
        return null;
    }

    // MSTest 2.x and 3.x: "{0} failed. {1}" (AssertionFailed) and its French "Échec de {0}. {1}", with
    // the message of each assertion after it. Variants come before the catch-all of their prefix.
    private static readonly (string Id, Regex English, Regex French, string[] Identity, bool Strings)[] ClassicForms =
    [
        // 3.11.1 strings: AreEqualStringDiffLengthDifferentMsg or ...LengthBothMsg, then lines that
        // AreEqualStringDiffExpectedPrefix and ...ActualPrefix start.
        ("MsTestAreEqualStrings",
            new(@"^assert\.areequal failed\. (?:expected string length (?<x>[0-9]+) but was (?<y>[0-9]+)|string lengths are both (?<x>[0-9]+) but differ at index (?<y>[0-9]+))\.(?: (?<m>.*))?$", Options),
            new(@"^echec de assert\.areequal\. (?:la longueur de chaine attendue (?<x>[0-9]+) mais etait (?<y>[0-9]+)|les longueurs de chaine sont toutes les deux (?<x>[0-9]+) mais different a l'index (?<y>[0-9]+))\.(?: (?<m>.*))?$", Options),
            ["m"], true),
        // AreEqualFailMsg.
        ("MsTestAreEqual", new(@"^assert\.areequal failed\. expected:<(?<e>.*?)>\. actual:<(?<a>.*?)>\.(?: (?<m>.*))?$", Options),
            new(@"^echec de assert\.areequal\. attendu : <(?<e>.*?)>, reel : <(?<a>.*?)>\.(?: (?<m>.*))?$", Options), ["e", "m"], false),
        // AreEqualCaseFailMsg.
        ("MsTestAreEqualCase", new(@"^assert\.areequal failed\. expected:<(?<e>.*?)>\. case is different for actual value:<(?<a>.*?)>\.(?: (?<m>.*))?$", Options),
            new(@"^echec de assert\.areequal\. attendu :<(?<e>.*?)>\. la casse est differente pour la valeur reelle :<(?<a>.*?)>\.(?: (?<m>.*))?$", Options), ["e", "m"], false),
        // AreEqualDeltaFailMsg.
        ("MsTestAreEqualDelta",
            new(@"^assert\.areequal failed\. expected a difference no greater than <(?<d>.*?)> between expected value <(?<e>.*?)> and actual value <(?<a>.*?)>\.(?: (?<m>.*))?$", Options),
            new(@"^echec de assert\.areequal\. difference attendue non superieure a <(?<d>.*?)> comprise entre la valeur attendue <(?<e>.*?)> et la valeur reelle <(?<a>.*?)>\.(?: (?<m>.*))?$", Options),
            ["d", "e", "m"], false),
        // AreNotEqualFailMsg.
        ("MsTestAreNotEqual", new(@"^assert\.arenotequal failed\. expected any value except:<(?<e>.*?)>\. actual:<(?<a>.*?)>\.(?: (?<m>.*))?$", Options),
            new(@"^echec de assert\.arenotequal\. toute valeur attendue sauf :<(?<e>.*?)>\. reel :<(?<a>.*?)>\.(?: (?<m>.*))?$", Options), ["e", "m"], false),
        // IsInstanceOfFailMsg: "{0} Expected type:<{1}>. Actual type:<{2}>.", the message first.
        ("MsTestInstanceOf", new(@"^assert\.isinstanceoftype failed\. (?:(?<m>.*?) )?expected type:<(?<e>.*?)>\. actual type:<(?<a>.*?)>\.$", Options),
            new(@"^echec de assert\.isinstanceoftype\. (?:(?<m>.*?) ?)?type attendu :<(?<e>.*?)>\. type reel :<(?<a>.*?)>\.$", Options), ["e", "m"], false),
        // IsNotInstanceOfFailMsg.
        ("MsTestNotInstanceOf", new(@"^assert\.isnotinstanceoftype failed\. wrong type:<(?<e>.*?)>\. actual type:<(?<a>.*?)>\.(?: (?<m>.*))?$", Options),
            new(@"^echec de assert\.isnotinstanceoftype\. type incorrect : <(?<e>.*?)>, type reel : <(?<a>.*?)>\.(?: (?<m>.*))?$", Options), ["e", "m"], false),
        // ContainsFail, StartsWithFail, EndsWithFail, IsMatchFail and IsNotMatchFail; 3.x drops the last period.
        ("MsTestStringAssert",
            new(@"^(?<api>stringassert\.[a-z]+) failed\. string '(?<a>.*?)' (?:does not contain string|does not start with string|does not end with string|does not match pattern|matches pattern) '(?<e>.*?)'\.(?: (?<m>.*?))?\.?$", Options),
            new(@"^echec de (?<api>stringassert\.[a-z]+)\. la chaine '(?<a>.*?)' (?:ne contient pas la chaine|ne commence pas par la chaine|ne se termine pas par la chaine|ne correspond pas au modele|correspond au modele) '(?<e>.*?)'\.(?: (?<m>.*?))?\.?$", Options),
            ["api", "e", "m"], false),
        // An assertion whose only text is the test author's message.
        ("MsTestCondition", new(@"^(?<api>assert\.(?:istrue|isfalse|isnull|isnotnull|fail|inconclusive)) failed\.(?: (?<m>.*))?$", Options),
            new(@"^echec de (?<api>assert\.(?:istrue|isfalse|isnull|isnotnull|fail|inconclusive))\.(?: (?<m>.*))?$", Options), ["api", "m"], false),
        // Any other assertion.
        ("MsTestAssert", new(@"^(?<api>(?:assert|stringassert|collectionassert)\.[a-z]+) failed\.(?: (?<m>.*))?$", Options),
            new(@"^echec de (?<api>(?:assert|stringassert|collectionassert)\.[a-z]+)\.(?: (?<m>.*))?$", Options), ["api", "m"], false),
    ];

    private static readonly Regex ClassicExpectedEnglish = new(@"^expected: (?<e>.*)$", Options);
    private static readonly Regex ClassicActualEnglish = new(@"^but was: (?<a>.*)$", Options);
    private static readonly Regex ClassicExpectedFrench = new(@"^attendu : (?<e>.*)$", Options);
    private static readonly Regex ClassicActualFrench = new(@"^mais c'etait : (?<a>.*)$", Options);

    private static TemplateMatch? Classic(ErrorLines lines, int start)
    {
        FoldedText line = lines.Folded(start);
        foreach ((string id, Regex english, Regex french, string[] identity, bool strings) in ClassicForms)
        {
            ErrorLanguage language = ErrorLanguage.English;
            Match match = english.Match(line.Text);
            if (!match.Success) { match = french.Match(line.Text); language = ErrorLanguage.French; }
            if (!match.Success) continue;
            List<ErrorPart> parts = [];
            AddParts(parts, line, match);
            List<string> keys = [.. identity.Select(group => Key(match.Groups[group]))];
            if (strings)
            {
                Regex expected = language == ErrorLanguage.English ? ClassicExpectedEnglish : ClassicExpectedFrench;
                Regex actual = language == ErrorLanguage.English ? ClassicActualEnglish : ClassicActualFrench;
                // The expected value identifies the assertion; without its line nothing does.
                if (Following(lines, start, expected) is not ({ } expectedLine, { } expectedMatch)) return null;
                AddParts(parts, expectedLine, expectedMatch);
                keys.Insert(0, Key(expectedMatch.Groups["e"]));
                if (Following(lines, start, actual) is ({ } actualLine, { } actualMatch)) AddParts(parts, actualLine, actualMatch);
            }
            bool located = id == "MsTestCondition" && keys[^1].Length == 0;
            return new TemplateMatch { Id = id, Language = language, Parts = parts, Identity = keys, IsLocated = located };
        }
        return null;
    }

    // MSTest 4.x: "Assertion failed." (AssertionFailed, the same in French), the summary of the
    // assertion (*FailedSummary) in the culture's language, the test author's message, labeled lines
    // in English in both languages, and the call as written. A summary with no French form, such as
    // the type summary, is the same in both.
    private static readonly Dictionary<string, (string Kind, ErrorLanguage Language)> Summaries = BuildSummaries();
    private static readonly Regex ModernHead = new(@"^assertion failed\.(?: (?<s>.*))?$", Options);
    private static readonly Regex ModernTypeSummary = new(@"^expected value to be of type .* \(or derived\)\.$", Options);
    private static readonly Regex ModernDetail = new(
        @"^(?:strings have different lengths \(expected: [0-9]+, actual: [0-9]+\) and differ at [0-9]+ location\(s\)\. first difference at index [0-9]+\.|strings have same length \([0-9]+\) and differ at [0-9]+ location\(s\)\. first difference at index [0-9]+\.|strings differ only in case\.|les chaines ont des longueurs differentes \(attendues : [0-9]+, reelles : [0-9]+\) et different a [0-9]+ emplacement\(s\)\. premiere difference a l'indice [0-9]+\.|les chaines ont la meme longueur \([0-9]+\) et different a [0-9]+ emplacement(?:\(s\))?\. premiere difference a l'indice [0-9]+\.|les chaines ne different que par la casse\.)$",
        Options);
    // On the line as sent: MSTest writes its labels in lower case and its calls as written.
    private static readonly Regex ModernLabel = new(@"^(?<label>[a-z][a-zA-Z ]*):[ ]+(?<v>.*)$", Options);
    private static readonly Regex Call = new(@"^(?:Assert|StringAssert|CollectionAssert|ClassicAssert)\.[A-Za-z]+\(.*\)$", Options);
    private static readonly string[] LocatedKinds = ["IsTrue", "IsFalse", "IsNull", "IsNotNull", "Fail"];

    private static TemplateMatch? Modern(ErrorLines lines, int start)
    {
        Match head = ModernHead.Match(lines.Folded(start).Text);
        if (!head.Success) return null;
        string summary = head.Groups["s"].Value;
        (string kind, ErrorLanguage language) = summary.Length == 0 ? ("Fail", ErrorLanguage.Neutral)
            : Summaries.TryGetValue(summary, out var known) ? known
            : ModernTypeSummary.IsMatch(summary) ? ("IsInstanceOfType", ErrorLanguage.Neutral)
            : ("?" + ErrorText.Tokenize(FoldedText.Of(summary)).Key, ErrorLanguage.Neutral);
        List<ErrorPart> parts = [new ErrorPart(lines.Lines[start], -1)];
        List<string> expected = [], message = [];
        string? call = null;
        int calls = 0;
        for (int index = start + 1; index < lines.Count && index <= start + MaximumFollowingLines; index++)
        {
            string text = lines.Lines[index];
            if (Call.IsMatch(text)) { call = text; calls = index; continue; }
            if (ModernDetail.IsMatch(lines.Folded(index).Text)) continue;
            Match label = ModernLabel.Match(text);
            if (label.Success)
            {
                string name = label.Groups["label"].Value;
                if (name is "difference" or "comparison") continue;
                bool identity = name.StartsWith("expected", StringComparison.Ordinal) || name.StartsWith("notExpected", StringComparison.Ordinal);
                if (identity) expected.Add(Key(FoldedText.Of(label.Groups["v"].Value).Text));
                parts.Add(new ErrorPart(" · ", -1));
                parts.Add(new ErrorPart(text[..label.Groups["v"].Index], -1));
                parts.Add(new ErrorPart(label.Groups["v"].Value, identity ? ExpectedSlot : ActualSlot));
                continue;
            }
            // The author's line identifies, so it shows too.
            message.Add(Key(lines.Folded(index).Text));
            parts.Add(new ErrorPart(" · ", -1));
            parts.Add(new ErrorPart(text, -1));
        }
        bool locatedKind = LocatedKinds.Contains(kind, StringComparer.Ordinal);
        List<string> keys = [.. expected, string.Join('\n', message)];
        if (call is not null)
        {
            parts.Add(new ErrorPart(" · ", -1));
            // A condition has no expected value: its call tells one assertion from another.
            if (locatedKind) { keys.Add(FoldedText.Of(call).Text); parts.Add(new ErrorPart(call, -1)); }
            else parts.Add(new ErrorPart(call, ExpressionSlot));
        }
        return new TemplateMatch
        {
            Id = "MsTest4." + kind, Language = language, Parts = parts, Identity = keys,
            IsLocated = locatedKind && message.Count == 0 && call is null,
        };
    }

    // xUnit: "Assert.X() Failure" and its reason, then labeled lines, which xunit.assert 2.9.3 and
    // xunit.v3.assert 4.0.1 write alike. A condition with the author's message is that message alone,
    // which the text form reads.
    private static readonly Regex XunitHead = new(@"^assert\.(?<api>[a-z]+)\(\) failure(?:: (?<d>.*))?$", Options);
    private static readonly Regex XunitLabel = new(@"^(?<label>[a-z][a-z ]*): (?<v>.*)$", Options);
    private static readonly Regex XunitMarker = new(@"^[↓↑] \(pos [0-9]+\)$", Options);
    private static readonly string[] XunitIdentityLabels = ["expected", "not found", "expected type", "expected end", "expected start", "regex", "range"];
    private static readonly string[] XunitLocated = ["true", "false", "null", "notnull"];

    private static TemplateMatch? Xunit(ErrorLines lines, int start)
    {
        FoldedText line = lines.Folded(start);
        Match head = XunitHead.Match(line.Text);
        if (!head.Success) return null;
        List<ErrorPart> parts = [];
        AddParts(parts, line, head, []);
        string api = head.Groups["api"].Value;
        List<string> keys = [api, Key(head.Groups["d"])];
        for (int index = start + 1; index < lines.Count && index <= start + MaximumFollowingLines; index++)
        {
            FoldedText next = lines.Folded(index);
            if (XunitMarker.IsMatch(next.Text)) continue;
            Match label = XunitLabel.Match(next.Text);
            if (!label.Success) break;
            bool identity = XunitIdentityLabels.Contains(label.Groups["label"].Value, StringComparer.Ordinal);
            if (identity) keys.Add(Key(label.Groups["v"]));
            AddParts(parts, next, label, [("v", identity ? ExpectedSlot : ActualSlot)]);
        }
        return new TemplateMatch { Id = "Xunit." + api, Language = ErrorLanguage.Neutral, Parts = parts, Identity = keys, IsLocated = XunitLocated.Contains(api, StringComparer.Ordinal) };
    }

    // NUnit 3.14.0 and 4.6.1: the author's message and, from 4.x, the call as written, an optional
    // string length line, then "Expected: " and "But was:  ", two spaces in, which trimming drops.
    private static readonly Regex NUnitExpected = new(@"^expected: (?<e>.*)$", Options);
    private static readonly Regex NUnitActual = new(@"^but was: (?<a>.*)$", Options);
    private static readonly Regex NUnitLength = new(
        @"^(?:expected string length [0-9]+ but was [0-9]+|string lengths are both [0-9]+)\. strings differ at index [0-9]+\.$", Options);
    private static readonly string[] NUnitLocated = ["true", "false", "null", "not null"];

    private static TemplateMatch? NUnit(ErrorLines lines, int start)
    {
        for (int index = start; index < lines.Count && index <= start + 6; index++)
        {
            Match expected = NUnitExpected.Match(lines.Folded(index).Text);
            if (!expected.Success) continue;
            if (index + 1 >= lines.Count || NUnitActual.Match(lines.Folded(index + 1).Text) is not { Success: true } actual) return null;
            List<string> message = [];
            string? call = null;
            for (int before = start; before < index; before++)
            {
                if (Call.IsMatch(lines.Lines[before])) call = lines.Lines[before];
                else if (!NUnitLength.IsMatch(lines.Folded(before).Text)) message.Add(Key(lines.Folded(before).Text));
            }
            // Every line before Expected shows, from the key line: the author's message identifies.
            List<ErrorPart> parts = [];
            for (int before = start; before < index; before++)
            {
                if (parts.Count > 0) parts.Add(new ErrorPart(" · ", -1));
                parts.Add(new ErrorPart(lines.Lines[before], -1));
            }
            AddParts(parts, lines.Folded(index), expected);
            AddParts(parts, lines.Folded(index + 1), actual);
            string value = expected.Groups["e"].Value;
            bool locatedValue = NUnitLocated.Contains(value, StringComparer.Ordinal) && message.Count == 0;
            List<string> keys = [Key(expected.Groups["e"]), string.Join('\n', message)];
            if (locatedValue && call is not null) keys.Add(FoldedText.Of(call).Text);
            return new TemplateMatch
            {
                Id = "NUnit.Constraint", Language = ErrorLanguage.Neutral, Parts = parts, Identity = keys, IsLocated = locatedValue && call is null,
            };
        }
        return null;
    }

    // Selenium.Support 4.50.0 DefaultWait: "Timed out after {0} seconds", then ": " and the wait's
    // message when it has one; WebDriverTimeoutException leads it.
    private static readonly Regex WaitTimeout = new(
        @"^(?:[a-z_][a-z0-9_.+`]*(?:exception|error) ?: )?timed out after (?<n>[0-9][0-9.,]*) seconds(?:: (?<m>.*))?$", Options);

    private static TemplateMatch? Wait(ErrorLines lines, int start)
    {
        FoldedText line = lines.Folded(start);
        Match match = WaitTimeout.Match(line.Text);
        if (!match.Success) return null;
        List<ErrorPart> parts = [];
        AddParts(parts, line, match);
        string message = Key(match.Groups["m"]);
        return new TemplateMatch { Id = "Selenium.WaitTimeout", Language = ErrorLanguage.Neutral, Parts = parts, Identity = [message], IsLocated = message.Length == 0 };
    }

    // The first line after start, within the template's reach, that the pattern matches.
    private static (FoldedText? Line, Match? Match) Following(ErrorLines lines, int start, Regex pattern)
    {
        for (int index = start + 1; index < lines.Count && index <= start + MaximumFollowingLines; index++)
        {
            Match match = pattern.Match(lines.Folded(index).Text);
            if (match.Success) return (lines.Folded(index), match);
        }
        return (null, null);
    }

    // The parts of one line from a match of its folded text: each hole the match found is a slot, the
    // text around them is literal, and every part is cut from the line as sent.
    private static void AddParts(List<ErrorPart> parts, FoldedText line, Match match, (string Group, int Slot)[]? holes = null)
    {
        if (parts.Count > 0) parts.Add(new ErrorPart(" · ", -1));
        List<(int Start, int End, int Slot)> found = [];
        foreach ((string group, int slot) in holes ?? Holes)
        {
            Group hole = match.Groups[group];
            if (!hole.Success || hole.Length == 0) continue;
            found.Add((line.OriginalStart(hole.Index), line.OriginalEnd(hole.Index + hole.Length), slot));
        }
        found.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        int position = 0;
        foreach ((int start, int end, int slot) in found)
        {
            if (start < position) continue;
            if (start > position) parts.Add(new ErrorPart(line.Original[position..start], -1));
            parts.Add(new ErrorPart(line.Original[start..end], slot));
            position = end;
        }
        if (position < line.Original.Length) parts.Add(new ErrorPart(line.Original[position..], -1));
    }

    private static string Key(Group group) => group.Success ? Key(group.Value) : string.Empty;

    private static string Key(string folded) => folded.Length == 0 ? string.Empty : ErrorText.Tokenize(FoldedText.Of(folded)).Key;

    // MSTest 4.4.1 *FailedSummary, English then French as its fr resources write them.
    private static Dictionary<string, (string Kind, ErrorLanguage Language)> BuildSummaries()
    {
        (string Kind, string English, string French)[] summaries =
        [
            ("IsGreaterThan", "Expected value to be greater than the lower bound.", "La valeur attendue doit être supérieure à la limite inférieure."),
            ("AreNotEqual", "Expected values to differ.", "Les valeurs devraient différer."),
            ("IsNegative", "Expected value to be negative.", "La valeur attendue doit être négative."),
            ("DoesNotContainPredicate", "Expected collection to not contain an element matching the predicate.",
                "La collection attendue ne doit pas contenir d’élément correspondant au prédicat."),
            ("DoesNotEndWith", "Expected string to not end with the specified suffix.", "La chaîne attendue ne doit pas se terminer par le suffixe spécifié."),
            ("AreNotEqualDelta", "Expected values to differ beyond tolerance.", "Les valeurs attendues doivent différer au-delà de la tolérance."),
            ("ContainsAll", "Expected collection to contain all specified items.", "La collection attendue doit contenir tous les éléments spécifiés."),
            ("ContainsSubstring", "Expected string to contain the specified substring.", "La chaîne attendue doit contenir la sous-chaîne spécifiée."),
            ("AreNotEqualStringsCaseInsensitive", "Expected strings to differ (case-insensitive).", "Les chaînes attendues doivent différer (insensible à la casse)."),
            ("IsFalse", "Expected condition to be false.", "La condition attendue doit être false."),
            ("AreSequenceEqualInAnyOrder", "Expected sequences to be equal (in any order).", "Les séquences attendues doivent être égales (quel que soit l’ordre)."),
            ("AreNotEquivalent", "Expected values to be structurally different.", "Les valeurs attendues doivent être structurellement différentes."),
            ("MatchesRegex", "Expected string to match the specified pattern.", "La chaîne attendue doit correspondre au motif spécifié."),
            ("IsNotNull", "Expected value to not be null.", "La valeur attendue ne doit pas être null."),
            ("ContainsPredicate", "Expected collection to contain an element matching the predicate.",
                "La collection attendue ne contient pas d’élément correspondant au prédicat."),
            ("AreEqualDelta", "Expected values to be equal within tolerance.", "Les valeurs attendues doivent être égales dans la tolérance."),
            ("ContainsSingle", "Expected collection to contain exactly one element.", "La collection attendue doit contenir exactement un élément."),
            ("AreAllOfType", "Expected all items in collection to be of the specified type.", "Tous les éléments de la collection doivent être du type spécifié."),
            ("AreEqual", "Expected values to be equal.", "Les valeurs attendues doivent être égales."),
            ("AreNotEqualStrings", "Expected strings to differ.", "Les chaînes attendues doivent différer."),
            ("HasCount", "Expected collection to contain a specific number of elements.", "La collection attendue doit contenir un nombre spécifique d’éléments."),
            ("ContainsItem", "Expected collection to contain the specified element.", "Collection attendue pour contenir l’élément spécifié."),
            ("AreNotSequenceEqualInOrder", "Expected sequences to differ.", "Les séquences attendues doivent différer."),
            ("AreNotEquivalentComparison", "Could not complete structural comparison.", "Impossible d’effectuer la comparaison structurelle."),
            ("ContainsSingleMatch", "Expected collection to contain exactly one element matching the predicate.",
                "La collection attendue contient exactement un élément correspondant au prédicat."),
            ("IsEmpty", "Expected collection to be empty.", "La collection attendue doit être vide."),
            ("IsLessThanOrEqualTo", "Expected value to be less than or equal to the upper bound.", "La valeur attendue doit être inférieure ou égale à la limite supérieure."),
            ("IsPositive", "Expected value to be positive.", "La valeur attendue doit être positive."),
            ("AreEqualDifferentTypes", "Expected values to be equal, but they are of different types.",
                "Les valeurs attendues doivent être égales, mais elles sont de types différents."),
            ("AreSequenceEqualInOrder", "Expected sequences to be equal.", "Les séquences attendues doivent être égales."),
            ("DoesNotContainItem", "Expected collection to not contain the specified element.", "Collection attendue pour ne pas contenir l’élément spécifié."),
            ("DoesNotMatchRegex", "Expected string to not match the specified pattern.", "La chaîne ne devait pas correspondre au motif spécifié."),
            ("IsInRange", "Expected value to be within the inclusive range.", "La valeur attendue doit être comprise dans l’intervalle inclusif."),
            ("IsNull", "Expected value to be null.", "La valeur attendue doit être null."),
            ("DoesNotContainSubstring", "Expected string to not contain the specified substring.", "La chaîne attendue ne doit pas contenir la sous-chaîne spécifiée."),
            ("AreNotSequenceEqualInAnyOrder", "Expected sequences to differ (in any order).", "Les séquences attendues doivent différer (quel que soit l’ordre)."),
            ("AreAllDistinct", "Expected all items in collection to be distinct.", "Tous les éléments de la collection doivent être distincts."),
            ("IsTrue", "Expected condition to be true.", "Condition attendue pour être true."),
            ("IsNotEmpty", "Expected collection to not be empty.", "La collection attendue ne doit pas être vide."),
            ("DoesNotContainAll", "Expected collection to not contain all specified items.", "La collection attendue ne doit pas contenir tous les éléments spécifiés."),
            ("StartsWith", "Expected string to start with the specified prefix.", "La chaîne attendue doit commencer par le préfixe spécifié."),
            ("EndsWith", "Expected string to end with the specified suffix.", "Chaîne attendue se terminant par le suffixe spécifié."),
            ("AreAllNotNull", "Expected all items in collection to be non-null.", "Tous les éléments attendus de la collection doivent être non null."),
            ("AreEquivalent", "Expected values to be structurally equivalent.", "Les valeurs attendues doivent être structurellement équivalentes."),
            ("IsGreaterThanOrEqualTo", "Expected value to be greater than or equal to the lower bound.",
                "La valeur attendue doit être supérieure ou égale à la limite inférieure."),
            ("DoesNotStartWith", "Expected string to not start with the specified prefix.", "La chaîne attendue ne doit pas commencer par le préfixe spécifié."),
            ("IsLessThan", "Expected value to be less than the upper bound.", "La valeur attendue doit être inférieure à la limite supérieure."),
            ("AreEqualStrings", "Expected strings to be equal.", "Les chaînes attendues doivent être égales."),
        ];
        Dictionary<string, (string Kind, ErrorLanguage Language)> map = new(StringComparer.Ordinal);
        foreach ((string kind, string english, string french) in summaries)
        {
            map[FoldedText.Of(english).Text] = (kind, ErrorLanguage.English);
            map[FoldedText.Of(french).Text] = (kind, ErrorLanguage.French);
        }
        return map;
    }
}
