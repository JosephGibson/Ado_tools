using System.Resources;

namespace AdoToolkit.Core.Diagnostics;

public static class DiagnosticMessageRenderer
{
    private static readonly ResourceManager Resources = new("AdoToolkit.Core.Resources.Strings", typeof(Messages).Assembly);

    public static string Render(string code, IReadOnlyList<string> arguments, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(culture);
        _ = DiagnosticCodes.GetSeverity(code);
        string template = Resources.GetString(code, culture) ?? throw new InvalidOperationException(code);
        return string.Format(culture, template, arguments.Cast<object>().ToArray());
    }

    public static AdoDiagnostic Create(string code, CultureInfo culture, int? workItemId = null,
        IEnumerable<string>? arguments = null, string? stepNumber = null, IEnumerable<int>? referenceChain = null)
    {
        IReadOnlyList<string> values = Array.AsReadOnly(arguments?.ToArray() ?? []);
        return new AdoDiagnostic
        {
            Code = code,
            Severity = DiagnosticCodes.GetSeverity(code),
            Arguments = values,
            Message = Render(code, values, culture),
            WorkItemId = workItemId,
            StepNumber = stepNumber,
            ReferenceChain = Array.AsReadOnly(referenceChain?.ToArray() ?? []),
        };
    }
}
