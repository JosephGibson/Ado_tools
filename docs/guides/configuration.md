# Configuration

AdoToolkit keeps connection profiles and optional limits in one local JSON file.
It stores no passwords or tokens.

## Location

The default location is `%APPDATA%\AdoToolkit\config.json`. Set the
`ADOTOOLKIT_CONFIG_PATH` environment variable to use a different file:

```powershell
$env:ADOTOOLKIT_CONFIG_PATH = 'D:\ado\config.json'
```

The file is created the first time you save a profile. Until then, the built-in
defaults apply.

## Editing

- Manage profiles with `Set-AdoProfile` and `Remove-AdoProfile`, as described in
  [Getting started](getting-started.md#save-a-profile). These cmdlets keep all other
  settings, and they validate the file before replacing it atomically.
- Edit other sections by hand. Retrieval and export commands reread their settings
  when they need them. Connections keep a copy of their profile defaults; run
  `Connect-Ado` again after changing a profile.
- If the file is invalid, commands that read it fail with a configuration error.
  Examples are malformed JSON, a property named twice in the same object, a limit of
  zero, an empty default branch, a default test plan ID of zero, an unsupported
  authentication value or an error rule without a pattern.
- Unknown keys produce a warning and are kept when the file is saved.
- A file with a newer `schemaVersion` than this version supports can be read but
  not saved.

## Example

```json
{
  "schemaVersion": 1,
  "defaultProfile": "work",
  "profiles": {
    "work": {
      "collectionUrl": "https://ado.example.test/tfs/DefaultCollection",
      "defaultProject": "Web",
      "defaultBranch": "develop",
      "defaultBuildDefinition": "Test_Plan",
      "defaultTestPlanId": 812,
      "defaultTestSuiteId": 813,
      "authentication": "WindowsIntegrated",
      "requestTimeoutSeconds": 100
    }
  },
  "testCases": {
    "maximumSharedStepDepth": 10
  },
  "testResults": {
    "historyCount": 20,
    "historyScope": "AllBranches"
  },
  "reporting": {
    "culture": "fr-CA",
    "errorRules": [
      {
        "name": "Home page did not load",
        "patterns": [
          "Home page did not load within * seconds",
          "La page d'accueil ne s'est pas chargée en moins de * secondes"
        ]
      }
    ]
  }
}
```

## Settings

Omitted settings use their defaults. Every limit must be a positive whole number, and a
value outside a stated range is a configuration error.
Where a cmdlet parameter exists, it overrides the setting for that call.

### Profiles

| Key | Default | Meaning |
| --- | --- | --- |
| `defaultProfile` | none | Profile used by `Connect-Ado` without `-Profile` or `-CollectionUrl`, and by any command that runs before a connection exists; set with `Set-AdoProfile -DefaultProfile` |
| `profiles.<name>.collectionUrl` | required | Azure DevOps Server collection URL |
| `profiles.<name>.defaultProject` | none | Project used when `-Project` is omitted; `Connect-Ado -Project` overrides it. `Set-AdoProfile -DefaultProject ''` removes it |
| `profiles.<name>.defaultBranch` | none | Branch used by `Get-AdoBuild` and `Get-AdoBuildTestFailure` when `-Branch` is omitted. `develop` becomes `refs/heads/develop`; full `refs/...` names are used as given. Without it, builds of every branch are returned |
| `profiles.<name>.defaultBuildDefinition` | none | Build definition used by `Get-AdoBuild` and `Get-AdoBuildTestFailure` when `-Definition` is omitted. A number is a definition ID and a string an exact definition name, as for `-Definition`. Without it, `-Definition` is required |
| `profiles.<name>.defaultTestPlanId` | none | Test plan ID used by `Get-AdoTestCase` and `Get-AdoTestSuite` when `-PlanId` is omitted. Without it, `-PlanId` is required |
| `profiles.<name>.defaultTestSuiteId` | none | Test suite ID used by `Get-AdoTestCase` and `Get-AdoTestSuite` when both `-PlanId` and `-SuiteId` are omitted. It belongs to the profile's test plan, so it is not used with an explicit `-PlanId` or a piped plan. Without it, `Get-AdoTestCase` requires `-SuiteId` and `Get-AdoTestSuite` starts from the plan's root suite |
| `profiles.<name>.authentication` | `WindowsIntegrated` | The only supported value |
| `profiles.<name>.requestTimeoutSeconds` | `100` | Timeout for each API request, 1–86400. Log and attachment downloads use fixed limits instead: 10 minutes in total and 60 seconds without data |

The four `default…` values save you from passing the same build and test plan
parameters to every command. An explicit parameter always wins; the profile value
applies only when the parameter is omitted. The branch and build definition must not be
empty, and the IDs must be positive whole numbers. Set or remove them with
`Set-AdoProfile`, for example
`Set-AdoProfile -Name work -DefaultBranch develop -DefaultBuildDefinition Test_Plan`, and
remove one with `$null`, or with an empty string for the branch and build definition.
Commands take them from the connection,
which copies them from the profile when it connects, so run `Connect-Ado` again after
changing them. A connection made with `Connect-Ado -CollectionUrl` has no defaults.

### Test Cases

| Key | Default | Meaning | Parameter |
| --- | --- | --- | --- |
| `testCases.maximumSharedStepDepth` | `10` | Deepest Shared Steps nesting that is expanded | `-MaximumSharedStepDepth` |
| `testCases.maximumExpandedSteps` | `5000` | Expansion threshold per Test Case; the row crossing it is kept, then a `Truncated` marker ends expansion | `-MaximumExpandedSteps` |
| `testCases.maximumResolvedWorkItems` | `10000` | Most additional work items (Shared Steps and shared parameter sets) retrieved per `Get-AdoTestCase` call | none |

### Test results

| Key | Default | Meaning | Parameter |
| --- | --- | --- | --- |
| `testResults.historyCount` | `10` | Builds in the run history, including the current one; values above 50 are treated as 50 | `-HistoryCount` |
| `testResults.historyScope` | `SameBranch` | `SameBranch` or `AllBranches`; other values mean `SameBranch` | `-HistoryScope` |
| `testResults.maximumReportedFailures` | `1000` | Most failing tests detailed per build; the rest are counted and the result is `Partial` | none |
| `testResults.maximumHistoryRequests` | `500` | Most requests spent on run history; older entries show as unavailable. Earlier builds are read newest first, one at a time, while the result pages of newer builds are read. A build's run list counts as it is sent; its result pages are then counted from the `totalTests` of its runs and set aside at once, so a build whose pages do not fit shows as unavailable, with every older build, before any of its pages is requested. Requests set aside are never given back, even when a page fails, so the builds kept do not depend on response order. The pages of a run without `totalTests` or not completed, retries, and pages beyond `totalTests` take from what is left when they are sent; near the limit, retries and such extra pages can still depend on response order | none |
| `testResults.maximumAttachmentBytes` | `52428800` (50 MiB) | Largest attachment that is downloaded | none |
| `testResults.maximumTotalAttachmentBytes` | `524288000` (500 MiB) | Total attachment download size per report | none |
| `testResults.maximumInlineJsonBytes` | `262144` (256 KiB) | Largest JSON or text attachment shown inline, and so searchable, in the failed-test report. Also the largest one that the export downloads from test runs other than the most recent | none |
| `testResults.maximumInlineTotalBytes` | `8388608` (8 MiB) | Total JSON and text shown inline per failed-test report; attachments that no longer fit are linked only, those of older runs first | none |
| `testResults.maximumConcurrentRequests` | `6` | Requests that `Get-AdoBuildTestFailure` has in progress at the same time, and attachments that `Export-AdoBuildTestFailure` downloads at the same time, 1–16. With `1`, every request waits for the previous one. Results are combined in input order, and the history builds that fit `testResults.maximumHistoryRequests` do not depend on this value, apart from the retries and extra pages that row names. Lower the value if the server handles concurrent requests poorly; Server 2020 concurrency is not confirmed at work (V-33) | none |

Every request to Azure DevOps accepts gzip and deflate, so a server that compresses its responses sends fewer
bytes; the attachment limits above count the decoded bytes. A JSON response may hold at most
256 MB once decoded, a fixed limit: a larger one fails with an `AdoResponseFormat` error that
names the operation. So does a response marked compressed whose body does not decompress, which
is not retried; when the server answered with an error status, that status decides the error
instead, as it does for any error body that cannot be read.

### Reporting

| Key | Default | Meaning | Parameter |
| --- | --- | --- | --- |
| `reporting.culture` | session UI culture | Report language, for example `en-US` or `fr-CA`. Cultures other than English or French fall back to English with a warning | `-Culture` |
| `reporting.errorRules` | none | Rules that name errors of the failed-test report, merge their wordings and mark the ones that come from the environment as generic; see [Error rules](#error-rules) | none |

Cmdlet messages and help always follow the session UI culture (`$PSUICulture`).

Full help: [Set-AdoProfile](../commands/en-US/Set-AdoProfile.md),
[Get-AdoProfile](../commands/en-US/Get-AdoProfile.md),
[Remove-AdoProfile](../commands/en-US/Remove-AdoProfile.md),
[Connect-Ado](../commands/en-US/Connect-Ado.md).
The cmdlets that read the defaults and limits above:
[Get-AdoTestCase](../commands/en-US/Get-AdoTestCase.md),
[Get-AdoTestSuite](../commands/en-US/Get-AdoTestSuite.md),
[Get-AdoBuild](../commands/en-US/Get-AdoBuild.md),
[Get-AdoBuildTestFailure](../commands/en-US/Get-AdoBuildTestFailure.md),
[Export-AdoBuildTestFailure](../commands/en-US/Export-AdoBuildTestFailure.md).

### Error rules

The HTML report of `Export-AdoBuildTestFailure` groups the failed attempts of each test by
error, and gives each test a primary error, as [Reading the report](build-report.md#reading-the-report)
describes; its CSV file names that error in the columns `Primary error`, `Error kind`,
`Error rule` and `Distinct errors`. The report recognizes the messages of MSTest, NUnit and
xUnit, the wait timeout of Selenium, and the timeouts and assertions of Playwright, in English
and, for MSTest, in French. Error rules cover the rest:

- they give an error a name, shown in the report;
- they merge the wordings of one error, such as an English and a French message, or
  messages that differ only by a value;
- they mark the errors that come from the environment, such as a page or a server that did
  not respond, as generic. The report lists generic errors after the others, and a test's
  primary error is generic only when the test had no other error.

```json
"reporting": {
  "errorRules": [
    {
      "name": "Home page did not load",
      "patterns": [
        "Home page did not load within * seconds",
        "La page d'accueil ne s'est pas chargée en moins de * secondes"
      ]
    },
    {
      "name": "Login banner text",
      "generic": false,
      "patterns": [ "*Expected:<Welcome*" ]
    }
  ]
}
```

| Key | Meaning |
| --- | --- |
| `name` | The name the report shows, 1–100 characters; spaces around it are dropped. Two rules cannot have the same name, ignoring case |
| `patterns` | 1–20 patterns, each a string of 1–500 characters that is not only spaces. The rule matches an error when one of its patterns does |
| `generic` | `true`, the default, marks the error as generic. With `false`, the rule only merges and names the errors it matches |

How a pattern matches:

- A pattern matches a whole line. `*` stands for any text, including none, and `?` for
  exactly one character; no other character is special. To find text inside a line, start
  and end the pattern with `*`, as in `*Expected:<Welcome*`.
- Case, accents and typographic apostrophes do not count, and a run of spaces, tabs or
  no-break spaces counts as one space: `chargee` matches `chargée`, and `'` matches `’`.
  Spaces at both ends of a line are dropped.
- A pattern is tried on the line that names the error, below a wrapper line such as MSTest's
  "Test method … threw exception:", on the first line of the message, and on each inner
  exception line without its leading `--->`. Each of these lines is tried as it is and without
  its leading exception type, such as `System.TimeoutException: `. Only the first 20 lines of
  a message are read.
- Rules are tried in the order of the file, then the built-in rules. The first rule that
  matches wins, before the recognized test framework messages: every error it matches becomes
  one error in the report, under its name. A configured rule makes an error of its own, even
  when its texts mean what a built-in rule means: to bring other wordings into a built-in
  rule's error, copy that rule's texts into the configured rule, which then wins.
- When the English and French wordings of one error are joined, as [Reading the
  report](build-report.md#english-and-french-attempts) describes, and two rules named them, the
  rule that comes first names the error: a configured rule before a built-in one.
- Patterns are not regular expressions. Matching takes time in proportion to the length of
  the lines, whatever the patterns.

The built-in rules are all generic. They recognize the English messages of Windows, .NET,
Chrome, chromedriver, Selenium and Playwright; the CSV file names them by their ID.

| ID | Name in the report | Texts recognized |
| --- | --- | --- |
| `ConnectionRefused` | **Connection refused** | "No connection could be made because the target machine actively refused it", `net::ERR_CONNECTION_REFUSED`, "Unable to connect to the remote server" |
| `NameResolution` | **Host name not resolved** | "No such host is known", "The remote name could not be resolved", `net::ERR_NAME_NOT_RESOLVED` |
| `ServerUnavailable` | **Server unavailable** | The status 502, 503 or 504, as `: 503 (` or `(503)`, on a line that names `HttpRequestException` or `WebException` or says "Response status code does not indicate success" or "The remote server returned an error" |
| `WebDriverSession` | **WebDriver session failed** | A line that starts with "session not created", "invalid session id" or "no such window"; "chrome not reachable"; "disconnected: not connected to DevTools"; "The HTTP request to the remote WebDriver server for URL … timed out after … seconds"; "Timed out waiting for driver service to initialize after" |
| `PageLoadTimeout` | **Page load timed out** | A line that starts with "timeout: Timed out receiving message from renderer" |
| `PlaywrightTimeout` | **Playwright timeout** | A line that is "Timeout …ms exceeded.", Playwright's action timeout, whatever element or page the action waited for; not "Timeout …ms exceeded while waiting for event …", a wait for an event such as a download |

Each text is found anywhere in a line unless the table says that the line starts with it or is
it. The French messages of Windows and .NET Framework (V-41), the texts of chromedriver (V-42)
and the Playwright forms on the agents (V-43) are not confirmed at work.

A configured rule with the pattern `Timeout *ms exceeded.` and `"generic": false` keeps
Playwright's timeouts among the specific errors, under its own name, since a configured rule
comes first.

Two failures that can come from the environment are left to a configured rule, because the
report cannot tell when they do: a browser that closed or crashed, which a test that closes its
own page causes too, and a deadlock in the database that the tests use, whose wording depends on
the database. Rules for them could read as follows; the deadlock text is SQL Server's, so adapt
it to your database and its language:

```json
"reporting": {
  "errorRules": [
    {
      "name": "Browser closed",
      "patterns": [ "Target page, context or browser has been closed*", "Target crashed*" ]
    },
    {
      "name": "Database deadlock",
      "patterns": [ "*chosen as the deadlock victim*" ]
    }
  ]
}
```

A file holds at most 100 rules. A rule that breaks a limit, or a value of the wrong type,
is a configuration error that names its place, such as `reporting.errorRules[2].patterns[0]`.
Like any invalid setting, it stops every command that reads the file until it is fixed. A
key that a rule does not know produces a warning and is kept when the file is saved.
`Get-AdoBuildTestFailure` and its table do not use the rules: they show each test's latest
error.
