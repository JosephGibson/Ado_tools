using System.Text;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting.Json;

public static class JsonTestCaseRenderer
{
    public static void Render(ReportDocumentModel model, TextWriter writer, bool includeSource = false)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);
        writer.Flush();
        using TextWriterUtf8Stream adapter = new(writer);
        Stream stream = writer is StreamWriter streamWriter && streamWriter.Encoding.CodePage == Encoding.UTF8.CodePage
            ? streamWriter.BaseStream : adapter;
        using Utf8JsonWriter json = new(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" });
        json.WriteStartObject();
        json.WriteNumber("schemaVersion", ReportDocumentModel.SchemaVersion);
        json.WriteStartObject("generator");
        json.WriteString("name", "AdoToolkit");
        json.WriteString("version", model.ToolkitVersion);
        json.WriteEndObject();
        json.WriteString("generatedAt", model.GeneratedAt);
        json.WriteString("culture", model.Culture.Name);
        json.WriteString("collectionUrl", model.CollectionUri.AbsoluteUri);
        json.WriteString("project", model.Project);
        json.WriteString("source", model.Source);
        if (model.IsMultiCase)
        {
            json.WriteStartObject("totals");
            json.WriteNumber("caseCount", model.Contents.Count);
            json.WriteNumber("complete", model.CompleteCount);
            json.WriteNumber("partial", model.PartialCount);
            json.WriteEndObject();
        }
        json.WriteStartArray("cases");
        foreach (TestCaseReportModel testCase in model.Cases)
        {
            WriteCase(json, testCase, includeSource);
            json.Flush();
        }
        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
    }

    private static void WriteCase(Utf8JsonWriter json, TestCaseReportModel model, bool includeSource)
    {
        json.WriteStartObject();
        json.WriteNumber("id", model.Id);
        json.WriteNumber("rev", model.Rev);
        json.WriteString("title", model.Title);
        json.WriteString("workItemType", model.WorkItemType);
        json.WriteString("state", model.State);
        json.WriteString("serverUrl", model.ServerUri.AbsoluteUri);
        json.WriteString("collectionUrl", model.CollectionUri.AbsoluteUri);
        json.WriteString("project", model.Project);
        Optional(json, "priority", model.Priority);
        Optional(json, "automationStatus", model.AutomationStatus);
        Optional(json, "areaPath", model.AreaPath);
        Optional(json, "iterationPath", model.IterationPath);
        WriteIdentity(json, "assignedTo", model.AssignedTo);
        WriteIdentity(json, "changedBy", model.ChangedBy);
        json.WriteString("changedDate", model.ChangedDate);
        json.WriteString("retrievedAt", model.RetrievedAt);
        json.WriteString("webUrl", model.WebUrl.AbsoluteUri);
        json.WriteString("status", model.Status.ToString());
        json.WriteNumber("stepCount", model.StepCount);
        WriteSuite(json, model.Suite);
        json.WriteStartArray("rows");
        foreach (ReportRow row in model.Rows)
        {
            json.WriteStartObject();
            json.WriteString("number", row.Number);
            json.WriteNumber("sequence", row.Sequence);
            json.WriteString("kind", row.Kind.ToString());
            json.WriteString("action", row.Action);
            json.WriteString("expectedResult", row.ExpectedResult);
            if (includeSource)
            {
                json.WriteString("actionSource", row.ActionSource);
                json.WriteString("expectedResultSource", row.ExpectedResultSource);
            }
            if (row.SharedStep is not null)
            {
                json.WritePropertyName("sharedStep");
                WriteSharedStep(json, row.SharedStep);
            }
            json.WriteBoolean("isExpanded", row.IsExpanded);
            json.WriteNumber("sourceWorkItemId", row.SourceWorkItemId);
            json.WriteNumber("sourceRev", row.SourceRev);
            Optional(json, "sourceStepId", row.SourceStepId);
            json.WriteNumber("depth", row.Depth);
            Optional(json, "diagnosticCode", row.DiagnosticCode);
            Optional(json, "diagnosticMessage", row.DiagnosticMessage);
            json.WriteStartArray("sharedStepPath");
            foreach (AdoSharedStepInfo shared in row.SharedStepPath) WriteSharedStep(json, shared);
            json.WriteEndArray();
            json.WriteEndObject();
            json.Flush();
        }
        json.WriteEndArray();
        json.WriteStartArray("sharedSteps");
        foreach (AdoSharedStepInfo shared in model.SharedSteps)
        {
            WriteSharedStep(json, shared);
            json.Flush();
        }
        json.WriteEndArray();
        WriteParameters(json, model.Parameters);
        json.WriteStartObject("diagnosticsSummary");
        json.WriteNumber("errorCount", model.ErrorCount);
        json.WriteNumber("warningCount", model.WarningCount);
        json.WriteNumber("informationCount", model.InformationCount);
        json.WriteEndObject();
        json.WriteStartArray("diagnostics");
        foreach (AdoDiagnostic diagnostic in model.Diagnostics)
        {
            json.WriteStartObject();
            json.WriteString("code", diagnostic.Code);
            json.WriteString("severity", diagnostic.Severity.ToString());
            json.WriteStartArray("arguments");
            foreach (string argument in diagnostic.Arguments) json.WriteStringValue(argument);
            json.WriteEndArray();
            json.WriteString("message", diagnostic.Message);
            Optional(json, "workItemId", diagnostic.WorkItemId);
            Optional(json, "stepNumber", diagnostic.StepNumber);
            json.WriteStartArray("referenceChain");
            foreach (int id in diagnostic.ReferenceChain) json.WriteNumberValue(id);
            json.WriteEndArray();
            json.WriteEndObject();
            json.Flush();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteSharedStep(Utf8JsonWriter json, AdoSharedStepInfo shared)
    {
        json.WriteStartObject();
        json.WriteNumber("id", shared.Id);
        Optional(json, "title", shared.Title);
        Optional(json, "rev", shared.Rev);
        Optional(json, "project", shared.TeamProject);
        Optional(json, "webUrl", shared.WebUrl?.AbsoluteUri);
        json.WriteNumber("referenceCount", shared.ReferenceCount);
        json.WriteEndObject();
    }

    private static void WriteIdentity(Utf8JsonWriter json, string name, AdoIdentityRef? identity)
    {
        if (identity is null) return;
        json.WriteStartObject(name);
        Optional(json, "id", identity.Id);
        json.WriteString("displayName", identity.DisplayName);
        Optional(json, "uniqueName", identity.UniqueName);
        json.WriteEndObject();
    }

    private static void WriteSuite(Utf8JsonWriter json, AdoTestSuiteRef? suite)
    {
        if (suite is null) return;
        json.WriteStartObject("suite");
        json.WriteNumber("planId", suite.PlanId);
        json.WriteNumber("suiteId", suite.SuiteId);
        json.WriteString("planName", suite.PlanName);
        json.WriteString("suiteName", suite.SuiteName);
        json.WriteStartArray("suitePath");
        foreach (string segment in suite.SuitePath) json.WriteStringValue(segment);
        json.WriteEndArray();
        json.WriteString("project", suite.TeamProject);
        json.WriteString("collectionUrl", suite.CollectionUri.AbsoluteUri);
        Optional(json, "planWebUrl", suite.PlanWebUrl?.AbsoluteUri);
        Optional(json, "webUrl", suite.WebUrl?.AbsoluteUri);
        json.WriteEndObject();
    }

    private static void WriteParameters(Utf8JsonWriter json, AdoTestParameters parameters)
    {
        json.WriteStartObject("parameters");
        json.WriteString("source", parameters.Source.ToString());
        json.WriteStartArray("names");
        foreach (string name in parameters.Names) json.WriteStringValue(name);
        json.WriteEndArray();
        json.WriteStartArray("rows");
        foreach (IReadOnlyDictionary<string, string> row in parameters.Rows)
        {
            json.WriteStartObject();
            foreach ((string name, string value) in row.OrderBy(pair => pair.Key, StringComparer.Ordinal)) json.WriteString(name, value);
            json.WriteEndObject();
            json.Flush();
        }
        json.WriteEndArray();
        json.WriteStartArray("sharedParameterSets");
        foreach (AdoSharedParameterInfo shared in parameters.SharedParameterSets)
        {
            json.WriteStartObject();
            json.WriteNumber("id", shared.Id);
            Optional(json, "title", shared.Title);
            Optional(json, "rev", shared.Rev);
            Optional(json, "project", shared.TeamProject);
            Optional(json, "webUrl", shared.WebUrl?.AbsoluteUri);
            json.WriteEndObject();
            json.Flush();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void Optional(Utf8JsonWriter json, string name, string? value)
    {
        if (value is not null) json.WriteString(name, value);
    }

    private static void Optional(Utf8JsonWriter json, string name, int? value)
    {
        if (value.HasValue) json.WriteNumber(name, value.Value);
    }
}
