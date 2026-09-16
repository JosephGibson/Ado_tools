using System.Xml;
using System.Xml.Linq;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.RichText;

namespace AdoToolkit.Core.TestManagement;

public static class StepsXmlParser
{
    public static StepDocument Parse(string? xml, int workItemId, int rev, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        List<StepNode> nodes = [];
        List<AdoDiagnostic> diagnostics = [];
        void Add(string code, params string[] arguments) => diagnostics.Add(
            DiagnosticMessageRenderer.Create(code, culture, workItemId, arguments));
        try
        {
            if (string.IsNullOrWhiteSpace(xml)) Add(DiagnosticCodes.EmptySteps);
            else
            {
                XDocument document = SafeXml.Parse(xml);
                XElement root = document.Root!;
                if (root.Name.LocalName != "steps")
                {
                    IXmlLineInfo location = root;
                    Add(DiagnosticCodes.MalformedStepsXml, location.LineNumber.ToString(CultureInfo.InvariantCulture),
                        location.LinePosition.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    foreach (XElement element in root.Elements())
                    {
                        int? sourceId = ParseInteger(element.Attribute("id")?.Value);
                        switch (element.Name.LocalName)
                        {
                            case "step":
                                XElement[] values = element.Elements().Where(e => e.Name.LocalName == "parameterizedString").ToArray();
                                if (values.Length > 2) Add(DiagnosticCodes.UnknownStepElement);
                                string actionSource = values.ElementAtOrDefault(0)?.Value ?? string.Empty;
                                string expectedSource = values.ElementAtOrDefault(1)?.Value ?? string.Empty;
                                string ConvertValue(int index, string source)
                                {
                                    if (values.ElementAtOrDefault(index)?.Attribute("isformatted")?.Value != "true") return source;
                                    PlainTextResult converted = PlainTextConverter.Convert(source, culture, workItemId);
                                    diagnostics.AddRange(converted.Diagnostics);
                                    return converted.Text;
                                }
                                string action = ConvertValue(0, actionSource);
                                string expected = ConvertValue(1, expectedSource);
                                string? type = element.Attribute("type")?.Value;
                                AdoTestStepKind kind;
                                if (type == "ActionStep") kind = AdoTestStepKind.Action;
                                else if (type == "ValidateStep") kind = AdoTestStepKind.Validate;
                                else
                                {
                                    Add(DiagnosticCodes.UnknownStepType);
                                    kind = expected.Length == 0 ? AdoTestStepKind.Action : AdoTestStepKind.Validate;
                                }
                                nodes.Add(new StepNode { Kind = kind, SourceStepId = sourceId, Action = action,
                                    ExpectedResult = expected, ActionSource = actionSource, ExpectedResultSource = expectedSource });
                                break;
                            case "compref":
                                int? reference = ParseInteger(element.Attribute("ref")?.Value);
                                if (reference is null or <= 0) { reference = null; Add(DiagnosticCodes.InvalidSharedStepReference); }
                                if (element.HasElements) Add(DiagnosticCodes.UnexpectedComprefChildren);
                                nodes.Add(new StepNode { Kind = AdoTestStepKind.SharedStep, SourceStepId = sourceId,
                                    SharedStepId = reference, DiagnosticCode = reference is null ? DiagnosticCodes.InvalidSharedStepReference : null });
                                break;
                            default:
                                Add(DiagnosticCodes.UnknownStepElement);
                                break;
                        }
                    }
                    if (!root.HasElements) Add(DiagnosticCodes.EmptySteps);
                }
            }
        }
        catch (XmlException exception)
        {
            nodes.Clear();
            diagnostics.Clear();
            Add(DiagnosticCodes.MalformedStepsXml, exception.LineNumber.ToString(CultureInfo.InvariantCulture),
                exception.LinePosition.ToString(CultureInfo.InvariantCulture));
        }
        return new StepDocument { WorkItemId = workItemId, Rev = rev, Nodes = nodes.AsReadOnly(), Diagnostics = diagnostics.AsReadOnly() };
    }

    private static int? ParseInteger(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
}
