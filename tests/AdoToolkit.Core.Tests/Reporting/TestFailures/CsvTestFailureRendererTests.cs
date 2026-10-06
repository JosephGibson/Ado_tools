using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The CSV file of Export-AdoBuildTestFailure -Format Csv: fixed English columns, invariant values,
// RFC 4180 quoting and formula-safe text. The class runs in both test cultures, and the file must
// not change with either.
public sealed class CsvTestFailureRendererTests
{
    private const string Header = "Build,Ordinal,Test,Title,Classification,Attempts,Latest error,Owner,Priority,"
        + "Test case ID,Test case state,Open bugs,Bug IDs,Bug states,New,Since,Primary error,Error kind,Error rule,Distinct errors";
    private const string Refused = "System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it.";
    private static readonly Uri Collection = new("https://ado.example.test/tfs/Collection/");
    private const string Project = "Synthetic Web";
    // Four hours behind UTC, so a build that finished at 02:00 UTC finished the day before here.
    private static readonly DateTimeOffset Clock = new(2026, 9, 16, 9, 30, 0, TimeSpan.FromHours(-4));

    [Fact]
    public void ColumnsKeepTheirNamesAndOrder()
    {
        Assert.Equal(["Build", "Ordinal", "Test", "Title", "Classification", "Attempts", "Latest error", "Owner", "Priority",
            "Test case ID", "Test case state", "Open bugs", "Bug IDs", "Bug states", "New", "Since",
            "Primary error", "Error kind", "Error rule", "Distinct errors"], CsvTestFailureRenderer.Columns);
        Assert.Equal(Header, string.Join(",", CsvTestFailureRenderer.Columns));
    }

    // The error columns: the whole message of the test's primary error, from the latest attempt that
    // had it; whether that error is specific or generic; the rule that named it, a configured one by
    // its name and a built-in one by its ID; and how many errors the failed attempts had.
    [Fact]
    public void ErrorColumnsNameThePrimaryErrorItsKindAndItsRule()
    {
        AdoTestFailure mixed = Failure("Synthetic.A.Mixed", errors: ["Expected 1, actual 2\nat line 2", "Expected 1, actual 3\nat line 3", Refused]);
        AdoTestFailure refused = Failure("Synthetic.B.Refused", errors: [Refused]);
        AdoTestFailure named = Failure("Synthetic.C.Named", errors: ["Home page did not load within 30 seconds"]);
        AdoTestFailure silent = Failure("Synthetic.D.Silent", error: null);
        List<string[]> rows = Cells(Render(Model(Set(mixed, refused, named, silent),
            rules: [new ErrorRuleOptions { Name = "=Home page", Patterns = ["Home page did not load*"] }])));
        // The latest error is the generic one; the primary error is the one of the most attempts.
        Assert.Equal(Refused, rows[1][6]);
        Assert.Equal(["Expected 1, actual 3\nat line 3", "Specific", "", "2"], rows[1][16..]);
        Assert.Equal([Refused, "Generic", "ConnectionRefused", "1"], rows[2][16..]);
        // A configured name is text, so a formula trigger is neutralized.
        Assert.Equal(["Home page did not load within 30 seconds", "Generic", "'=Home page", "1"], rows[3][16..]);
        Assert.Equal(["", "", "", "0"], rows[4][16..]);
    }

    [Fact]
    public void EmptySetWritesTheHeaderOnly()
    {
        TestFailureReportModel model = Model(Set());
        Assert.Equal("﻿" + Header + "\r\n", Render(model));
        using TestDirectory directory = new();
        string path = Write(directory, model);
        CsvTestFailureRenderer.Validate(path, model);
    }

    // The byte order mark comes first, and every record, the last one included, ends with CRLF.
    [Fact]
    public void FileStartsWithAByteOrderMarkAndEndsEveryRecordWithCrlf()
    {
        TestFailureReportModel model = Model(Set(Failure("Synthetic.Checkout.Taxes"), Failure("Synthetic.Checkout.Totals")));
        using TestDirectory directory = new();
        byte[] bytes = File.ReadAllBytes(Write(directory, model));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        string text = new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
        Assert.EndsWith("\r\n", text, StringComparison.Ordinal);
        Assert.Equal(3, text.Split("\r\n").Length - 1);
        Assert.Equal(3, text.Count(static character => character == '\n'));
        Assert.StartsWith(Header + "\r\n401,1,Synthetic.Checkout.Taxes,", text, StringComparison.Ordinal);
    }

