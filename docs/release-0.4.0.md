# AdoToolkit 0.4.0 release notes

Prepared on 2026-09-21 from the pending changes in `src/`, `tests/`, `tools/`, `docs/` and
`.github/workflows/`. This is an offline review. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed live. The areas
the README lists as confirmed are still connections, projects, builds and test runs.

## What changed

### Bugs of failed tests

`Get-AdoBuildTestFailure` now lists the bugs of every reported test, and
`Export-AdoBuildTestFailure` shows them.

| Area | 0.4.0 behavior |
| --- | --- |
| Which bugs | Every work item associated with one of the test's results, and every work item linked to its Test Case, by any work item link type, whose type is in the project's Bug category (`Microsoft.BugCategory`). Hyperlinks, artifact links and attachments are never followed |
| Output | `AdoTestFailure.Bugs`, ordered by ID, and `HasOpenBug`. Each `AdoTestBug` has `Id`, `Title`, `State`, `WorkItemType`, `TeamProject`, `StateCategory`, `IsOpen`, `IsResolved`, `IsAssociatedWithResult`, `IsLinkedToTestCase` and `WebUrl`. The failure table view adds `HasOpenBug`, and bugs print as a table of `Id`, `IsOpen`, `State`, `WorkItemType` and `Title` |
| Open or closed | A bug is open unless its state is in the Completed or Removed state category of its project and type, so a `Resolved` bug is still open. Categories come from `workitemtypes/{type}/states` and are read once per project and type |
| Requests | The Test Case read asks for relations instead of five fields. All bug candidates of all reported tests are read together, 200 per request. The Bug category is read once per project, only when a Test Case has work item links, and the state list once per project and bug type. Nothing is requested per test |
| Degraded lookups | `BugMetadataUnavailable`: only the type named `Bug` counts, and `Closed`, `Done` and `Removed` count as closed. `UnresolvedBug`: a bug of a test result could not be read and keeps its ID link. `BugLookupFailed`: no bug could be read; result bug IDs stay links and linked work items are left out. All three are warnings, so `Status` stays `Complete`; only authentication, authorization and cancellation stop the retrieval |
| Report | Overview and By error rows mark a test with an open bug. Each card lists its bugs with ID link, title, state and an Open mark, instead of the bug links that were in the card header. Search matches bug titles and states. **Without an open bug**, shown when at least one test has an open bug, leaves only the tests that no open bug tracks yet |

### Other changes

| Area | Change |
| --- | --- |
| Report times | Every time in the failed-test report is shown in the offset of the export, like its generation time. Server times were shown in UTC beside the local generation time, both without a zone |
| `Export-AdoTestCase -Open` | Opens only `.html`, `.htm`, `.md`, `.markdown`, `.json` and `.txt` files. Any other name is still written, with a warning that it was not opened |
| Project names | A project named `.` or `..` is a configuration error before any request. Project, type and route values that are empty or dot segments are refused by the request builder, and an unusable `System.TeamProject` from the server falls back to the build's project |
| Configuration | A property named twice in the same object is a configuration error |
| Completion | Profile and project names are quoted with PowerShell's own escaping, so a name with a typographic apostrophe (’) completes, and is suggested by `Test-AdoConnection`, as a command that runs |
| Profiles | A blank `-Name` for `Set-AdoProfile` or `Remove-AdoProfile` is a parameter validation error |
| Release workflow | The PowerShell runtime must match both its published `hashes.sha256` and the SHA-256 pinned in the workflow. Checkout no longer stores the write-scoped token in the Git configuration |
| Live checks | `TestFailures.Live.ps1` adds V-30 for the bug lookup and prints only counts |
| Portable launcher | The French greeting reads `est prêt.` |

### Visible changes for 0.3.0 users

- Each reported test costs no extra request, but a retrieval adds one bug read per 200 bug
  candidates, one Bug category read per project when Test Cases have links, and one state
  list per project and bug type. The Test Case read now returns every field with its
  relations, so its response is larger.
