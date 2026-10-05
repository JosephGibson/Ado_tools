using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Localization;

public sealed class HardCodedStringTests
{
    // Exemptions apply only to reviewed machine text at a message call site.
    // Resource keys, URI paths, HTTP headers, and error codes outside message
    // arguments are machine identifiers, not human messages. No call-site
    // exemptions are currently needed.
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal);

    // A message call site: the method or the type named before its argument list, or a variable,
    // method or ErrorDetails property of a message type that a target-typed new constructs.
    private static readonly Regex[] CallSites =
    [
        new(@"\b(?:WriteWarning|WriteVerbose|WriteDebug|WriteInformation|ProgressRecord|ErrorDetails|Ado\w*Exception)\s*\(", RegexOptions.CultureInvariant),
        new(@"\b(?:ProgressRecord|ErrorDetails|Ado\w*Exception)\??\s+\w+\s*=\s*new\s*\(", RegexOptions.CultureInvariant),
        new(@"\b(?:ProgressRecord|ErrorDetails|Ado\w*Exception)\??\s+\w+\s*\([^)]*\)\s*=>\s*new\s*\(", RegexOptions.CultureInvariant),
        new(@"\bErrorDetails\s*=\s*new\s*\(", RegexOptions.CultureInvariant),
    ];

    [Fact]
    [Trait("Acceptance", "S0-7")]
    public void HumanMessageCallSitesDoNotContainStringLiterals()
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };
        foreach (string path in Directory.EnumerateFiles(Path.Combine(TestDirectory.RepositoryRoot, "src"), "*.cs", options))
        {
            string relative = Path.GetRelativePath(TestDirectory.RepositoryRoot, path).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "obj" or "bin")) continue;
            foreach (string site in LiteralCallSites(File.ReadAllText(path)))
                Assert.True(Exemptions.Contains(relative + ":" + site), relative + ": localize message literals: " + site);
        }
    }

    // The scan sees a message type however it is constructed, a target-typed new included.
    [Fact]
    public void EveryFormOfConstructionIsScanned()
    {
        string[] literal =
        [
            "throw new AdoNotFoundException(\"Work item not found.\");",
            "AdoNotFoundException error = new(\"Work item not found.\");",
            "AdoNotFoundException? error = new(\"Work item not found.\");",
            "ProgressRecord record = new(1, \"Reading\", \"Step\");",
            "private static AdoResponseFormatException TooLarge(CultureInfo culture) => new(\"Too large.\");",
            "WriteError(record) { ErrorDetails = new(\"Details.\") };",
            "WriteWarning(\"Careful.\");",
        ];
        foreach (string source in literal) Assert.Single(LiteralCallSites(source));
        Assert.Empty(LiteralCallSites("AdoNotFoundException error = new(Messages.Get(AdoMessage.NotFound, culture));"));
        Assert.Empty(LiteralCallSites("throw new AdoNotFoundException(Messages.Get(AdoMessage.NotFound, culture));"));
    }

    // The call sites whose argument list holds a string literal.
    private static IEnumerable<string> LiteralCallSites(string source)
    {
        foreach (Regex call in CallSites)
            foreach (Match match in call.Matches(source))
            {
                int depth = 1;
                for (int index = match.Index + match.Length; index < source.Length && depth > 0; index++)
                {
                    char character = source[index];
                    if (character == '"') { yield return match.Value; break; }
                    if (character == '(') depth++;
                    if (character == ')') depth--;
                }
            }
    }
}
