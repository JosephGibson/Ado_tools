using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

public static partial class TestFailureReportValidator
{
    public static void Validate(string path, TestFailureReportModel model) => Validate(path, model, null);

    // attachmentFolder is where the linked files are before the folder rename (§13.4 step 5).
    public static void Validate(string path, TestFailureReportModel model, string? attachmentFolder)
    {
        ArgumentNullException.ThrowIfNull(model);
        ReportOutputValidator.ValidateNonEmpty(path, model.Culture);
        using StreamReader reader = new(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        Validate(reader, model, attachmentFolder);
    }

    public static void Validate(TextReader reader, TestFailureReportModel model) => Validate(reader, model, null);

    // Tokenize only toolkit-authored markup. Remote text has already been encoded; no DOM or
    // parser package is needed. Memory is bounded by one tag/raw-text asset, not the report.
    public static void Validate(TextReader reader, TestFailureReportModel model, string? attachmentFolder)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(model);
        LocalLinks links = new(model, attachmentFolder);
        int roots = 0, generators = 0, policies = 0, cards = 0, attempts = 0, closedCards = 0, ended = 0;
        IReadOnlyList<AdoTestAttempt> order = [];
        string? policy = null;
        bool inCard = false, assetSeen = false;
        List<string> scripts = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach ((string name, Dictionary<string, string> attributes, string? body) in Tags(reader, model))
        {
            string? Attribute(string key) => attributes.GetValueOrDefault(key);
            if (attributes.Keys.Any(key => key.StartsWith("on", StringComparison.OrdinalIgnoreCase))) Invalid(model);
            if (Attribute("id") is { } id && !ids.Add(id)) Invalid(model);
            bool local = attributes.ContainsKey("data-local-file");
            if (Attribute("href") is { } href) links.Check(href, local, image: false);
            else if (Attribute("src") is { } source) links.Check(source, local, image: name == "img");
            else if (local) Invalid(model);
            switch (name)
            {
                case "html":
                    roots++;
                    if (Attribute("lang") != model.Culture.Name || Attribute("data-failure-count") != N(model.Failures.Count) ||
                        Attribute("data-history-count") != N(model.History.Count)) Invalid(model);
                    break;
                case "meta":
                    if (Attribute("name") == "generator")
                    {
                        generators++;
                        if (Attribute("content") != "AdoToolkit " + model.ToolkitVersion) Invalid(model);
                    }
                    if (string.Equals(Attribute("http-equiv"), "Content-Security-Policy", StringComparison.OrdinalIgnoreCase))
                    {
                        policies++;
                        if (assetSeen) Invalid(model);
                        policy = Attribute("content");
                    }
                    break;
                case "style":
                case "script":
                    assetSeen = true;
                    if (policies != 1) Invalid(model);
                    if (name == "script")
                    {
                        // The renderer emits only a bare script tag: this also rejects src,
                        // data scripts, nonces and malformed or duplicate attributes.
                        if (attributes.Count != 0 || body is null) Invalid(model);
                        scripts.Add(body!);
                    }
                    break;
                case "article":
                    if (inCard || cards >= model.Failures.Count) Invalid(model);
                    var failure = model.Failures[cards++];
                    if (Attribute("id") != "f-" + N(failure.Ordinal) || Attribute("data-attempt-count") != N(failure.Attempts.Count)) Invalid(model);
                    inCard = true;
                    attempts = 0;
                    // Attempts appear once each, grouped by pipeline group when the build has groups.
                    order = [.. TestFailureGroups.Of(failure, model.Grouping).SelectMany(group => group.Attempts)];
                    break;
                case "details" when attributes.ContainsKey("open"):
                    // Every attempt, group and preview starts collapsed.
                    Invalid(model);
                    break;
                case "details" when Attribute("class") == "attempt":
                    if (!inCard || attempts >= order.Count) Invalid(model);
                    if (Attribute("id") != "f-" + N(model.Failures[cards - 1].Ordinal) + "-a" + N(order[attempts++].Number)) Invalid(model);
                    break;
                case "/article":
                    if (!inCard || attempts != model.Failures[cards - 1].Attempts.Count) Invalid(model);
                    inCard = false;
                    closedCards++;
                    break;
                case "/html": ended++; break;
            }
        }
        if (roots != 1 || generators != 1 || policies != 1 || ended != 1 || inCard || cards != model.Failures.Count || closedCards != cards ||
            scripts.Count == 0 || scripts.Distinct(StringComparer.Ordinal).Count() != scripts.Count || policy != ContentSecurityPolicy.Create(scripts)) Invalid(model);
        links.EnsureAllLinked();
    }