    // A comma, a quote or a line break puts the field in quotes and doubles its quotes; the error is
    // the whole message with its own line breaks, not the first line that the report's table shows.
    [Fact]
    public void QuotesCommasQuotesAndLineBreaksPerRfc4180()
    {
        const string error = "Assert.Equal() Failure\r\nExpected: 1, 2\nActual:   \"3\"";
        TestFailureReportModel model = Model(Set(Failure("Synthetic.Checkout.Totals", error: error, title: "Totals, \"net\" of tax")));
        string expected = "﻿" + Header + "\r\n" + string.Join(",", "401", "1", "Synthetic.Checkout.Totals", "\"Totals, \"\"net\"\" of tax\"",
            "Failed", "1", "\"Assert.Equal() Failure\r\nExpected: 1, 2\nActual:   \"\"3\"\"\"", "", "", "", "", "0", "", "", "", "",
            "\"Assert.Equal() Failure\r\nExpected: 1, 2\nActual:   \"\"3\"\"\"", "Specific", "", "1") + "\r\n";
        Assert.Equal(expected, Render(model));
        Assert.Equal(error, Cells(Render(model))[1][6]);
        Assert.Equal(error, Cells(Render(model))[1][16]);
    }

    // A cell that a spreadsheet would read as a formula starts with an apostrophe instead. Integers
    // and dates are never changed, even a negative priority.
    [Theory]
    [InlineData("=")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("@")]
    [InlineData("\t")]
    [InlineData("\r")]
    public void NeutralizesFormulaTriggersInTextOnly(string trigger)
    {
        string formula = trigger + "SUM(1+1)";
        AdoTestFailure failure = Failure("Synthetic.Checkout.Totals", error: formula, title: formula, priority: -2,
            owner: new AdoIdentityRef { DisplayName = formula },
            history: History((398, AdoTestHistoryOutcome.Passed), (399, AdoTestHistoryOutcome.Failed), (400, AdoTestHistoryOutcome.Failed), (401, AdoTestHistoryOutcome.Failed)),
            testCase: new AdoTestCaseLink { Id = 902, State = formula, IsResolved = true, WebUrl = new Uri(Collection, "Synthetic%20Web/_workitems/edit/902") },
            bugs: [Bug(2001, formula, isOpen: true)]);
        string[] row = Cells(Render(Model(Set(failure))))[1];
        foreach (int text in new[] { 3, 6, 7, 10, 13, 16 }) Assert.Equal("'" + formula, row[text]);
        Assert.Equal("-2", row[8]);
        Assert.Equal("902", row[9]);
        Assert.Equal("1", row[11]);
        Assert.Equal("2001", row[12]);
        Assert.Equal("False", row[14]);
        Assert.Equal("2026-09-14", row[15]);
    }

    // Bug IDs and states follow the bug list, which is ordered by ID; a bug that could not be read
    // keeps its place with an empty state and does not count as open.
    [Fact]
    public void JoinsSeveralBugsInListOrder()
    {
        AdoTestFailure failure = Failure("Synthetic.Checkout.Totals", bugs:
        [
            Bug(2003, "Active", isOpen: true), Bug(2001, "Resolved", isOpen: true), Bug(2002, null, isOpen: null), Bug(2004, "New", isOpen: true),
        ]);
        string[] row = Cells(Render(Model(Set(failure))))[1];
        Assert.Equal("3", row[11]);
        Assert.Equal("2001; 2002; 2003; 2004", row[12]);
        Assert.Equal("Resolved; ; Active; New", row[13]);
    }

