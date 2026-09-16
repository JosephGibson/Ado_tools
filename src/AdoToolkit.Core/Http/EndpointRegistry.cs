using System.Net.Http;

namespace AdoToolkit.Core.Http;

internal static class EndpointRegistry
{
    internal static EndpointDefinition ProjectsList { get; } = new(
        "ProjectsList", HttpMethod.Get, "_apis/projects", "6.0", PagingStrategy.TopSkip, true, TimeoutClass.Metadata);
    internal static EndpointDefinition WorkItemsBatch { get; } = new(
        "WorkItemsBatch", HttpMethod.Post, "_apis/wit/workitemsbatch", "6.0", PagingStrategy.IdChunks, true, TimeoutClass.Query, 200);
    internal static EndpointDefinition WorkItemTypeCategory { get; } = new(
        "WorkItemTypeCategory", HttpMethod.Get, "{project}/_apis/wit/workitemtypecategories/{category}", "6.0", PagingStrategy.None, true, TimeoutClass.Metadata);
    // WIQL has no paging: the 20,000-result cap is enforced by WiqlService (§9.2).
    internal static EndpointDefinition Wiql { get; } = new(
        "Wiql", HttpMethod.Post, "{project}/_apis/wit/wiql", "6.0", PagingStrategy.None, true, TimeoutClass.Query);
    // Testplan-area preview versions are pinned pending V-04.
    internal static EndpointDefinition TestPlansList { get; } = new(
        "TestPlansList", HttpMethod.Get, "{project}/_apis/testplan/plans", "6.0-preview.1", PagingStrategy.ContinuationHeader, true, TimeoutClass.Metadata);
    internal static EndpointDefinition TestSuitesForPlan { get; } = new(
        "TestSuitesForPlan", HttpMethod.Get, "{project}/_apis/testplan/Plans/{planId}/suites", "6.0-preview.1", PagingStrategy.ContinuationHeader, true, TimeoutClass.Metadata);
    internal static EndpointDefinition SuiteTestCaseList { get; } = new(
        "SuiteTestCaseList", HttpMethod.Get, "{project}/_apis/testplan/Plans/{planId}/Suites/{suiteId}/TestCase", "6.0-preview.2", PagingStrategy.ContinuationHeader, true, TimeoutClass.Query);
    internal static EndpointDefinition BuildDefinitionsList { get; } = new(
        "BuildDefinitionsList", HttpMethod.Get, "{project}/_apis/build/definitions", "6.0", PagingStrategy.ContinuationHeader, true, TimeoutClass.Metadata);
    internal static EndpointDefinition BuildsList { get; } = new(
        "BuildsList", HttpMethod.Get, "{project}/_apis/build/builds", "6.0", PagingStrategy.ContinuationHeader, true, TimeoutClass.Query);
    internal static EndpointDefinition BuildTimeline { get; } = new(
        "BuildTimeline", HttpMethod.Get, "{project}/_apis/build/builds/{buildId}/timeline", "6.0", PagingStrategy.None, true, TimeoutClass.Metadata);
    internal static EndpointDefinition BuildLog { get; } = new(
        "BuildLog", HttpMethod.Get, "{project}/_apis/build/builds/{buildId}/logs/{logId}", "6.0", PagingStrategy.None, true, TimeoutClass.Download, Accept: "text/plain");
    internal static EndpointDefinition BuildLogsList { get; } = new(
        "BuildLogsList", HttpMethod.Get, "{project}/_apis/build/builds/{buildId}/logs", "6.0", PagingStrategy.None, true, TimeoutClass.Metadata);
    internal static EndpointDefinition BuildGet { get; } = new(
        "BuildGet", HttpMethod.Get, "{project}/_apis/build/builds/{buildId}", "6.0", PagingStrategy.None, true, TimeoutClass.Metadata);
    // Test-area routes, versions and fields are pinned pending V-19, V-20, V-21 and V-23.
    internal static EndpointDefinition TestRunsList { get; } = new(
        "TestRunsList", HttpMethod.Get, "{project}/_apis/test/runs", "6.0", PagingStrategy.TopSkip, true, TimeoutClass.Query);
    internal static EndpointDefinition TestResultsList { get; } = new(
        "TestResultsList", HttpMethod.Get, "{project}/_apis/test/Runs/{runId}/results", "6.0", PagingStrategy.TopSkip, true, TimeoutClass.Query);
    internal static EndpointDefinition TestResultGet { get; } = new(
        "TestResultGet", HttpMethod.Get, "{project}/_apis/test/Runs/{runId}/results/{resultId}", "6.0", PagingStrategy.None, true, TimeoutClass.Query);
    internal static EndpointDefinition TestResultAttachmentsList { get; } = new(
        "TestResultAttachmentsList", HttpMethod.Get, "{project}/_apis/test/Runs/{runId}/Results/{resultId}/attachments", "6.0-preview.1",
        PagingStrategy.None, true, TimeoutClass.Metadata);
    // Same route as the result list; the sub-result is selected by the testSubResultId parameter.
    internal static EndpointDefinition TestSubResultAttachmentsList { get; } = new(
        "TestSubResultAttachmentsList", HttpMethod.Get, "{project}/_apis/test/Runs/{runId}/Results/{resultId}/attachments", "6.0-preview.1",
        PagingStrategy.None, true, TimeoutClass.Metadata);
    internal static EndpointDefinition TestResultAttachmentContent { get; } = new(
        "TestResultAttachmentContent", HttpMethod.Get, "{project}/_apis/test/Runs/{runId}/Results/{resultId}/attachments/{attachmentId}", "6.0-preview.1",
        PagingStrategy.None, true, TimeoutClass.Download, Accept: "application/octet-stream");
    internal static IReadOnlyList<EndpointDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        ProjectsList, WorkItemsBatch, WorkItemTypeCategory, Wiql, TestPlansList, TestSuitesForPlan, SuiteTestCaseList,
        BuildDefinitionsList, BuildsList, BuildTimeline, BuildLogsList, BuildLog, BuildGet,
        TestRunsList, TestResultsList, TestResultGet, TestResultAttachmentsList, TestSubResultAttachmentsList,
        TestResultAttachmentContent,
    });
}