    // Links are fragments, absolute http(s) URLs, or toolkit-marked local files. A local link is
    // exactly "<escaped final folder>/<escaped toolkit name>" and names a non-empty file of the
    // recorded size in the attachment folder; every recorded file is linked at least once.
    private sealed class LocalLinks(TestFailureReportModel model, string? folder)
    {
        private readonly HashSet<string> linked = new(StringComparer.Ordinal);

        internal void Check(string value, bool marked, bool image)
        {
            bool external = value.StartsWith('#')
                || (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                || (image && value.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
            if (external)
            {
                if (marked) Invalid(model);
                return;
            }
            if (!marked || folder is null || model.LocalAttachments is not { } local) { Invalid(model); return; }
            string[] parts = value.Split('/');
            if (parts.Length != 2 || parts[0] != Uri.EscapeDataString(local.FolderName)) Invalid(model);
            string name = Uri.UnescapeDataString(parts[1]);
            long length = 0;
            // Only JSON, text and mismatched .bin files are ever downloaded; none is an image.
            if (parts[1] != Uri.EscapeDataString(name) || !ToolkitName().IsMatch(name) || image
                || !local.Files.TryGetValue(name, out length)) Invalid(model);
            if (linked.Contains(name)) return;
            FileInfo file = new(Path.Combine(folder, name));
            if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint) || length < 1 || file.Length != length) Invalid(model);
            linked.Add(name);
        }

        internal void EnsureAllLinked()
        {
            if (model.LocalAttachments is { } local && local.Files.Keys.Any(name => !linked.Contains(name))) Invalid(model);
        }
    }

    private static IEnumerable<(string Name, Dictionary<string, string> Attributes, string? Body)> Tags(TextReader reader, TestFailureReportModel model)
    {
        int value;
        while ((value = reader.Read()) >= 0)
        {
            if (value != '<') continue;
            StringBuilder tag = new();
            char quote = '\0';
            while ((value = reader.Read()) >= 0)
            {
                char character = (char)value;
                if (quote == '\0' && character == '>') break;
                tag.Append(character);
                if (quote == character) quote = '\0';
                else if (quote == '\0' && character is '\'' or '"') quote = character;
            }
            if (value < 0) Invalid(model);
            string text = tag.ToString();
            Match match = TagName().Match(text);
            if (!match.Success) Invalid(model);
            string name = match.Value.ToLowerInvariant();
            Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);
            if (!name.StartsWith('!') && !name.StartsWith('/'))
            {
                int consumed = match.Length;
                foreach (Match attribute in AttributePattern().Matches(text, consumed))
                {
                    if (!string.IsNullOrWhiteSpace(text[consumed..attribute.Index])) Invalid(model);
                    string key = attribute.Groups[1].Value;
                    string content = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Success ? attribute.Groups[3].Value : attribute.Groups[4].Value;
                    if (!attributes.TryAdd(key, WebUtility.HtmlDecode(content))) Invalid(model);
                    consumed = attribute.Index + attribute.Length;
                }
                if (text[consumed..].Trim() is not ("" or "/")) Invalid(model);
            }
            string? body = name is "script" or "style" ? RawText(reader, name, model) : null;
            yield return (name, attributes, body);
        }
    }

    private static string RawText(TextReader reader, string name, TestFailureReportModel model)
    {
        string closing = "</" + name + ">";
        StringBuilder body = new();
        int character;
        while ((character = reader.Read()) >= 0)
        {
            body.Append((char)character);
            if (character != '>' || body.Length < closing.Length) continue;
            int start = body.Length - closing.Length;
            if (string.Equals(body.ToString(start, closing.Length), closing, StringComparison.OrdinalIgnoreCase))
                return body.ToString(0, start);
        }
        Invalid(model);
        return string.Empty;
    }

    private static string N(int number) => number.ToString(CultureInfo.InvariantCulture);
    private static void Invalid(TestFailureReportModel model) => throw new InvalidDataException(Messages.Get(AdoMessage.InvalidReportOutput, model.Culture));

    [GeneratedRegex(@"^r[1-9][0-9]*-[1-9][0-9]*(?:-s[1-9][0-9]*)?-a[1-9][0-9]*\.(?:json|txt|bin)$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolkitName();

    [GeneratedRegex(@"^/?[a-zA-Z!][a-zA-Z0-9!:-]*", RegexOptions.CultureInvariant)]
    private static partial Regex TagName();

    [GeneratedRegex("([^\\s=/>]+)(?:\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+)))?", RegexOptions.CultureInvariant)]
    private static partial Regex AttributePattern();
}
