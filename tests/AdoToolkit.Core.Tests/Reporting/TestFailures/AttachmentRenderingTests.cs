using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.TestRuns;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The 5.3 report fixture with export results applied: attachment 51 (PNG) and 52 (JSON, sub-result 301).
[Trait("Acceptance", "S5-6")]
public sealed class AttachmentRenderingTests
{
    private const string Folder = "Rapport été #1.files-20260916T133000000Z";
    private const string Escaped = "Rapport%20%C3%A9t%C3%A9%20%231.files-20260916T133000000Z/";
    private static readonly byte[] Png = AttachmentFixture.Bytes("pattern.png");
    private static readonly byte[] Json = AttachmentFixture.Bytes("valid.json");

    [Fact]
    public void DownloadedFilesRenderAThumbnailInlineJsonAndLocalLinks()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.png", Png), (Downloaded, "r201-11-s301-a52.json", Json));
        Assert.Contains("<li id=\"f-1-a1-att51\" data-attachment=\"51\" data-download-status=\"downloaded\">", report.Html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"attachment-local\"><div class=\"thumbnail-grid\"><a class=\"thumbnail\" data-local-file href=\""
            + Escaped + "r201-11-a51.png\"><img data-local-file src=\"" + Escaped + "r201-11-a51.png\" loading=\"lazy\" alt=\"Attachment image: Screenshot.PNG\"></a></div></div>",
            report.Html, StringComparison.Ordinal);
        Assert.Contains("<a class=\"local-file\" data-local-file href=\"" + Escaped + "r201-11-s301-a52.json\">Local copy</a> <code>r201-11-s301-a52.json</code> <span>189 bytes</span>",
            report.Html, StringComparison.Ordinal);
        // Inline JSON is pretty-printed with two spaces and highlighted after the metadata.
        string json = report.Html[report.Html.IndexOf("id=\"f-1-a1-att52\"", StringComparison.Ordinal)..];
        json = json[..json.IndexOf("</li>", StringComparison.Ordinal)];
        Assert.Contains("</dl><div class=\"code-section\">", json, StringComparison.Ordinal);
        Assert.Contains("<code class=\"lang-json\"><span class=\"tok-punct\">{</span><span class=\"tok-plain\">&#xA;  </span><span class=\"tok-property\">&quot;title&quot;</span>",
            json, StringComparison.Ordinal);
        Assert.Contains("data-action=\"copy\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", json, StringComparison.OrdinalIgnoreCase);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
        // Scripts ignored, every local link is still present.
        string withoutScripts = TestFailureMarkup.WithoutScripts(report.Html);
        Assert.Equal(3, withoutScripts.Split(" data-local-file ").Length - 1);
    }

    [Fact]
    public void JsonAboveTheInlineLimitIsLinkedOnlyAndFrenchLabelsApply()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.png", Png), (Downloaded, "r201-11-s301-a52.json", Json),
            culture: "fr-CA", inlineLimit: 188);
        Assert.DoesNotContain("lang-json", report.Html, StringComparison.Ordinal);
        Assert.Contains(">Copie locale</a> <code>r201-11-s301-a52.json</code> <span>189 octets</span>", report.Html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Image jointe : Screenshot.PNG\"", report.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
    }

    [Fact]
    public void HtmlOutputAndMismatchedFilesAreLinkedButNeverPreviewed()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Mismatch, "r201-11-a51.bin", Png), (Mismatch, "r201-11-s301-a52.bin", Json),
            kinds: (AdoTestAttachmentKind.Png, AdoTestAttachmentKind.Json));
        Assert.DoesNotContain("<img", report.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("lang-json", report.Html, StringComparison.Ordinal);
        Assert.Equal(2, report.Html.Split("The content does not match the file type; saved as .bin and not previewed.").Length - 1);
        Assert.Contains("href=\"" + Escaped + "r201-11-a51.bin\">Local copy</a>", report.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);

        Rendered html = Render(directory, (Downloaded, "r201-11-a51.html", Json), (NotRequested, null, null),
            kinds: (AdoTestAttachmentKind.Html, AdoTestAttachmentKind.Json), subFolder: "second");
        Assert.Contains("href=\"" + Escaped + "r201-11-a51.html\">Test output HTML (local file)</a> <code>r201-11-a51.html</code>",
            html.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(html.Html), html.Model, html.Folder);
    }

    [Theory]
    [InlineData(AdoTestAttachmentStatus.TooLarge, "Not downloaded: larger than the size limit.")]
    [InlineData(AdoTestAttachmentStatus.BudgetExceeded, "Not downloaded: the total attachment budget was reached.")]
    [InlineData(AdoTestAttachmentStatus.Failed, "Download failed; the original remains in Azure DevOps.")]
    public void AttachmentsWithoutAFileShowTheirStatusAndKeepTheAdoLink(AdoTestAttachmentStatus status, string note)
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (status, null, null), (status, null, null));
        Assert.Equal(2, report.Html.Split("<p class=\"attachment-status\">" + note + "</p>").Length - 1);
        Assert.DoesNotContain("data-local-file", report.Html, StringComparison.Ordinal);
        Assert.Contains(">Screenshot.PNG <span role=\"img\"", report.Html, StringComparison.Ordinal);
        Assert.Contains("data-download-status=\"" + status.ToString().ToLowerInvariant() + "\"", report.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, null);
    }

    [Fact]
    public void HostileRemoteNamesStayEncodedTextAndNeverBecomeLinks()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.png", Png), (Downloaded, "r201-11-s301-a52.json", Json), variant: "hostile");
        Assert.Contains("alt=\"Attachment image: &lt;/script&gt;&lt;script&gt;alert(&#x27;fixture-only&#x27;)&lt;/script&gt;", report.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"" + Escaped + "..", report.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("CON", string.Join(' ', report.Model.Failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments)
            .Select(a => a.LocalRelativePath ?? "")), StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
    }

    public static TheoryData<string> Corruptions => new(["missing", "resized", "empty", "no-folder", "other-folder", "traversal", "unmarked",
        "marked-external", "image-not-png", "unlinked", "javascript", "marker-without-link", "unescaped"]);

    [Theory]
    [MemberData(nameof(Corruptions))]
    public void ValidatorRejectsLocalLinksThatDoNotMatchTheDownloadedFiles(string corruption)
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.png", Png), (Downloaded, "r201-11-s301-a52.json", Json));
        string html = report.Html;
        TestFailureReportModel model = report.Model;
        string? folder = report.Folder;
        string png = Path.Combine(report.Folder, "r201-11-a51.png");
        switch (corruption)
        {
            case "missing": File.Delete(png); break;
            case "resized": File.AppendAllText(png, "x"); break;
            case "empty": File.WriteAllBytes(png, []); model = WithFiles(model, ("r201-11-a51.png", 0), ("r201-11-s301-a52.json", 189)); break;
            case "no-folder": folder = null; break;
            case "other-folder": html = html.Replace(Escaped + "r201-11-a51.png", "Other.files-20260916T133000000Z/r201-11-a51.png", StringComparison.Ordinal); break;
            case "traversal": html = html.Replace(Escaped + "r201-11-a51.png", Escaped + "..%2Fr201-11-a51.png", StringComparison.Ordinal); break;
            case "unmarked": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><a href=\"notes.txt\">x</a>", StringComparison.Ordinal); break;
            case "marked-external": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><a data-local-file href=\"https://ado.example.test/x\">x</a>", StringComparison.Ordinal); break;
            case "image-not-png": html = html.Replace("src=\"" + Escaped + "r201-11-a51.png", "src=\"" + Escaped + "r201-11-s301-a52.json", StringComparison.Ordinal); break;
            case "unlinked": model = WithFiles(model, ("r201-11-a51.png", Png.Length), ("r201-11-s301-a52.json", 189), ("r201-11-a99.bin", 3)); break;
            case "javascript": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><a href=\"javascript:fixture()\">x</a>", StringComparison.Ordinal); break;
            case "marker-without-link": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><span data-local-file>x</span>", StringComparison.Ordinal); break;
            case "unescaped": html = html.Replace(Escaped, Folder + "/", StringComparison.Ordinal); break;
        }
        Assert.Throws<InvalidDataException>(() => TestFailureReportValidator.Validate(new StringReader(html), model, folder));
    }

    private sealed record Rendered(TestFailureReportModel Model, string Html, string Folder);

    private static Rendered Render(TestDirectory directory, (AdoTestAttachmentStatus Status, string? Name, byte[]? Bytes) first,
        (AdoTestAttachmentStatus Status, string? Name, byte[]? Bytes) second, string variant = "failed", string culture = "en-US",
        long inlineLimit = 262144, (AdoTestAttachmentKind, AdoTestAttachmentKind)? kinds = null, string subFolder = "first")
    {
        string folder = Directory.CreateDirectory(Path.Combine(directory.Root, subFolder, Folder)).FullName;
        TestFailureReportModel model = TestFailureReportFixture.Model(variant, culture);
        Dictionary<string, long> files = new(StringComparer.OrdinalIgnoreCase);
        foreach ((AdoTestAttachmentStatus _, string? name, byte[]? bytes) in new[] { first, second })
        {
            if (name is null) continue;
            File.WriteAllBytes(Path.Combine(folder, name), bytes!);
            files.Add(name, bytes!.Length);
        }
        AdoTestFailure[] failures = [.. model.Failures.Select(failure => failure.WithAttempts([.. failure.Attempts.Select(attempt =>
            attempt.WithAttachments([.. attempt.Attachments.Select((attachment, index) =>
            {
                (AdoTestAttachmentStatus status, string? name, byte[]? _) = index == 0 ? first : second;
                AdoTestAttachment typed = kinds is null ? attachment : new AdoTestAttachment
                {
                    Id = attachment.Id, RunId = attachment.RunId, ResultId = attachment.ResultId, SubResultId = attachment.SubResultId,
                    FileName = attachment.FileName, Size = attachment.Size, Comment = attachment.Comment, AttachmentType = attachment.AttachmentType,
                    Kind = index == 0 ? kinds.Value.Item1 : kinds.Value.Item2,
                };
                return typed.WithDownload(status, name is null ? null : Folder + "/" + name);
            })]))]))];
        TestFailureReportModel updated = TestFailureReportModelBuilder.WithAttachments(model, failures, [], files.Count == 0 ? null : new TestFailureLocalAttachments
        {
            FolderName = Folder, SourceFolder = folder, Files = files, MaximumInlineJsonBytes = inlineLimit,
        });
        return new Rendered(updated, TestFailureReportFixture.Render(updated), folder);
    }

    private static TestFailureReportModel WithFiles(TestFailureReportModel model, params (string Name, long Length)[] files) =>
        TestFailureReportModelBuilder.WithAttachments(model, model.Failures, [], new TestFailureLocalAttachments
        {
            FolderName = model.LocalAttachments!.FolderName, SourceFolder = model.LocalAttachments.SourceFolder,
            MaximumInlineJsonBytes = model.LocalAttachments.MaximumInlineJsonBytes,
            Files = files.ToDictionary(file => file.Name, file => file.Length),
        });

    private const AdoTestAttachmentStatus Downloaded = AdoTestAttachmentStatus.Downloaded;
    private const AdoTestAttachmentStatus Mismatch = AdoTestAttachmentStatus.ContentMismatch;
    private const AdoTestAttachmentStatus NotRequested = AdoTestAttachmentStatus.NotRequested;
}
