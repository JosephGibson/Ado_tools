using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.TestRuns;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The 5.3 report fixture with export results applied: attachment 51 (text) and 52 (JSON, sub-result 301).
[Trait("Acceptance", "S5-6")]
public sealed class AttachmentRenderingTests
{
    private const string Folder = "Rapport été #1.files-20260916T133000000Z";
    private const string Escaped = "Rapport%20%C3%A9t%C3%A9%20%231.files-20260916T133000000Z/";
    private static readonly byte[] Text = AttachmentFixture.Bytes("other-bytes.txt");
    private static readonly byte[] Json = AttachmentFixture.Bytes("valid.json");

    [Fact]
    public void DownloadedJsonAndTextRenderCollapsedPreviewsAndLocalLinks()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.txt", Text), (Downloaded, "r201-11-s301-a52.json", Json));
        Assert.Contains("<li id=\"f-1-a1-att51\" data-download-status=\"downloaded\">", report.Html, StringComparison.Ordinal);
        Assert.Contains("<a class=\"local-file\" data-local-file href=\"" + Escaped + "r201-11-s301-a52.json\">Local copy</a> <code>r201-11-s301-a52.json</code> <span>189 bytes</span>",
            report.Html, StringComparison.Ordinal);
        // Previews start collapsed; JSON is pretty-printed with two spaces and highlighted, text stays plain.
        string json = report.Html[report.Html.IndexOf("id=\"f-1-a1-att52\"", StringComparison.Ordinal)..];
        json = json[..json.IndexOf("</li>", StringComparison.Ordinal)];
        Assert.Contains("<details class=\"attachment-preview\"><summary>Preview</summary><div class=\"code-section\">", json, StringComparison.Ordinal);
        Assert.Contains("<code class=\"lang-json\">{\n  <span class=\"p\">&quot;title&quot;</span>", json, StringComparison.Ordinal);
        Assert.Contains("data-action=\"copy\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<code class=\"lang-text\">Synthetic trace bytes", report.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
        // Scripts ignored, every local link is still present.
        string withoutScripts = TestFailureMarkup.WithoutScripts(report.Html);
        Assert.Equal(2, withoutScripts.Split(" data-local-file ").Length - 1);
    }

