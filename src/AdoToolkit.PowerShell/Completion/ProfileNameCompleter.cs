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
            WildcardPattern pattern = new(WildcardPattern.Escape(wordToComplete.Trim('\'')) + "*", WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
            return new ConfigurationStore().Load(CultureInfo.InvariantCulture).Profiles.Keys.Where(pattern.IsMatch)
                .Order(StringComparer.OrdinalIgnoreCase).Select(name => new CompletionResult("'" + CodeGeneration.EscapeSingleQuotedStringContent(name) + "'", name, CompletionResultType.ParameterValue, name)).ToArray();
        }
        catch (AdoException) { return []; }
    }
}
