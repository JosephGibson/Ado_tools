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
  zero, an empty default branch, a default test plan ID of zero or an unsupported
  authentication value.
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
    "culture": "fr-CA"
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

Every request accepts gzip and deflate, so a server that compresses its responses sends fewer
bytes; the attachment limits above count the decoded bytes. A JSON response may hold at most
256 MB once decoded, a fixed limit: a larger one fails with an `AdoResponseFormat` error that
names the operation.

### Reporting

| Key | Default | Meaning | Parameter |
| --- | --- | --- | --- |
| `reporting.culture` | session UI culture | Report language, for example `en-US` or `fr-CA`. Cultures other than English or French fall back to English with a warning | `-Culture` |

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