- `Get-AdoBuildTestFailure` output shows a `HasOpenBug` column.
- In a report card, the bug links moved from the header to the new bug list.
- Report times shift from UTC to the time zone of the computer that exports the report.
- `Export-AdoTestCase -Open` no longer opens a report saved with another extension.
- A configuration file with a repeated property, a blank profile name and a project named
  `.` or `..` now fail with configuration or parameter errors instead of .NET exceptions or
  requests outside the collection.

## Security fixes

| Area | Finding | Fix | Regression test | Files |
| --- | --- | --- | --- | --- |
| HTTP requests | A route value of `.` or `..` escaped the collection, because `Uri` collapses dot segments even after escaping. `-Project '..'`, or a server-supplied `System.TeamProject` or type name, sent Windows-authenticated requests to the server level, for example `/tfs/_apis` | The request builder refuses empty, blank, `.` and `..` route values; cmdlets refuse such projects with a configuration error; server-supplied projects and types that cannot name a route fall back to the build's project or skip the state lookup | `HttpPipelineContractTests.RouteValuesThatAreEmptyOrDotSegmentsAreRejectedBeforeAnyRequest`; `TestBugResolutionTests.UnusableBugProjectFallsBackToTheBuildProject`; `Projects.Pester.ps1` "rejects the dot-segment project" | `Http/RequestBuilder.cs`, `TestRuns/TestBugResolver.cs`, `TestManagement/TestWorkItem.cs`, `TestCaseService.cs`, `AdoCmdletBase.cs` |
| `Export-AdoTestCase -Open` | The written file was handed to the shell whatever its extension. A report saved as `.cmd`, `.bat`, `.js`, `.vbs` or `.hta` would run, with text that anyone who can edit a Test Case controls | Only document extensions are opened; other names get a warning. `ShellDocumentLauncher` refuses them itself | `TestCaseExporterTests.OpenLaunchesOnlyDocumentExtensions` (no process is started) | `IO/ShellDocumentLauncher.cs`, `Reporting/TestCaseExporter.cs` |
| Release workflow | `actions/checkout` kept the `contents: write` token in `.git/config` while PSGallery modules, NuGet packages and the test suites ran in the same job | `persist-credentials: false`; only the publishing step receives the token | `ReleaseWorkflow.Tests.ps1` "checks out without persisting the write-scoped token" | `.github/workflows/release.yml` |
| Release workflow | The bundled PowerShell runtime was checked only against `hashes.sha256` from the same release, which cannot detect a replaced upstream asset | `POWERSHELL_SHA256` pins the runtime's SHA-256; both hashes must match before extraction | `ReleaseWorkflow.Tests.ps1` "refuses a runtime that matches its published hash but not the pinned hash" | `.github/workflows/release.yml` |

The pinned value, `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860`, is the
SHA-256 in PowerShell 7.6.6's published `hashes.sha256`; the previous portable bundle's
`bundle.json` and the archive in `artifacts/runtime-download/` have the same hash.

## Bug fixes

| Area | Finding | Fix | Regression test | Files |
| --- | --- | --- | --- | --- |
| Bug lookup (unreleased work) | An empty or blank `System.TeamProject` on a bug threw `ArgumentException` while building its link and failed the whole retrieval | Such a project falls back to the build's project; `TeamProject` is empty | `TestBugResolutionTests.UnusableBugProjectFallsBackToTheBuildProject` | `TestRuns/TestBugResolver.cs` |
| Test Cases | A blank `System.TeamProject` on a Test Case or Shared Step escaped as `ArgumentException`, and `..` built a link outside the collection | A Test Case without a usable project is a format error; a Shared Step only loses its project and link | `TestCaseServiceTests.UnusableTeamProjectIsAFormatErrorForTheCaseAndDropsASharedStepLink` | `TestManagement/TestWorkItem.cs`, `TestCaseService.cs` |
| Configuration | A repeated property name surfaced as a raw `ArgumentException` from every command, and broke profile completion | Reported as `InvalidConfiguration` | `ConfigurationStoreTests.DuplicatePropertyNamesAreAConfigurationError`; `Profiles.Pester.ps1` "reports a repeated property" | `Configuration/ConfigurationStore.cs` |
| Completion | Names with ’ or ‘, which PowerShell reads as single quotes, completed into commands that do not parse; `Test-AdoConnection` suggested the same broken command | Quoting uses `CodeGeneration.EscapeSingleQuotedStringContent` | `Projects.Pester.ps1`, `Profiles.Pester.ps1` and `Usability.Pester.ps1` typographic apostrophe tests | `Completion/*.cs`, `TestAdoConnectionCommand.cs` |
| Failed-test report | Build, run, attempt and history times were UTC, shown beside the local generation time without a zone, so a build could appear to finish hours after the report was made | All times are shown in the export's offset | `ReportTimeTests.EveryTimeIsShownInTheOffsetOfTheExport` (English and French) | `HtmlTestFailureRenderer.cs`, `Charts/RunHistoryChart.cs` |
| Profiles | `-Name ' '` passed validation and failed later with a raw `ArgumentException` | `ValidateNotNullOrWhiteSpace` | `Profiles.Pester.ps1` "rejects a blank profile name" | `SetAdoProfileCommand.cs`, `RemoveAdoProfileCommand.cs` |
| Portable launcher | The French greeting read `est pret.` | `est prêt.` | `Portable.Tests.ps1` "greets the fr-CA console" | `tools/package/portable/Start-AdoToolkit.ps1` |
| Documentation | `pipeline-triage.md` still described 0.2.0 reports: PNG thumbnails, flaky tests by default and a missing half of the options. The README quick start said the report shows flaky tests | Rewritten for 0.4.0 | — | `README.md`, `docs/guides/pipeline-triage.md` |