    // Server text stays data: quotes are doubled inside the quoted field, a formula is neutralized,
    // markup, accents and other scripts pass unchanged, and the file still reads back.
    [Fact]
    public void HostileRemoteTextStaysData()
    {
        const string name = "=HYPERLINK(\"https://evil.example.test/\",\"Totals\")";
        const string owner = "@Robert \"Bobby\", Tables";
        AdoTestFailure failure = Failure(name, title: TestFailureReportFixture.Hostile, error: "-cmd|' /C calc'!A0",
            owner: new AdoIdentityRef { DisplayName = owner, UniqueName = "robert@ado.example.test" }, bugs: [Bug(2001, "+Active,\"x\"", isOpen: true)]);
        TestFailureReportModel model = Model(Set(failure));
        string csv = Render(model);
        Assert.Contains(",\"'=HYPERLINK(\"\"https://evil.example.test/\"\",\"\"Totals\"\")\",", csv, StringComparison.Ordinal);
        string[] row = Cells(csv)[1];
        Assert.Equal("'" + name, row[2]);
        Assert.Equal(TestFailureReportFixture.Hostile, row[3]);
        Assert.Equal("'-cmd|' /C calc'!A0", row[6]);
        Assert.Equal("'" + owner + " <robert@ado.example.test>", row[7]);
        Assert.Equal("'+Active,\"x\"", row[13]);
        using TestDirectory directory = new();
        CsvTestFailureRenderer.Validate(Write(directory, model), model);
    }

    // A lone surrogate half cannot be written as UTF-8: it becomes U+FFFD and the export does not fail.
    [Fact]
    public void ReplacesALoneSurrogateHalf()
    {
        TestFailureReportModel model = Model(Set(Failure("Synthetic.Checkout.Totals", title: "a\uD800b 😀 c\uDC00")));
        using TestDirectory directory = new();
        string path = Write(directory, model);
        CsvTestFailureRenderer.Validate(path, model);
        Assert.Equal("a�b 😀 c�", Cells(File.ReadAllText(path, Encoding.UTF8))[1][3]);
    }

    // The report's table cuts the first line at 240 characters; the file keeps every character.
    [Fact]
    public void KeepsAnErrorOfMoreThanOneMebibyteWhole()
    {
        StringBuilder builder = new();
        for (int line = 0; builder.Length <= 1_100_000; line++)
            builder.Append("Line ").Append(line.ToString(CultureInfo.InvariantCulture)).Append(", \"value\" ").Append('x', 200).Append("\r\n");
        string error = builder.ToString();
        TestFailureReportModel model = Model(Set(Failure("Synthetic.Checkout.Totals", error: error)));
        using TestDirectory directory = new();
        string path = Write(directory, model);
        CsvTestFailureRenderer.Validate(path, model);
        Assert.True(new FileInfo(path).Length > 1024 * 1024);
        Assert.Equal(error, Cells(File.ReadAllText(path, Encoding.UTF8))[1][6]);
    }