    [Fact]
    public void JsonAboveTheInlineLimitIsLinkedOnlyAndFrenchLabelsApply()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.txt", Text), (Downloaded, "r201-11-s301-a52.json", Json),
            culture: "fr-CA", inlineLimit: 188);
        Assert.DoesNotContain("lang-json", report.Html, StringComparison.Ordinal);
        Assert.Contains("lang-text", report.Html, StringComparison.Ordinal);
        Assert.Contains(">Copie locale</a> <code>r201-11-s301-a52.json</code> <span>189 octets</span>", report.Html, StringComparison.Ordinal);
        Assert.Contains("Trop volumineuse pour être affichée ici; ouvrez la copie locale.", report.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
    }

    // The report-wide inline budget counts files in report order; later files are linked only.
    [Fact]
    public void FilesPastTheReportInlineBudgetAreLinkedOnly()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.txt", Text), (Downloaded, "r201-11-s301-a52.json", Json), inlineTotal: 200);
        Assert.Contains("lang-text", report.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("lang-json", report.Html, StringComparison.Ordinal);
        Assert.Contains("Not shown here: the report reached its limit for inline attachments; open the local copy.", report.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
    }

    [Fact]
    public void MismatchedFilesAreLinkedButNeverPreviewed()
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Mismatch, "r201-11-a51.bin", Json), (Mismatch, "r201-11-s301-a52.bin", Text));
        Assert.DoesNotContain("class=\"attachment-preview\"", report.Html, StringComparison.Ordinal);
        Assert.Equal(2, report.Html.Split("The content does not match the file type; saved as .bin and not previewed.").Length - 1);
        Assert.Contains("href=\"" + Escaped + "r201-11-a51.bin\">Local copy</a>", report.Html, StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
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
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.txt", Text), (Downloaded, "r201-11-s301-a52.json", Json), variant: "hostile");
        Assert.Contains(">&lt;/script&gt;&lt;script&gt;alert(&#x27;fixture-only&#x27;)&lt;/script&gt;", report.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"" + Escaped + "..", report.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("CON", string.Join(' ', report.Model.Failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments)
            .Select(a => a.LocalRelativePath ?? "")), StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(report.Html), report.Model, report.Folder);
    }

    public static TheoryData<string> Corruptions => new(["missing", "resized", "empty", "no-folder", "other-folder", "traversal", "unmarked",
        "marked-external", "local-image", "downloaded-png", "unlinked", "javascript", "marker-without-link", "unescaped", "open-attempt"]);

    [Theory]
    [MemberData(nameof(Corruptions))]
    public void ValidatorRejectsLocalLinksThatDoNotMatchTheDownloadedFiles(string corruption)
    {
        using TestDirectory directory = new();
        Rendered report = Render(directory, (Downloaded, "r201-11-a51.txt", Text), (Downloaded, "r201-11-s301-a52.json", Json));
        string html = report.Html;
        TestFailureReportModel model = report.Model;
        string? folder = report.Folder;
        string text = Path.Combine(report.Folder, "r201-11-a51.txt");
        switch (corruption)
        {
            case "missing": File.Delete(text); break;
            case "resized": File.AppendAllText(text, "x"); break;
            case "empty": File.WriteAllBytes(text, []); model = WithFiles(model, ("r201-11-a51.txt", 0), ("r201-11-s301-a52.json", 189)); break;
            case "no-folder": folder = null; break;
            case "other-folder": html = html.Replace(Escaped + "r201-11-a51.txt", "Other.files-20260916T133000000Z/r201-11-a51.txt", StringComparison.Ordinal); break;
            case "traversal": html = html.Replace(Escaped + "r201-11-a51.txt", Escaped + "..%2Fr201-11-a51.txt", StringComparison.Ordinal); break;
            case "unmarked": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><a href=\"notes.txt\">x</a>", StringComparison.Ordinal); break;
            case "marked-external": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><a data-local-file href=\"https://ado.example.test/x\">x</a>", StringComparison.Ordinal); break;
            case "local-image": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><img data-local-file src=\"" + Escaped + "r201-11-a51.txt\">", StringComparison.Ordinal); break;
            case "downloaded-png":
                File.Move(text, Path.Combine(report.Folder, "r201-11-a51.png"));
                html = html.Replace(Escaped + "r201-11-a51.txt", Escaped + "r201-11-a51.png", StringComparison.Ordinal);
                model = WithFiles(model, ("r201-11-a51.png", Text.Length), ("r201-11-s301-a52.json", 189));
                break;
            case "unlinked": model = WithFiles(model, ("r201-11-a51.txt", Text.Length), ("r201-11-s301-a52.json", 189), ("r201-11-a99.bin", 3)); break;
            case "javascript": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><a href=\"javascript:fixture()\">x</a>", StringComparison.Ordinal); break;
            case "marker-without-link": html = html.Replace("<main id=\"report-content\">", "<main id=\"report-content\"><span data-local-file>x</span>", StringComparison.Ordinal); break;
            case "unescaped": html = html.Replace(Escaped, Folder + "/", StringComparison.Ordinal); break;
            case "open-attempt": html = html.Replace("<details class=\"attempt\" id=\"f-1-a1\">", "<details class=\"attempt\" id=\"f-1-a1\" open>", StringComparison.Ordinal); break;
        }
        Assert.Throws<InvalidDataException>(() => TestFailureReportValidator.Validate(new StringReader(html), model, folder));
    }

    private sealed record Rendered(TestFailureReportModel Model, string Html, string Folder);

    private static Rendered Render(TestDirectory directory, (AdoTestAttachmentStatus Status, string? Name, byte[]? Bytes) first,
        (AdoTestAttachmentStatus Status, string? Name, byte[]? Bytes) second, string variant = "failed", string culture = "en-US",
        long inlineLimit = 262144, long inlineTotal = 8388608)
    {
        string folder = Directory.CreateDirectory(Path.Combine(directory.Root, Folder)).FullName;
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
                // Only JSON and text are ever downloaded, so the fixture's first attachment becomes text.
                AdoTestAttachment typed = new()
                {
                    Id = attachment.Id, RunId = attachment.RunId, ResultId = attachment.ResultId, SubResultId = attachment.SubResultId,
                    FileName = attachment.FileName, Size = attachment.Size, Comment = attachment.Comment, AttachmentType = attachment.AttachmentType,
                    Kind = index == 0 ? AdoTestAttachmentKind.Text : AdoTestAttachmentKind.Json,
                };
                return typed.WithDownload(status, name is null ? null : Folder + "/" + name);
            })]))]))];
        TestFailureReportModel updated = TestFailureReportModelBuilder.WithAttachments(model, failures, [], files.Count == 0 ? null : new TestFailureLocalAttachments
        {
            FolderName = Folder, SourceFolder = folder, Files = files, MaximumInlineJsonBytes = inlineLimit, MaximumInlineTotalBytes = inlineTotal,
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
}
