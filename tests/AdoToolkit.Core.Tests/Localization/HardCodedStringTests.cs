using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Localization;

public sealed class HardCodedStringTests
{
    // Exemptions apply only to reviewed machine text at a message call site.
    // Resource keys, URI paths, HTTP headers, and error codes outside message
    // arguments are machine identifiers, not human messages. No call-site
    // exemptions are currently needed.
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal);

    [Fact]
    [Trait("Acceptance", "S0-7")]
    public void HumanMessageCallSitesDoNotContainStringLiterals()
    {
        Regex call = new(@"\b(?:WriteWarning|WriteVerbose|WriteDebug|WriteInformation|ProgressRecord|ErrorDetails|Ado\w*Exception)\s*\(", RegexOptions.CultureInvariant);
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
            string source = File.ReadAllText(path);
            foreach (Match match in call.Matches(source))
            {
                int depth = 1;
                for (int index = match.Index + match.Length; index < source.Length && depth > 0; index++)
                {
                    char character = source[index];
                    Assert.True(character != '"' || Exemptions.Contains(relative + ":" + match.Value),
                        relative + ": localize message literals: " + match.Value);
                    if (character == '(') depth++;
                    if (character == ')') depth--;
                }
            }
        }
    }
}
