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
- Edit other sections by hand. Settings are read each time a cmdlet runs, and a
  profile is read when `Connect-Ado` runs.
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

Omitted settings use their defaults. Every limit must be a positive whole number.
Where a cmdlet parameter exists, it overrides the setting for that call.

### Profiles

| Key | Default | Meaning |
| --- | --- | --- |
| `defaultProfile` | none | Profile used by `Connect-Ado` without `-Profile` or `-CollectionUrl`, and by any command that runs before a connection exists; set with `Set-AdoProfile -DefaultProfile` |
| `profiles.<name>.collectionUrl` | required | Azure DevOps Server collection URL |
| `profiles.<name>.defaultProject` | none | Project used when `-Project` is omitted; `Connect-Ado -Project` overrides it |
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
| `testCases.maximumExpandedSteps` | `5000` | Most expanded rows per Test Case | `-MaximumExpandedSteps` |
| `testCases.maximumResolvedWorkItems` | `10000` | Most additional work items (Shared Steps and shared parameter sets) retrieved per `Get-AdoTestCase` call | none |

### Test results

| Key | Default | Meaning | Parameter |
| --- | --- | --- | --- |
| `testResults.historyCount` | `10` | Builds in the run history, including the current one; values above 50 are treated as 50 | `-HistoryCount` |
| `testResults.historyScope` | `SameBranch` | `SameBranch` or `AllBranches`; other values mean `SameBranch` | `-HistoryScope` |
| `testResults.maximumReportedFailures` | `1000` | Most failing tests detailed per build; the rest are counted and the result is `Partial` | none |
| `testResults.maximumHistoryRequests` | `400` | Most requests spent on run history; older entries show as unavailable | none |
| `testResults.maximumAttachmentBytes` | `52428800` (50 MiB) | Largest attachment that is downloaded | none |
| `testResults.maximumTotalAttachmentBytes` | `524288000` (500 MiB) | Total attachment download size per report | none |
| `testResults.maximumInlineJsonBytes` | `262144` (256 KiB) | Largest JSON or text attachment shown inline, and so searchable, in the failed-test report | none |
| `testResults.maximumInlineTotalBytes` | `8388608` (8 MiB) | Total JSON and text shown inline per failed-test report; later attachments are linked only | none |

### Reporting

| Key | Default | Meaning | Parameter |
| --- | --- | --- | --- |
| `reporting.culture` | session UI culture | Report language, for example `en-US` or `fr-CA`. Cultures other than English or French fall back to English with a warning | `-Culture` |

Cmdlet messages and help always follow the session UI culture (`$PSUICulture`).
