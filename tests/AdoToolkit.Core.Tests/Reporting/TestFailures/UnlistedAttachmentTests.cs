using System.Text.RegularExpressions;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// A set gathered with SkipAttachments lists no attachment of any attempt. Its report says that the
// attachments were not listed where it would otherwise count them, in the header and in the Runs
// table, and its export has nothing to download. The grouped fixture has four runs, the first of
// them outside the attachment window.
public sealed class UnlistedAttachmentTests
{
    [Theory]
    [InlineData("en-US", "Attachments", "not listed", "Not listed", "Attachments were not listed: ")]
    [InlineData("fr-CA", "Pièces jointes", "non répertoriées", "Non répertoriées", "Les pièces jointes n’ont pas été répertoriées : ")]
    public void HeaderAndRunsTableSayTheAttachmentsWereNotListed(string culture, string label, string note, string cell, string sentence)
    {
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(Unlisted(), TestFailureReportFixture.Options(culture));
        Assert.False(model.AttachmentsListed);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);

        // The chip has a note instead of a count.
        Assert.Contains("<span class=\"count-chip status-attachments\"><span class=\"count-label\">" + label + "</span><span class=\"count-note\">"
            + note + "</span></span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span class=\"count-label\">" + label + "</span><strong", html, StringComparison.Ordinal);
        // Every run says so, the one that started before the window included, and none is muted.
        string view = Section(html, "<section class=\"view\" id=\"runs\" data-view>", "</section>\n");
        Assert.Equal([200, 201, 202, 203], Regex.Matches(view, "<tr data-run=\"([0-9]+)\"").Select(static match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
        Assert.Equal(4, Regex.Count(view, "<td>" + cell + "</td></tr>"));
        Assert.DoesNotContain("outside-window", view, StringComparison.Ordinal);
        // The note under the table says why, instead of describing the window.
        string expected = Messages.Get(AdoMessage.TestReportAttachmentsNotListedNote, model.Culture);
        Assert.StartsWith(sentence, expected, StringComparison.Ordinal);
        Assert.EndsWith("Get-AdoBuildTestFailure -SkipAttachments.", expected, StringComparison.Ordinal);
        Assert.Contains("</tbody></table></div>\n<p class=\"window-note\">" + SinkEncoding.Attribute(expected) + "</p>\n", view, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(view, "class=\"window-note\""));
        // No filter that could only hide every test.
        Assert.DoesNotContain("data-toggle=\"attachments\"", html, StringComparison.Ordinal);
    }

    // A set from any other source lists its attachments, so its report is the one the goldens hold.
    [Fact]
    public void ASetListsItsAttachmentsUnlessItSaysOtherwise()
    {
        AdoBuildTestFailureSet set = TestFailureReportFixture.Set("grouped");
        Assert.True(set.AttachmentsListed);
        TestFailureReportModel model = TestFailureReportFixture.Model("grouped");
        Assert.True(model.AttachmentsListed);
        string html = TestFailureReportFixture.Render(model);
        // Three attachments, one of them in the run outside the window.
        Assert.Contains("<span class=\"count-chip status-attachments\"><span class=\"count-label\">Attachments</span><strong class=\"count-value\">2</strong></span>",
            html, StringComparison.Ordinal);
        Assert.Contains("data-toggle=\"attachments\"", html, StringComparison.Ordinal);
        Assert.Contains("<tr data-run=\"200\" class=\"outside-window\">", html, StringComparison.Ordinal);

        // Downloads keep the flag.
        TestFailureReportModel unlisted = TestFailureReportModelBuilder.Build(Unlisted(), TestFailureReportFixture.Options("en-US"));
        Assert.False(TestFailureReportModelBuilder.WithAttachments(unlisted, unlisted.Failures, [], null).AttachmentsListed);
    }

    // Nothing was listed, so nothing is selected whatever the switches, and the export needs no downloader.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportDownloadsNothingAndNeedsNoDownloader(bool allRuns)
    {
        using TestDirectory directory = new();
        TestFailureExporter exporter = new(new SilentLauncher());
        TestFailureExportPlan plan = exporter.Prepare(Unlisted(), new TestFailureExportOptions
        {
            Path = directory.Root, Culture = "en-US", SessionCulture = CultureInfo.GetCultureInfo("en-US"), GeneratedAt = TestFailureReportFixture.Clock,
            ToolkitVersion = "5.3.0-test", AllRunAttachments = allRuns, IncludeFlaky = true,
        });
        Assert.False(plan.DownloadsAttachments);
        Assert.Null(plan.AttachmentDirectory);
        TestFailureExportResult result = await exporter.ExportAsync(plan, null, null, TestContext.Current.CancellationToken);
        Assert.Null(result.AttachmentDirectory);
        Assert.Empty(result.Diagnostics);
        string html = File.ReadAllText(result.Report.FullName);
        Assert.Contains("<span class=\"count-label\">Attachments</span><span class=\"count-note\">not listed</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-local-file", html, StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(directory.Root));
    }

    // The grouped fixture as SkipAttachments retrieves it: every attempt, none with an attachment.
    private static AdoBuildTestFailureSet Unlisted()
    {
        AdoBuildTestFailureSet set = TestFailureReportFixture.Set("grouped");
        return new AdoBuildTestFailureSet
        {
            Build = set.Build, Runs = set.Runs, Summary = set.Summary, History = set.History,
            Failures = [.. set.Failures.Select(static failure => failure.WithAttempts([.. failure.Attempts.Select(static attempt => attempt.WithAttachments([]))]))],
            FailedCount = set.FailedCount, FlakyCount = set.FlakyCount, Status = set.Status, Diagnostics = set.Diagnostics, RetrievedAt = set.RetrievedAt,
            CollectionUri = set.CollectionUri, AttachmentsListed = false,
        };
    }

    private static string Section(string html, string opening, string closing)
    {
        int start = html.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, opening);
        int end = html.IndexOf(closing, start + opening.Length, StringComparison.Ordinal);
        Assert.True(end > start, closing);
        return html[start..end];
    }

    private sealed class SilentLauncher : IDocumentLauncher
    {
        public void Open(string path) => Assert.Fail("This export must not launch a browser.");
    }
}
