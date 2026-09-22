namespace AdoToolkit.Core.Connections;

public static class AdoWebLinks
{
    public static Uri Build(Uri collectionUri, string teamProject, int id) => BuildLink(collectionUri, teamProject, id, "/results?buildId=");

    public static Uri BuildDefinition(Uri collectionUri, string teamProject, int id) => BuildLink(collectionUri, teamProject, id, "?definitionId=");

    private static Uri BuildLink(Uri collectionUri, string teamProject, int id, string suffix)
    {
        ArgumentNullException.ThrowIfNull(collectionUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamProject);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return new Uri(collectionUri.AbsoluteUri.TrimEnd('/') + "/" + Uri.EscapeDataString(teamProject)
            + "/_build" + suffix + id.ToString(CultureInfo.InvariantCulture));
    }

    public static Uri TestPlan(Uri collectionUri, string teamProject, int planId, int? suiteId = null)
    {
        ArgumentNullException.ThrowIfNull(collectionUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamProject);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(planId);
        if (suiteId.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(suiteId.Value);
        string query = "planId=" + Uri.EscapeDataString(planId.ToString(CultureInfo.InvariantCulture));
        if (suiteId.HasValue)
            query += "&suiteId=" + Uri.EscapeDataString(suiteId.Value.ToString(CultureInfo.InvariantCulture));
        return new Uri(collectionUri.AbsoluteUri.TrimEnd('/') + "/" + Uri.EscapeDataString(teamProject)
            + "/_testPlans/define?" + query);
    }

    // Server 2020 test routes and view identifier are [Verify V-26]; ids are invariant integers.
    public static Uri TestRun(Uri collectionUri, string teamProject, int runId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runId);
        return new Uri(Prefix(collectionUri, teamProject) + "/_testManagement/runs?_a=runCharts&runId="
            + runId.ToString(CultureInfo.InvariantCulture));
    }

    public static Uri BuildTestResult(Uri collectionUri, string teamProject, int buildId, int? runId = null, int? resultId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        if (runId.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runId.Value);
        if (resultId.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resultId.Value);
        string query = "?buildId=" + buildId.ToString(CultureInfo.InvariantCulture)
            + "&view=" + Uri.EscapeDataString("ms.vss-test-web.build-test-results-tab");
        if (runId.HasValue) query += "&runId=" + runId.Value.ToString(CultureInfo.InvariantCulture);
        if (resultId.HasValue) query += "&resultId=" + resultId.Value.ToString(CultureInfo.InvariantCulture);
        return new Uri(Prefix(collectionUri, teamProject) + "/_build/results" + query);
    }

    // Server 2020 REST 6.0 attachment content, also used by the local downloader. A sub-result
    // shares its parent's route and is distinguished by the testSubResultId query parameter.
    public static Uri TestResultAttachment(Uri collectionUri, string teamProject, int runId, int resultId, int attachmentId, int? subResultId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resultId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attachmentId);
        if (subResultId.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(subResultId.Value);
        string url = Prefix(collectionUri, teamProject) + "/_apis/test/Runs/" + runId.ToString(CultureInfo.InvariantCulture)
            + "/Results/" + resultId.ToString(CultureInfo.InvariantCulture) + "/attachments/" + attachmentId.ToString(CultureInfo.InvariantCulture)
            + "?api-version=6.0-preview.1";
        if (subResultId.HasValue) url += "&testSubResultId=" + subResultId.Value.ToString(CultureInfo.InvariantCulture);
        return new Uri(url);
    }

    private static string Prefix(Uri collectionUri, string teamProject)
    {
        ArgumentNullException.ThrowIfNull(collectionUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamProject);
        return collectionUri.AbsoluteUri.TrimEnd('/') + "/" + Uri.EscapeDataString(teamProject);
    }

    // [Verify V-26] Only collection-hosted Git commits have this route.
    public static Uri? Commit(Uri collectionUri, string teamProject, string? repositoryType, string? repositoryId, string? sourceVersion)
    {
        if (!(string.Equals(repositoryType, "Git", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(repositoryType, "TfsGit", StringComparison.OrdinalIgnoreCase)) ||
            !Guid.TryParse(repositoryId, out Guid repository) || sourceVersion is not { Length: 40 } ||
            !sourceVersion.All(char.IsAsciiHexDigit)) return null;
        return new Uri(Prefix(collectionUri, teamProject) + "/_git/" + Uri.EscapeDataString(repository.ToString("D", CultureInfo.InvariantCulture))
            + "/commit/" + Uri.EscapeDataString(sourceVersion));
    }

    public static Uri WorkItem(Uri collectionUri, string teamProject, int id)
    {
        ArgumentNullException.ThrowIfNull(collectionUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamProject);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return new Uri(collectionUri.AbsoluteUri.TrimEnd('/') + "/" + Uri.EscapeDataString(teamProject)
            + "/_workitems/edit/" + id.ToString(CultureInfo.InvariantCulture));
    }
}
