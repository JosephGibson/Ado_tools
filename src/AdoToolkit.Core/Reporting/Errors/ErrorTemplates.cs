using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting.Errors;

// The catalog: the error texts of the test frameworks and of Selenium that the report recognizes, in
// English and in French where the framework writes both. Each form was read from a package, never
// written from memory: MSTest.TestFramework and MSTest.TestAdapter 2.2.10, 3.11.1 and 4.4.1 (their
// fr resources, and messages captured under en-US and fr-FR), NUnit 3.14.0 and 4.6.1, xunit.assert
// 2.9.3, xunit.v3.assert 4.0.1, Selenium.WebDriver and Selenium.Support 4.50.0, and Microsoft.Playwright
// 1.41.2 and 1.63.0 (the string literals of its assembly and its driver, and the client source at
// those tags). NUnit, xUnit, Selenium and Playwright write English only. Patterns run on folded lines
// (FoldedText): lower case, no accents, one space for a run of spaces.
internal static class ErrorTemplates
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    // Lines after the head that a multi-line template reads.
    private const int MaximumFollowingLines = 10;
    // Slots by role, the same in every form of a template so that their values line up.
    private const int ExpectedSlot = 0, ActualSlot = 1, MessageSlot = 2, ExtraSlot = 3, SecondExtraSlot = 4, ExpressionSlot = 5, TargetSlot = 6;
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
        return Adapter(lines, start) ?? Classic(lines, start) ?? Modern(lines, start) ?? Xunit(lines, start) ?? PlaywrightExpect(lines, start) ?? NUnit(lines, start)
            ?? Wait(lines, start) ?? PlaywrightTimeout(lines, start);
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

    // Playwright 1.41.2 and 1.63.0 put the call log after the message (Connection.FormatCallLog):
    // "\nCall log:\n  - " and the entries joined by "\n  - " (1.41.2), or "\nCall log:\n" and the
    // entries the driver compressed, each "  - text" or, for a repeated run, "  9 × text" (1.63.0,
    // compressCallLog). Trimmed, an entry reads "- text" or "9 × text". The entry that names what a
    // call waited for is "waiting for {locator}", with " to be {state}" for a wait for a selector, or
    // "navigating to \"{url}\", waiting until \"{state}\"" (the driver's frames).
    private static readonly Regex PlaywrightCallLog = new(@"^call log:$", Options);
    private static readonly Regex PlaywrightEntry = new(@"^(?:- |[0-9]+ × )(?<v>waiting for|navigating to) (?<t>.*)$", Options);

    // An action that runs out of time: System.TimeoutException with the driver's "Timeout {0}ms
    // exceeded." (progress, both versions) and the call log (Connection.ParseException), or the
    // client's "Timeout {0}ms exceeded while waiting for event \"{1}\"" (Waiter). Its first line says
    // nothing of what it waited for; the call log does.
    private static readonly Regex PlaywrightTimeoutHead = new(
        @"^(?:[a-z_][a-z0-9_.+`]*(?:exception|error) ?: )?timeout (?<n>[0-9]+)ms exceeded(?:\.| while waiting for event ""(?<x>.*)"")$", Options);

    private static TemplateMatch? PlaywrightTimeout(ErrorLines lines, int start)
    {
        FoldedText line = lines.Folded(start);
        Match head = PlaywrightTimeoutHead.Match(line.Text);
        if (!head.Success) return null;
        List<ErrorPart> parts = [];
        AddParts(parts, line, head, [("n", ExtraSlot)]);
        string target = string.Empty;
        if (PlaywrightTarget(lines, start, true) is ({ } entryLine, { } entry))
        {
            AddParts(parts, entryLine, entry, [("t", TargetSlot)], entry.Groups["v"].Index);
            target = Key(entry.Groups["v"].Value + " " + entry.Groups["t"].Value);
        }
        string waited = Key(head.Groups["x"]);
        return new TemplateMatch
        {
            Id = "Playwright.Timeout", Language = ErrorLanguage.Neutral, Parts = parts, Identity = [waited, target],
            IsLocated = waited.Length == 0 && target.Length == 0,
        };
    }

    // An assertion (AssertionsBase.ExpectImplAsync) throws PlaywrightException: the test's own message
    // and an empty line first when it passed one (1.63.0); the assertion's message, which starts
    // "Locator expected", "Page expected", "Page title expected" or "Page URL expected", "expected not
    // to" when negated; in 1.63.0, a line with the driver's message, "Error: " first (Frame.ExpectAsync);
    // then " '{expected}'\nBut was: '{actual}' " when there is an expected value, else " "; then the
    // call log, whose first entry is the assertion with its timeout, Expect "ToBeVisibleAsync" with
    // timeout 5000ms (1.63.0) or LocatorAssertions.ToBeVisibleAsync with timeout 5000ms (1.41.2), and
    // whose next one names the locator. With the driver's message, the expected value ends that line.
    // A name in quotes, as in "to have attribute 'name'", belongs to the assertion; a value may span
    // lines, so its closing quote can be missing.
    private static readonly Regex PlaywrightExpectHead = new(
        @"^(?:[a-z_][a-z0-9_.+`]*(?:exception|error) ?: )?(?<k>(?:locator|page|page title|page url) expected [^']*?(?:(?:attribute|property) '[^']*')?)(?: '(?<e>.*?)'?)?$",
        Options);
    private static readonly Regex PlaywrightErrorLine = new(@"^error: (?<d>.*?)(?: '(?<e>.*)')?$", Options);
    private static readonly Regex PlaywrightActual = new(@"^but was: '(?<a>.*?)'?$", Options);
    private const string PlaywrightException = "Microsoft.Playwright.PlaywrightException";

    private static TemplateMatch? PlaywrightExpect(ErrorLines lines, int start)
    {
        // Only a PlaywrightException, which every test framework prints with its type: a test
        // author's message that starts like an assertion is the framework's. The head opens the
        // line where the form starts, or, after the test's own message of however many lines, the
        // last line before the call log that reads as one.
        if (!string.Equals(ErrorText.Prefix(lines.Lines[start]).Type, PlaywrightException, StringComparison.Ordinal)) return null;
        int head = PlaywrightExpectHead.IsMatch(lines.Folded(start).Text) ? start : -1;
        for (int index = start + 1; index < lines.Count && index <= start + MaximumFollowingLines; index++)
        {
            string text = lines.Folded(index).Text;
            if (PlaywrightCallLog.IsMatch(text)) break;
            if (PlaywrightExpectHead.IsMatch(text)) head = index;
        }
        if (head < 0) return null;
        FoldedText headLine = lines.Folded(head);
        Match match = PlaywrightExpectHead.Match(headLine.Text);
        List<ErrorPart> parts = [];
        List<string> message = [];
        for (int index = start; index < head; index++)
        {
            if (parts.Count > 0) parts.Add(new ErrorPart(" · ", -1));
            parts.Add(new ErrorPart(lines.Lines[index], -1));
            message.Add(Key(lines.Folded(index).Text));
        }
        AddParts(parts, headLine, match, [("e", ExpectedSlot)]);
        string kind = match.Groups["k"].Value;
        Group expected = match.Groups["e"];

        (FoldedText Line, Match Match)? error = null, actual = null, target = null;
        bool log = false;
        for (int index = head + 1; index < lines.Count && index <= head + MaximumFollowingLines; index++)
        {
            FoldedText line = lines.Folded(index);
            if (log)
            {
                if (PlaywrightTarget(line, false) is { } entry) { target = (line, entry); break; }
                continue;
            }
            if (PlaywrightCallLog.IsMatch(line.Text)) log = true;
            else if (error is null && actual is null && PlaywrightErrorLine.Match(line.Text) is { Success: true } errorMatch) error = (line, errorMatch);
            else if (actual is null && PlaywrightActual.Match(line.Text) is { Success: true } actualMatch) actual = (line, actualMatch);
        }
        // Without an expected value in the head, the error line ends with it: it shows after the head.
        bool moved = !expected.Success && error is (_, { } carried) && carried.Groups["e"] is { Success: true, Length: > 0 };
        if (moved && error is ({ } errorLine, { } found))
        {
            expected = found.Groups["e"];
            // Contiguous cuts: a combining mark at the end of the value, which folding drops, stays in it.
            int open = expected.Index - 2, close = expected.Index + expected.Length;
            parts.Add(new ErrorPart(errorLine.Original[errorLine.OriginalStart(open)..errorLine.OriginalStart(expected.Index)], -1));
            parts.Add(new ErrorPart(errorLine.Original[errorLine.OriginalStart(expected.Index)..errorLine.OriginalStart(close)], ExpectedSlot));
            parts.Add(new ErrorPart(errorLine.Original[errorLine.OriginalStart(close)..errorLine.OriginalEnd(close + 1)], -1));
        }
        string targetKey = string.Empty;
        if (target is ({ } targetLine, { } targetMatch))
        {
            AddParts(parts, targetLine, targetMatch, [("t", TargetSlot)], targetMatch.Groups["v"].Index);
            targetKey = Key(targetMatch.Groups["t"]);
        }
        if (actual is ({ } actualLine, { } actualFound)) AddParts(parts, actualLine, actualFound, [("a", ActualSlot)]);
        if (error is ({ } shownLine, { } shown)) AddParts(parts, shownLine, shown, [("d", ExtraSlot)], 0, moved ? shown.Groups["e"].Index - 2 : -1);
        string expectedKey = !expected.Success || expected.Length == 0 ? string.Empty
            : kind is "page url expected to be" or "page url expected not to be" ? PathKey(expected.Value) : Key(expected.Value);
        return new TemplateMatch
        {
            Id = "Playwright.Expect", Language = ErrorLanguage.Neutral, Parts = parts,
            Identity = [Key(kind), expectedKey, string.Join('\n', message), targetKey],
            // An assertion that names no value, no locator and no message of the test's own is told
            // apart by the test's frame, as a condition is.
            IsLocated = expectedKey.Length == 0 && message.Count == 0 && targetKey.Length == 0,
        };
    }

    // The call log's first entry that names what the call waited for, within reach after the head.
    private static (FoldedText? Line, Match? Match) PlaywrightTarget(ErrorLines lines, int head, bool navigation)
    {
        bool log = false;
        for (int index = head + 1; index < lines.Count && index <= head + MaximumFollowingLines; index++)
        {
            FoldedText line = lines.Folded(index);
            if (!log) log = PlaywrightCallLog.IsMatch(line.Text);
            else if (PlaywrightTarget(line, navigation) is { } entry) return (line, entry);
        }
        return (null, null);
    }

    private static Match? PlaywrightTarget(FoldedText line, bool navigation)
    {
        Match entry = PlaywrightEntry.Match(line.Text);
        if (!entry.Success || (!navigation && entry.Groups["v"].Value != "waiting for")) return null;
        // Before it acts, 1.63.0 waits for a pending navigation to finish: that entry names no element.
        return entry.Groups["t"].Value.EndsWith("navigation to finish...", StringComparison.Ordinal) ? null : entry;
    }

    // A URL that an assertion expects, keyed by its path, without scheme, host, query or fragment: the
    // host changes between environments, the path says which page. A segment is a slot only when the
    // whole of it is a number, a GUID or a hexadecimal ID as ErrorText reads them, so /orders/1234 and
    // /orders/1235 are one page, and /oauth2/callback and /saml2/callback two.
    private static readonly Regex UrlAuthority = new(@"^[a-z][a-z0-9+.-]*://[^/?#]*", Options);

    private static string PathKey(string folded)
    {
        Match authority = UrlAuthority.Match(folded);
        string path = authority.Success ? folded[authority.Length..] : folded;
        int end = path.IndexOfAny(['?', '#']);
        if (end >= 0) path = path[..end];
        path = path.TrimEnd('/');
        IEnumerable<string> segments = path.Split('/').Select(static segment =>
        {
            (string key, IReadOnlyList<ErrorPart> parts) = ErrorText.Tokenize(FoldedText.Of(segment));
            return parts is [{ Slot: >= 0 }] ? key : segment;
        });
        return "url:" + string.Join('/', segments);
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
    // text around them is literal, and every part is cut from the line as sent. from and to bound the
    // folded text shown; to is -1 for the end of the line.
    private static void AddParts(List<ErrorPart> parts, FoldedText line, Match match, (string Group, int Slot)[]? holes = null, int from = 0, int to = -1)
    {
        if (parts.Count > 0) parts.Add(new ErrorPart(" · ", -1));
        int position = from == 0 ? 0 : line.OriginalStart(from), limit = to < 0 ? line.Original.Length : line.OriginalEnd(to);
        List<(int Start, int End, int Slot)> found = [];
        foreach ((string group, int slot) in holes ?? Holes)
        {
            Group hole = match.Groups[group];
            if (!hole.Success || hole.Length == 0) continue;
            found.Add((line.OriginalStart(hole.Index), line.OriginalEnd(hole.Index + hole.Length), slot));
        }
        found.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        foreach ((int start, int end, int slot) in found)
        {
            if (start < position || end > limit) continue;
            if (start > position) parts.Add(new ErrorPart(line.Original[position..start], -1));
            parts.Add(new ErrorPart(line.Original[start..end], slot));
            position = end;
        }
        if (position < limit) parts.Add(new ErrorPart(line.Original[position..limit], -1));
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
