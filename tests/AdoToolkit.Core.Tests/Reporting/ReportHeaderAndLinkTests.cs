using System.Text.Json;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

// The model/JSON portion of S2-6; HTML header and content-link assertions arrive in 2.2.
[Trait("Acceptance", "S2-6")]
public sealed class ReportHeaderAndLinkTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void MetadataIsPreservedAndLinksAreBuiltFromTheConnectionAndOwningProject(string culture)
    {
        ReportDocumentModel document = ReportFixture.Model("parameterized", culture);
        TestCaseReportModel model = Assert.Single(document.Cases);
        Assert.Equal(new Uri("https://ado.example.test/"), model.ServerUri);
        Assert.Equal(ReportFixture.Collection, model.CollectionUri);
        Assert.Equal(ReportFixture.Project, model.Project);
        Assert.Equal("https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_workitems/edit/10", model.WebUrl.AbsoluteUri);
        Assert.Equal("https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_testPlans/define?planId=40&suiteId=50", model.Suite!.WebUrl!.AbsoluteUri);
        Assert.Equal("https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_testPlans/define?planId=40", model.Suite.PlanWebUrl!.AbsoluteUri);
        Assert.Equal("https://ado.example.test/tfs/Collection%20A/Param%C3%A8tres%20%2F%20A/_workitems/edit/60", model.Parameters.SharedParameterSets[0].WebUrl!.AbsoluteUri);
        Assert.Equal(2, model.Priority);
        Assert.Equal("Not Automated", model.AutomationStatus);
        Assert.Equal("Area\\Équipe", model.AreaPath);
        Assert.Equal("Iteration\\One", model.IterationPath);
        Assert.Equal("Fictional Person", model.AssignedTo!.DisplayName);
        Assert.Equal("Another Person", model.ChangedBy!.DisplayName);
        Assert.Equal(ReportFixture.Timestamp.AddDays(-2), model.ChangedDate);
        Assert.Equal(ReportFixture.Timestamp, model.GeneratedAt);
        Assert.Equal("2.1.0-test", model.ToolkitVersion);
        Assert.Equal(culture == "fr-CA" ? "Cas de test" : "Test Case", model.Labels["TestCase"]);
        Assert.DoesNotContain("untrusted.example.test", ReportFixture.Render(document), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingMetadataAndUnresolvedSharedMetadataStayAbsent()
    {
        ReportDocumentModel document = ReportFixture.Model("partial");
        TestCaseReportModel model = document.Cases[0];
        Assert.Null(model.Priority);
        Assert.Null(model.AssignedTo);
        Assert.Null(model.ChangedBy);
        Assert.Null(model.AreaPath);
        Assert.Null(model.IterationPath);
        Assert.Null(model.AutomationStatus);
        Assert.Null(model.Suite);
        AdoSharedStepInfo shared = Assert.Single(model.SharedSteps);
        Assert.Equal(99, shared.Id);
        Assert.Null(shared.Title);
        Assert.Null(shared.Rev);
        Assert.Null(shared.TeamProject);
        Assert.Null(shared.WebUrl);
        using JsonDocument json = JsonDocument.Parse(ReportFixture.Render(document));
        JsonElement testCase = json.RootElement.GetProperty("cases")[0];
        foreach (string name in new[] { "priority", "assignedTo", "changedBy", "areaPath", "iterationPath", "automationStatus", "suite" })
            Assert.False(testCase.TryGetProperty(name, out _));
        Assert.False(testCase.GetProperty("sharedSteps")[0].TryGetProperty("webUrl", out _));
    }

    [Fact]
    public void NestedGroupLinksAndProvenanceArePreserved()
    {
        TestCaseReportModel model = ReportFixture.Model("nested").Cases[0];
        Assert.Equal(2, model.StepCount);
        Assert.Equal(Enumerable.Range(1, 4), model.Rows.Select(row => row.Sequence));
        Assert.Equal("2.1.1", model.Rows[3].Number);
        Assert.Equal(2, model.Rows[3].Depth);
        Assert.Equal(30, model.Rows[3].SourceWorkItemId);
        Assert.Equal(21, model.Rows[3].SourceStepId);
        Assert.Equal("https://ado.example.test/tfs/Collection%20A/Autre%20%2F%20%C3%A9quipe/_workitems/edit/20",
            model.Rows[1].SharedStep!.WebUrl!.AbsoluteUri);
        Assert.Equal(model.SharedSteps[0].WebUrl, model.Rows[3].SharedStepPath[0].WebUrl);
        Assert.Equal(model.SharedSteps[1].WebUrl, model.Rows[2].SharedStep!.WebUrl);
        Assert.All(model.Rows.Where(row => row.Kind == AdoTestStepKind.SharedStep), row => Assert.True(row.IsExpanded));
    }

    [Fact]
    public void DiagnosticsAreRerenderedWithArgumentsInReportCulture()
    {
        AdoTestCase original = ReportFixture.Case("partial");
        ReportDocumentModel document = ReportModelBuilder.Build(original,
            new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options("fr-CA"));
        TestCaseReportModel model = document.Cases[0];
        Assert.Equal(AdoTestCaseStatus.Partial, model.Status);
        Assert.Equal(3, model.ErrorCount);
        Assert.Equal(0, model.WarningCount);
        Assert.Equal(0, model.InformationCount);
        for (int index = 0; index < original.Diagnostics.Count; index++)
        {
            AdoDiagnostic input = original.Diagnostics[index];
            AdoDiagnostic output = model.Diagnostics[index];
            Assert.Equal(input.Arguments, output.Arguments);
            Assert.Equal(input.ReferenceChain, output.ReferenceChain);
            Assert.NotEqual(input.Message, output.Message);
            Assert.Equal(DiagnosticMessageRenderer.Render(input.Code, input.Arguments, model.Culture), output.Message);
        }
        Assert.Equal(model.Diagnostics[0].Message, model.Rows[1].DiagnosticMessage);
        Assert.Equal(model.Diagnostics[1].Message, model.Rows[2].DiagnosticMessage);
        Assert.Equal("Steps XML is invalid at line 7, position 19.", original.Diagnostics[2].Message);
        using JsonDocument json = JsonDocument.Parse(ReportFixture.Render(document));
        Assert.Equal(model.Diagnostics[2].Message, json.RootElement.GetProperty("cases")[0].GetProperty("diagnostics")[2].GetProperty("message").GetString());
    }

    [Fact]
    public void ModelsSnapshotInputListsAndRejectMismatchedProvenance()
    {
        AdoTestCase original = ReportFixture.Case("parameterized");
        AdoConnection connection = new() { CollectionUri = ReportFixture.Collection };
        ReportDocumentModel model = ReportModelBuilder.Build(original, connection, ReportFixture.Options());
        ((List<AdoTestStep>)original.Steps).Clear();
        ((Dictionary<string, string>)original.Parameters.Rows[0])["user"] = "changed";
        Assert.Single(model.Cases[0].Rows);
        Assert.Equal("alice", model.Cases[0].Parameters.Rows[0]["user"]);
        Assert.Throws<AdoConnectionMismatchException>(() => ReportModelBuilder.Build(original,
            new AdoConnection { CollectionUri = new Uri("https://different.example.test/Collection") }, ReportFixture.Options()));
    }

    [Fact]
    public void PlanRoutesUseInvariantPositiveIdsAndEscapeTheProject()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal("https://ado.example.test/tfs/Collection%20A/a%26b%2Fc%3Fd%23e/_testPlans/define?planId=1234567&suiteId=7654321",
                AdoWebLinks.TestPlan(ReportFixture.Collection, "a&b/c?d#e", 1234567, 7654321).AbsoluteUri);
            Assert.Throws<ArgumentOutOfRangeException>(() => AdoWebLinks.TestPlan(ReportFixture.Collection, "Project", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => AdoWebLinks.TestPlan(ReportFixture.Collection, "Project", 1, -1));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
