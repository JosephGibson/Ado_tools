using System.Text.Json.Serialization;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.WorkItems;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Http;

// Server 2020 sends many numeric fields as JSON strings (reference IDs such as build.id and
// testRun.id), so every number also reads from numeric text. Non-numeric text still fails.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(ProjectPageDto))]
[JsonSerializable(typeof(IdentityDto))]
[JsonSerializable(typeof(WorkItemBatchRequestDto))]
[JsonSerializable(typeof(WorkItemBatchDto))]
[JsonSerializable(typeof(WiqlRequestDto))]
[JsonSerializable(typeof(WiqlResponseDto))]
[JsonSerializable(typeof(TestPlanPageDto))]
[JsonSerializable(typeof(TestSuitePageDto))]
[JsonSerializable(typeof(SuiteTestCasePageDto))]
[JsonSerializable(typeof(BuildDefinitionPageDto))]
[JsonSerializable(typeof(BuildPageDto))]
[JsonSerializable(typeof(TimelineDto))]
[JsonSerializable(typeof(BuildLogPageDto))]
[JsonSerializable(typeof(BuildDto))]
[JsonSerializable(typeof(TestRunPageDto))]
[JsonSerializable(typeof(TestResultPageDto))]
[JsonSerializable(typeof(TestResultDto))]
[JsonSerializable(typeof(TestAttachmentPageDto))]
internal sealed partial class AdoJsonContext : JsonSerializerContext { }