Each regression test was run before its fix and failed: the Core tests against the
unfixed source, the Pester tests against the module staged by the pre-change `verify`, and
the tooling tests against the committed workflow and launcher.

### Existing tests changed

| Test | Change | Reason |
| --- | --- | --- |
| `ReleaseWorkflow.Tests.ps1` "checks the UTF-16LE published checksum before extraction" | Its setup now sets `POWERSHELL_SHA256` to the hash of its synthetic archive, as the workflow sets it at job level. Its assertions are unchanged | Without a pin the step now refuses the runtime. The test had relied on the published hash alone being enough, which was the weakness fixed above |

The failed-test report goldens in `tests/Fixtures/Reports/` were regenerated after the
filter and card marker were added; their times are unchanged because the fixture clock is
UTC.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| `verify` reports a `What if:` line from a product test as an advisory `project-check` warning | PowerShell writes WhatIf text straight to the host, where no redirection reaches it, and the gate scrapes the word "missing" from a test folder name in that path. It never changes the result. Removing it means renaming the folder in an existing test or filtering localized host text in the gate |
| Workflow actions are pinned by tag (`actions/checkout@v7`, `actions/setup-dotnet@v6`), not by commit | The commit SHAs could not be looked up offline. Pin them when the workflow is next updated online |
| JSON responses are read without a size cap below .NET's 2 GiB buffer limit | The collection is the user's own server, and no live data shows how large legitimate Test Case batches get; a cap that is too low would break exports |
| Test Case reports link `https://user@host` URLs, which the failed-test report shows as text | The link text shows the whole URL, and Markdown viewers autolink plain URLs anyway |
| French messages from 0.1.0 use a plain space before colons, later ones a no-break space | Cosmetic; changing them rewrites translated strings and goldens without a functional gain |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Bug routes on Server 2020 | The states route exists only as `6.0-preview.1` in the Server 2020 REST reference. Its response shape, the Bug category route and relation expansion on `workitemsbatch` are unconfirmed at work (V-30). Without the metadata, the lookup falls back with `BugMetadataUnavailable` |
| Linked bugs in other projects | A bug's own project is used for its link and state categories. A project that cannot be read degrades to the default state names |
| Pipeline names on Server 2020 | Unchanged from the previous release: stage, phase and job names are unverified (V-19) |
| Report script | By decision, the script's views, search, filters and keyboard handling have no automated test. The tests check the markup the script reads |

## Azure DevOps Server 2020 contract

