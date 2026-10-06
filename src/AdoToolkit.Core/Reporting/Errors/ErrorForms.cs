namespace AdoToolkit.Core.Reporting.Errors;

// Reads the form of one error message. Past MSTest's wrapper line, the catalog is tried, then the
// default messages of the runtime, then the tokenized key line. The rules come last and win: a line
// they match joins the rule's error, and keeps the parts the reading found, so that it shows as the
// server wrote it.
internal static class ErrorForms
{
    // The messages that the runtime gives these exceptions by default, which say nothing the type does
    // not: CoreLib of .NET 10 and mscorlib of .NET Framework 4.8 write them alike in English. A custom
    // message is not among them and stays part of the form.
    private static readonly Dictionary<string, string> DefaultMessages = new(StringComparer.Ordinal)
    {
        ["nullreferenceexception"] = "object reference not set to an instance of an object.",
        ["indexoutofrangeexception"] = "index was outside the bounds of the array.",
        ["dividebyzeroexception"] = "attempted to divide by zero.",
        ["operationcanceledexception"] = "the operation was canceled.",
        ["taskcanceledexception"] = "a task was canceled.",
    };

    // The form of the message's lines, and the language that its framework text shows: the
    // template's, else that of MSTest's wrapper line. frame is the test's own first frame, which
    // tells apart forms that hold no identifying text.
    internal static (ErrorSignature Form, ErrorLanguage Evidence) Read(IReadOnlyList<string> scanned, string? frame, IReadOnlyList<ErrorRule> rules)
    {
        ArgumentNullException.ThrowIfNull(scanned);
        ArgumentNullException.ThrowIfNull(rules);
        if (scanned.Count == 0) throw new ArgumentException(null, nameof(scanned));
        ErrorLines lines = new(scanned);
        ErrorLanguage? wrapper = ErrorTemplates.Wrapper(lines);
        int start = wrapper is null ? lines.KeyIndex : 1;
        (string? prefixType, int prefixLength) = ErrorText.Prefix(lines.Lines[start]);
        ErrorSignature form = Template(lines, start, frame) ?? Default(lines, start, prefixType, prefixLength, frame) ?? Text(lines, start);
        // The type that a heading shows apart must open the line the form shows.
        if (prefixType is not null && !form.Line.StartsWith(lines.Lines[start][..prefixLength], StringComparison.Ordinal)) (prefixType, prefixLength) = (null, 0);
        form = new ErrorSignature
        {
            Key = form.Key, FramelessKey = form.FramelessKey, Layout = form.Layout, Parts = form.Parts, TemplateId = form.TemplateId,
            Language = form.Language, IsLocated = form.IsLocated, PrefixType = prefixType, PrefixLength = prefixLength,
        };
        ErrorLanguage evidence = form.Language != ErrorLanguage.Neutral ? form.Language : wrapper ?? ErrorLanguage.Neutral;
        IReadOnlyList<string> candidates = Candidates(lines, start);
        for (int index = 0; index < rules.Count; index++)
        {
            if (!rules[index].Matches(candidates)) continue;
            // A rule's matches are one form, whatever they say; configured rules are told apart by
            // their place in the list, built-in rules by their ID.
            string key = "R|" + (rules[index].BuiltInId is { } id ? "b:" + id : "c:" + index.ToString(CultureInfo.InvariantCulture));
            return (new ErrorSignature
            {
                Key = key, FramelessKey = key, Layout = form.Layout, Parts = form.Parts, TemplateId = form.TemplateId, Language = form.Language,
                Rule = rules[index], PrefixType = prefixType, PrefixLength = prefixLength,
            }, evidence);
        }
        return (form, evidence);
    }

    private static ErrorSignature? Template(ErrorLines lines, int start, string? frame)
    {
        if (ErrorTemplates.Match(lines, start) is not { } match) return null;
        string key = "T|" + match.Id + "|" + string.Join('\u001f', match.Identity);
        bool located = match.IsLocated && frame is not null;
        return new ErrorSignature
        {
            Key = located ? key + "|@" + frame : key, FramelessKey = key, Layout = "T|" + match.Id, Parts = match.Parts, TemplateId = match.Id,
            Language = match.Language, IsLocated = located,
        };
    }

    private static ErrorSignature? Default(ErrorLines lines, int start, string? prefixType, int prefixLength, string? frame)
    {
        if (prefixType is null) return null;
        string type = FoldedText.Of(prefixType).Text;
        int dot = type.LastIndexOf('.');
        if (!DefaultMessages.TryGetValue(dot >= 0 ? type[(dot + 1)..] : type, out string? message)) return null;
        if (!string.Equals(FoldedText.Of(lines.Lines[start][prefixLength..]).Text, message, StringComparison.Ordinal)) return null;
        (string layout, IReadOnlyList<ErrorPart> parts) = ErrorText.Tokenize(lines.Folded(start));
        string key = "D|" + type;
        return new ErrorSignature
        {
            Key = frame is null ? key : key + "|@" + frame, FramelessKey = key, Layout = "X|" + layout, Parts = parts, IsLocated = frame is not null,
        };
    }

    private static ErrorSignature Text(ErrorLines lines, int start)
    {
        (string key, IReadOnlyList<ErrorPart> parts) = ErrorText.Tokenize(lines.Folded(start));
        return new ErrorSignature { Key = "X|" + key, FramelessKey = "X|" + key, Layout = "X|" + key, Parts = parts };
    }

    // The folded lines a rule is tried on: the key line, the first line and each inner exception
    // (a line that starts with "--->"), each also without its leading "Type:".
    private static List<string> Candidates(ErrorLines lines, int start)
    {
        List<string> candidates = [];
        void Add(string line)
        {
            string folded = FoldedText.Of(line).Text;
            if (!candidates.Contains(folded, StringComparer.Ordinal)) candidates.Add(folded);
            (string? type, int length) = ErrorText.Prefix(line);
            if (type is not null) Add(line[length..]);
        }
        Add(lines.Lines[start]);
        Add(lines.Lines[0]);
        foreach (string line in lines.Lines)
            if (line.StartsWith("--->", StringComparison.Ordinal)) Add(line[4..].TrimStart());
        return candidates;
    }
}
