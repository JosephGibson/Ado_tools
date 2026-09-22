using System.Collections;
using System.Management.Automation.Language;

namespace AdoToolkit.Completion;

public sealed class ProjectNameCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters)
    {
        ArgumentNullException.ThrowIfNull(wordToComplete);
        SessionStateHolder holder = SessionStateRegistry.Current;
        if (holder.Connection is not AdoConnection connection ||
            !holder.Projects.TryGet(SessionStateHolder.Key(connection) + ":completion", out IReadOnlyList<AdoProject>? projects)) return [];
        WildcardPattern pattern = new(WildcardPattern.Escape(wordToComplete.Trim('\'')) + "*", WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
        return projects.Select(project => project.Name).Where(pattern.IsMatch).Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new CompletionResult("'" + CodeGeneration.EscapeSingleQuotedStringContent(name) + "'", name, CompletionResultType.ParameterValue, name)).ToArray();
    }
}
