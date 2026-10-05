using System.Collections;
using System.Management.Automation.Language;

namespace AdoToolkit.Completion;

public sealed class ProfileNameCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters)
    {
        ArgumentNullException.ThrowIfNull(wordToComplete);
        try
        {
            WildcardPattern pattern = NameCompletion.PrefixPattern(wordToComplete);
            // Get-AdoProfile -Name is a pattern: a name such as 'Lab [1]' is escaped so that it matches
            // itself only. Connect-Ado, Set-AdoProfile and Remove-AdoProfile take the name literally.
            bool isPattern = string.Equals(commandName, "Get-AdoProfile", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parameterName, "Name", StringComparison.OrdinalIgnoreCase);
            return new ConfigurationStore().Load(CultureInfo.InvariantCulture).Profiles.Keys.Where(pattern.IsMatch)
                .Order(StringComparer.OrdinalIgnoreCase).Select(name => new CompletionResult(
                    "'" + CodeGeneration.EscapeSingleQuotedStringContent(isPattern ? WildcardPattern.Escape(name) : name) + "'",
                    name, CompletionResultType.ParameterValue, name)).ToArray();
        }
        catch (AdoException) { return []; }
    }
}