    // New and Since are the report's trend: New when the build before passed; otherwise the day the
    // first build of the run of failures finished, in the export's offset, as the report shows it,
    // or that build's number when the day is not known; nothing without a comparison.
    [Fact]
    public void TrendCellsAreTheReportTrend()
    {
        TestFailureReportModel model = Model(Set(
            Failure("Synthetic.A.New", history: History((400, AdoTestHistoryOutcome.Passed), (401, AdoTestHistoryOutcome.Failed))),
            Failure("Synthetic.B.SinceDay", history: History((398, AdoTestHistoryOutcome.Passed), (399, AdoTestHistoryOutcome.Failed),
                (400, AdoTestHistoryOutcome.Flaky), (401, AdoTestHistoryOutcome.Failed))),
            Failure("Synthetic.C.SinceBuild", history: History((399, AdoTestHistoryOutcome.Passed), (400, AdoTestHistoryOutcome.Failed), (401, AdoTestHistoryOutcome.Failed))),
            Failure("Synthetic.D.NoComparison", history: History((401, AdoTestHistoryOutcome.Failed))),
            Failure("Synthetic.E.NotRunBefore", history: History((400, AdoTestHistoryOutcome.NotRun), (401, AdoTestHistoryOutcome.Failed)))));
        List<string[]> rows = Cells(Render(model));
        Assert.Equal(["True", ""], rows[1][14..16]);
        Assert.Equal(["False", "2026-09-14"], rows[2][14..16]);
        Assert.Equal(["False", "20260915.2"], rows[3][14..16]);
        Assert.Equal(["", ""], rows[4][14..16]);
        Assert.Equal(["", ""], rows[5][14..16]);
        // The report says Since and the same day, in its own culture.
        string html = TestFailureReportFixture.Render(model);
        Assert.Contains(SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportSince, CultureInfo.GetCultureInfo("en-US"),
            new DateTime(2026, 9, 14).ToString("d", CultureInfo.GetCultureInfo("en-US")))), html, StringComparison.Ordinal);
    }

    // English names and invariant values, whatever the report culture or the process culture.
    [Fact]
    public void SameTextInEveryCulture()
    {
        AdoTestFailure failure = Failure("Synthetic.Checkout.Totals", title: "Total « TTC »", priority: 1234567,
            history: History((398, AdoTestHistoryOutcome.Passed), (399, AdoTestHistoryOutcome.Failed), (400, AdoTestHistoryOutcome.Failed), (401, AdoTestHistoryOutcome.Failed)));
        CultureInfo previous = CultureInfo.CurrentCulture;
        List<string> outputs = [];
        try
        {
            foreach (string process in new[] { "en-US", "fr-CA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(process);
                foreach (string report in new[] { "en-US", "fr-CA" }) outputs.Add(Render(Model(Set(failure), report)));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Assert.All(outputs, output => Assert.Equal(outputs[0], output));
        string[] row = Cells(outputs[0])[1];
        Assert.Equal(["1234567", "False", "2026-09-14"], new[] { row[8], row[14], row[15] });
        Assert.StartsWith("﻿" + Header + "\r\n", outputs[0], StringComparison.Ordinal);
    }

    // The check of the written file refuses one without its byte order mark, with a record missing
    // or out of order, or with broken quoting.
    [Theory]
    [InlineData("bom")]
    [InlineData("record")]
    [InlineData("order")]
    [InlineData("quote")]
    [InlineData("width")]
    [InlineData("crlf")]
    public void ValidationRejectsAFileThatDoesNotMatchTheModel(string damage)
    {
        TestFailureReportModel model = Model(Set(Failure("Synthetic.Checkout.Taxes", title: "Taxes, \"net\""), Failure("Synthetic.Checkout.Totals")));
        using TestDirectory directory = new();
        string path = Write(directory, model);
        CsvTestFailureRenderer.Validate(path, model);
        // Read raw, so the byte order mark stays the first character.
        string text = Encoding.UTF8.GetString(File.ReadAllBytes(path));
        string[] records = text.Split("\r\n");
        string damaged = damage switch
        {
            "bom" => text[1..],
            "record" => string.Join("\r\n", records[0], records[1], records[3]),
            "order" => string.Join("\r\n", records[0], records[2], records[1], records[3]),
            "quote" => text.Replace("\"\"net\"\"", "\"net\"", StringComparison.Ordinal),
            "width" => text.Replace("\r\n401,2,", "\r\n401,2,,", StringComparison.Ordinal),
            _ => text.Replace("\r\n", "\n", StringComparison.Ordinal),
        };
        Assert.NotEqual(text, damaged);
        File.WriteAllText(path, damaged, new UTF8Encoding(false));
        Assert.Throws<InvalidDataException>(() => CsvTestFailureRenderer.Validate(path, model));
    }

    private static string Render(TestFailureReportModel model)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        CsvTestFailureRenderer.Render(model, writer);
        return writer.ToString();
    }

    private static string Write(TestDirectory directory, TestFailureReportModel model)
    {
        string path = Path.Combine(directory.Root, "Build-401-TestFailures.csv");
        using (StreamWriter writer = new(path, false, new UTF8Encoding(false, true))) CsvTestFailureRenderer.Render(model, writer);
        return path;
    }

    // An RFC 4180 reader of its own, so the tests do not trust the renderer's: the records of a
    // rendered file, after its byte order mark.
    private static List<string[]> Cells(string csv)
    {
        List<string[]> records = [];
        List<string> fields = [];
        StringBuilder field = new();
        bool quoted = false;
        for (int index = csv.StartsWith('﻿') ? 1 : 0; index < csv.Length; index++)
        {
            char character = csv[index];
            if (quoted)
            {
                if (character != '"') field.Append(character);
                else if (index + 1 < csv.Length && csv[index + 1] == '"') { field.Append('"'); index++; }
                else quoted = false;
            }
            else if (character == '"') quoted = true;
            else if (character == ',') { fields.Add(field.ToString()); field.Clear(); }
            else if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                records.Add([.. fields]);
                fields.Clear();
                index++;
            }
            else field.Append(character);
        }
        Assert.False(quoted);
        Assert.Empty(fields);
        Assert.All(records, static record => Assert.Equal(20, record.Length));
        return records;
    }

    private static TestFailureReportModel Model(AdoBuildTestFailureSet set, string culture = "en-US", IReadOnlyList<ErrorRuleOptions>? rules = null) =>
        TestFailureReportModelBuilder.Build(set, new TestFailureReportOptions
        {
            Culture = culture, SessionCulture = CultureInfo.GetCultureInfo("en-US"), GeneratedAt = Clock, ToolkitVersion = "5.4.0-test", IncludeFlaky = true,
            ErrorRules = rules ?? [],
        });

    private static AdoBuildTestFailureSet Set(params AdoTestFailure[] failures)
    {
        AdoBuildTestSummary current = Summary(401, "20260916.3", Clock.AddMinutes(-12));
        return new AdoBuildTestFailureSet
        {
            Build = new AdoBuild
            {
                Id = 401, BuildNumber = "20260916.3", Definition = new AdoBuildDefinitionRef { Id = 12, Name = "Synthetic UI tests" },
                QueueTime = Clock.AddMinutes(-30), FinishTime = Clock.AddMinutes(-12), TeamProject = Project, CollectionUri = Collection,
                WebUrl = new Uri(Collection, "Synthetic%20Web/_build/results?buildId=401"),
            },
            Summary = current,
            // 399 finished at 02:00 UTC on 15 September, the 14th in the export's offset; 400 has no finish time.
            History = [Summary(398, "20260913.1", new DateTimeOffset(2026, 9, 13, 22, 0, 0, TimeSpan.Zero)),
                Summary(399, "20260915.1", new DateTimeOffset(2026, 9, 15, 2, 0, 0, TimeSpan.Zero)), Summary(400, "20260915.2", null), current],
            Failures = failures, FailedCount = failures.Length, RetrievedAt = Clock, CollectionUri = Collection,
        };
    }

    private static AdoBuildTestSummary Summary(int id, string number, DateTimeOffset? finished) => new()
    {
        BuildId = id, BuildNumber = number, FinishTime = finished, IsAvailable = true, IsCurrent = id == 401,
        WebUrl = new Uri(Collection, "Synthetic%20Web/_build/results?buildId=" + id.ToString(CultureInfo.InvariantCulture)),
    };

    private static AdoTestHistoryEntry[] History(params (int Build, AdoTestHistoryOutcome Outcome)[] cells) => [.. cells.Select(cell => new AdoTestHistoryEntry
    {
        BuildId = cell.Build, BuildNumber = cell.Build switch { 398 => "20260913.1", 399 => "20260915.1", 400 => "20260915.2", _ => "20260916.3" },
        Outcome = cell.Outcome, IsCurrent = cell.Build == 401,
        WebUrl = new Uri(Collection, "Synthetic%20Web/_build/results?buildId=" + cell.Build.ToString(CultureInfo.InvariantCulture)),
    })];

    private static AdoTestBug Bug(int id, string? state, bool? isOpen) => new()
    {
        Id = id, Title = "Synthetic bug " + id.ToString(CultureInfo.InvariantCulture), State = state, IsOpen = isOpen, TeamProject = Project,
        WebUrl = new Uri(Collection, "Synthetic%20Web/_workitems/edit/" + id.ToString(CultureInfo.InvariantCulture)),
    };

    // One failed attempt with error, or one per message of errors.
    private static AdoTestFailure Failure(string name, string? error = "Expected 1, actual 2", string? title = null, int? priority = null,
        AdoIdentityRef? owner = null, AdoTestHistoryEntry[]? history = null, AdoTestCaseLink? testCase = null, AdoTestBug[]? bugs = null, string?[]? errors = null) => new()
    {
        Ordinal = 1, Classification = AdoTestFailureClassification.Failed, TestName = name, ShortName = name[(name.LastIndexOf('.') + 1)..],
        Title = title, Priority = priority, Owner = owner, History = history ?? [], TestCase = testCase, Bugs = bugs ?? [], CollectionUri = Collection,
        Attempts = [.. (errors ?? [error]).Select((message, index) => new AdoTestAttempt
        {
            Number = index + 1, RunId = 201, ResultId = 11 + index, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = message,
        })],
    };
}
