using System.Text.Json;

namespace AdoToolkit.Core.Reporting;

public static class ReportOutputValidator
{
    // Counts come from the contents entries so validation never rebuilds case models.
    public static void ValidateHtml(string path, ReportDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ValidateNonEmpty(path, model.Culture);
        IReadOnlyList<ReportContentsEntry> expected = model.Contents;
        int generator = 0, documents = 0, cases = 0, lists = 0, cards = 0, ended = 0;
        foreach (string line in LinePrefixes(path))
        {
            if (line == "<meta name=\"generator\" content=\"AdoToolkit " + SinkEncoding.Attribute(model.ToolkitVersion) + "\">") generator++;
            if (line == "<html lang=\"" + SinkEncoding.Attribute(model.Culture.Name) + "\" data-case-count=\"" + model.Cases.Count.ToString(CultureInfo.InvariantCulture) + "\">") documents++;
            if (line.StartsWith("<article class=\"test-case\"", StringComparison.Ordinal))
            {
                cases++;
                cards = 0;
                string opening = model.IsMultiCase && cases <= expected.Count ? "<article class=\"test-case\" id=\"" + expected[cases - 1].Anchor + "\">" : "<article class=\"test-case\">";
                if (line != opening) Invalid(model);
            }
            if (line.StartsWith("<section class=\"steps\"", StringComparison.Ordinal))
            {
                if (cases == 0 || cases > expected.Count || line != "<section class=\"steps\" data-step-count=\"" + expected[cases - 1].RowCount.ToString(CultureInfo.InvariantCulture) + "\">") Invalid(model);
                lists++;
            }
            if (line.StartsWith("<div class=\"step-card", StringComparison.Ordinal)) cards++;
            if (line == "</article>")
            {
                if (cases == 0 || cases > expected.Count || cards != expected[cases - 1].RowCount) Invalid(model);
                ended++;
            }
        }
        if (generator != 1 || documents != 1 || cases != model.Cases.Count || cases != expected.Count || lists != cases || ended != cases) Invalid(model);
    }

    public static void ValidateMarkdown(string path, ReportDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ValidateNonEmpty(path, model.Culture);
        IReadOnlyList<ReportContentsEntry> expected = model.Contents;
        // A multi-case document starts with one cover heading, and no step headings precede the first case.
        int headings = 0, cases = 0, rows = 0;
        int offset = model.IsMultiCase ? 1 : 0;
        foreach (string line in LinePrefixes(path))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                if (cases > 0 && rows != expected[cases - 1].RowCount) Invalid(model);
                if (headings++ < offset) continue;
                cases++;
                if (cases > expected.Count) Invalid(model);
                rows = 0;
            }
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                if (cases == 0) Invalid(model);
                rows++;
            }
        }
        if (cases != model.Cases.Count || cases != expected.Count || (cases > 0 && rows != expected[cases - 1].RowCount)) Invalid(model);
    }

    public static void ValidateJson(string path, ReportDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ValidateJson(path, model.Culture, model.Cases.Count);
    }

    // Only renderer-owned prefixes are needed. Memory stays bounded even for a multi-MiB content line.
    private static IEnumerable<string> LinePrefixes(string path)
    {
        using StreamReader reader = new(path, new System.Text.UTF8Encoding(false, true));
        System.Text.StringBuilder prefix = new(512);
        int character;
        while ((character = reader.Read()) >= 0)
        {
            if (character == '\n') { yield return prefix.ToString(); prefix.Clear(); }
            else if (prefix.Length < 512) prefix.Append((char)character);
        }
        if (prefix.Length > 0) yield return prefix.ToString();
    }

    private static void Invalid(ReportDocumentModel model) => throw new InvalidDataException(Messages.Get(AdoMessage.InvalidReportOutput, model.Culture));

    public static void ValidateNonEmpty(string path, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(culture);
        FileInfo file = new(path);
        if (!file.Exists || file.Length == 0)
            throw new InvalidDataException(Messages.Get(AdoMessage.InvalidReportOutput, culture));
    }

    public static void ValidateJson(string path, CultureInfo culture) => ValidateJson(path, culture, null);

    private static void ValidateJson(string path, CultureInfo culture, int? caseCount)
    {
        ValidateNonEmpty(path, culture);
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count(property => property.NameEquals("schemaVersion")) != 1 ||
                !root.TryGetProperty("schemaVersion", out JsonElement version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) ||
                number != ReportDocumentModel.SchemaVersion ||
                (caseCount.HasValue && (!root.TryGetProperty("cases", out JsonElement cases) || cases.ValueKind != JsonValueKind.Array
                    || cases.GetArrayLength() != caseCount.Value)))
                throw new InvalidDataException(Messages.Get(AdoMessage.InvalidReportOutput, culture));
        }
        catch (JsonException error)
        {
            throw new InvalidDataException(Messages.Get(AdoMessage.InvalidReportOutput, culture), error);
        }
    }
}