| Operation | Route and version | Change |
| --- | --- | --- |
| Test Case read | `POST _apis/wit/workitemsbatch`, `6.0` | Body sends `"$expand": "relations"` instead of a five-field list, because the API is documented not to accept both (V-10). Only `rev`, `System.Title`, `System.State` and work item link URLs (`…/_apis/wit/workItems/<id>`) are read |
| Bug read | `POST _apis/wit/workitemsbatch`, `6.0` | New. `errorPolicy: omit` with `System.Id`, `System.Title`, `System.State`, `System.WorkItemType` and `System.TeamProject`; 200 IDs per request |
| Bug category | `GET {project}/_apis/wit/workitemtypecategories/Microsoft.BugCategory`, `6.0` | New use of the route already used for Test Case categories |
| State categories | `GET {project}/_apis/wit/workitemtypes/{type}/states`, `6.0-preview.1` | New. Reads `value[].name` and `value[].category` |

A two-run retrieval in the product tests went from 12 to 14 requests: one bug read and one
state list.

## Work-PC Live checks

Install the candidate 0.4.0 package on the work PC and run each script in a fresh
PowerShell process. The rules from the [0.2.0 audit](release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `TestFailures.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID` set to a build whose failed tests have bugs on their results and bugs linked to their Test Cases | `PASS V-30 BUG_ROUTES_AND_LOOKUP_AGREE`, with `LINKED` above 0 and `WORKITEM_LINKS` present. `STATE_CATEGORIES_DIFFER` or `MODULE_LOOKUP_DEGRADED` means the preview route or its shape differs. Counts are printed, never names |
| 2 | Export that build in English and French | Open bug marks and bug lists match the bugs in Azure DevOps, a Resolved bug shows as open, **Without an open bug** leaves the untracked tests, and the Finished time matches the build page in local time |
| 3 | The portable ZIP: unblock, extract and start `Start-AdoToolkit.cmd` | AdoToolkit 0.4.0 loads under the work PC's policies, a French console greets with `est prêt.`, and `Test-AdoConnection` passes |
| 4 | The [previous release's checks](release-0.3.0.md#work-pc-live-checks) and the remaining 0.2.0 checks: `TestCase`, `Bulk`, `Triage`, `Shape`, `Smoke` and `Connection` | Still pending |

## Local validation and developer handoff

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify` | Exit 0. All five stages pass: 55 PowerShell files linted, 174 tooling Pester tests, 142 configuration files, hook layout and the product check. Its only warning is the advisory `What if:` line described under findings not fixed |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1` | Exit 0 with the exact module pins in `tools/BuildModules.psd1` |
| Core tests in the gate | 929 passed under en-US and 929 under fr-CA, with no failures or skips in either TRX report |
| Product Pester tests against the staged module | 109 passed, none failed, skipped or not run |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version` | Prints `0.4.0`; the staged manifests in `artifacts/verify/AdoToolkit/0.4.0/` and `artifacts/AdoToolkit/0.4.0/` report `ModuleVersion` 0.4.0 |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoBuild` | Exit 0; stages `AdoToolkit/0.4.0` from the verified build |
| `tools/package/New-AdoToolkitRelease.ps1` with the pinned PowerShell 7.6.6 archive and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1` | Passes: AdoToolkit 0.4.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the eight expected files under `AdoToolkit/0.4.0/`. The portable ZIP has the same eight files under `module/`, the launcher with the corrected French greeting, `README.txt`, `bundle.json` (PowerShell SHA-256 equal to the pin) and 658 runtime files. The installer is byte-identical to its source |

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.4.0-win-x64.zip` | 110267842 | `9465bf10b1966e98c1e63c728473d183a137583b86da61208ad29492367f1938` |
| `AdoToolkit-0.4.0.zip` | 660948 | `aa9a6360ea3d12de7fa52df992e0c759c5c623ad90535aaf6f9e6b8f50075187` |
| `Install-AdoToolkit.ps1` | 12926 | Same file as `tools/package/Install-AdoToolkit.ps1` |

No Live script, commit, tag, push or GitHub release publication was performed. The
developer owns the rest of the release:

1. Review the final diff, including the linked-bug work, the regenerated goldens and the
   new tests. Run the work-PC checks above and record any inconclusive coverage.
2. Commit the reviewed 0.4.0 changes.
3. Create `v0.4.0` on that commit and push the commit and tag when ready to publish.
4. Watch the release workflow and check its five uploaded assets, version and checksums.
   CI builds its own assets, so compare each checksum with CI's own files; archive
   metadata can differ from a local build.
