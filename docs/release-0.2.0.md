# AdoToolkit 0.2.0 release audit

Reviewed on 2026-09-18 across `src/`, `tests/`, `tools/`, `docs/` and
`.github/workflows/`, including the pending attachment switch, report styling and
fixtures. This is an offline contract audit. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` execution was attempted. The README's confirmed-live areas
remain connections, projects, builds and test runs.

## Findings and disposition

| Finding | Disposition | Files |
| --- | --- | --- |
| A stalled error body replaced an established HTTP error, including 403, with a timeout | Fixed. Preserve the known status; caller cancellation still takes precedence | `src/AdoToolkit.Core/Http/ErrorTranslator.cs`, `AdoHttpPipeline.cs`; `tests/AdoToolkit.Core.Tests/Http/ErrorTranslationTests.cs` |
| Rerun custom fields were discarded and replaced with parent fields | Fixed. Child values override parent values; absent fields fall back and malformed missing values remain ignored | `src/AdoToolkit.Core/TestRuns/TestRunDtos.cs`, `TestAttemptMapper.cs`; `tests/AdoToolkit.Core.Tests/TestRuns/TestAttemptMapperTests.cs` |
| Overlapping result pages duplicated attempts within a run | Fixed. Reject duplicate result IDs within or across pages of one run; IDs remain scoped to their run | `src/AdoToolkit.Core/TestRuns/TestRunService.cs`; `tests/AdoToolkit.Core.Tests/TestRuns/WireAuditRegressionTests.cs` |
| A new package manifest could label stale or invalid DLLs as the new release | Fixed. Read PE assembly identities and versions without loading package code; validate all four DLLs before committing package or release output | `tools/package/Package.Common.ps1`; `tools/tests/Package.Tests.ps1` |
| Exact module installation did not ensure exact module use on a runner with newer modules | Fixed. Share `tools/BuildModules.psd1` between installation and imports; release children inherit `ADOTOOLKIT_RELEASE_BUILD=1` and reject missing pins | `.github/workflows/release.yml`, `tools/lib/dependencies.ps1`, `validation.ps1`, `tools/check.ps1`, `tools/package/Publish-AdoToolkitPackage.ps1`; `tools/tests/Dev.Tests.ps1`, `ReleaseWorkflow.Tests.ps1` |
| Build Top/history limits discarded excess data after transfer | Fixed. Send documented `$top` bounds and continue following tokens on short or empty pages | `src/AdoToolkit.Core/Builds/BuildQuery.cs`, `TestRuns/RunHistoryService.cs`; `tests/AdoToolkit.Core.Tests/Builds/BuildQueryTests.cs`, `TestRuns/RunHistoryTests.cs` |
| Trimming excess closing parentheses from report URLs took quadratic work and allocations | Fixed. Count parentheses once and trim by index. The previous code allocated over 16 MB for a 4 KB synthetic input | `src/AdoToolkit.Core/Reporting/ContentLinks.cs`; `tests/AdoToolkit.Core.Tests/Reporting/RenderedHeaderAndLinkTests.cs` |
| Command help drifted from parameter metadata | Fixed in both locales: `Save-AdoBuildLog -Project` alias/property binding and nullable history parameter types on `Get-AdoBuildTestFailure`. Add parity checks across all 22 commands | `docs/commands/{en-US,fr-CA}/{Save-AdoBuildLog,Get-AdoBuildTestFailure}.md`; `tests/AdoToolkit.PowerShell.Tests/HelpMetadata.Pester.ps1` |
| Diagnose omitted the compiled-help prerequisite | Fixed. Report PlatyPS 1.x when command-help sources are present; no implicit installation | `tools/lib/dependencies.ps1`; `tools/tests/Dev.Tests.ps1` |
| Pending latest-run attachment behavior and `-AllRunAttachments` | No corrective change needed. Retained the feature, tests, help and guide. The last run in attempt order is selected, without falling back to earlier runs; SkipAttachments takes precedence | `src/AdoToolkit.Core/Reporting/TestFailures/`, `TestRuns/AttachmentDownloader.cs`, `src/AdoToolkit.PowerShell/Commands/ExportAdoBuildTestFailureCommand.cs`; `LatestRunAttachmentTests.cs`, `TestFailureExport.Pester.ps1` |
| Pending report styling and regenerated fixtures | No corrective change needed. Retained changes; golden, structure, hostile-content and contrast checks validate them | `src/AdoToolkit.Core/Reporting/Assets/`, `tests/Fixtures/Reports/`, reporting tests |
| Current-release metadata and examples still named 0.1.1 | Updated to 0.2.0. Historical audit records and synthetic multi-version rejection fixtures retain their historical/test versions | `Directory.Build.props`, `README.md`, `docs/guides/getting-started.md`, `docs/tooling.md`, `tools/package/Install-AdoToolkit.ps1` |
| Server-specific response variations, preview routes, retry evidence, log ranges and browser integration | Deferred to the work-PC checks below. Documentation agreement is not live confirmation | `tests/Live/`, `README.md` |

Each bug fix has a regression observed failing before the fix. The Core regression
selection initially reported 12 failures and subsequently passed all 53 selected
tests. Packaging, dependency selection, prerequisite reporting and help metadata
also had failing regressions before correction. All test data is synthetic.

## Azure DevOps Server 2020 REST contracts

[Microsoft's compatibility table](https://learn.microsoft.com/en-us/azure/devops/integrate/concepts/rest-api-versioning?view=azure-devops)
maps Server 2020 to REST 6.0; 7.x is not supported. The audit uses Microsoft's
versioned 6.0 specifications, including their preview resource revisions:
[Core](https://github.com/MicrosoftDocs/vsts-rest-api-specs/blob/master/specification/core/6.0/core.json),
[Work Item Tracking](https://github.com/MicrosoftDocs/vsts-rest-api-specs/blob/master/specification/wit/6.0/workItemTracking.json),
[Build](https://github.com/MicrosoftDocs/vsts-rest-api-specs/blob/master/specification/build/6.0/build.json),
[Test Plan](https://github.com/MicrosoftDocs/vsts-rest-api-specs/blob/master/specification/testPlan/6.0/testPlan.json),
and [Test](https://github.com/MicrosoftDocs/vsts-rest-api-specs/blob/master/specification/test/6.0/test.json).

Routes below are relative to the configured collection URI. All are GET except
the two explicitly marked POST. "Match" means the request matches the 6.0 contract;
it does not claim that this machine reached the server. All 19 registry entries
are included. No route or api-version change was required.

| Endpoint | Route | api-version | Paging and consumed response fields | Status and remaining risk |
| --- | --- | --- | --- | --- |
| ProjectsList | `_apis/projects` | `6.0` | `$top`/`$skip` to empty page; `value[].id`, `name` | Match. The contract supports these offsets as well as continuation tokens; retain the implemented offset strategy |
| WorkItemsBatch | POST `_apis/wit/workitemsbatch` | `6.0` | Up to 200 IDs per request; `value[].id`, `rev`, `fields`, `relations` | Match. Project path is optional (`x-ms-required: false`). Fields/expand behavior still needs V-10 |
| WorkItemTypeCategory | `{project}/_apis/wit/workitemtypecategories/{category}` | `6.0` | No paging; category work-item type names | Match. Localized/custom process categories need work-PC coverage |
| Wiql | POST `{project}/_apis/wit/wiql` | `6.0` | No continuation; optional `$top`; `queryType`, `asOf`, `workItems[].id` | Match. Team path is optional. Flat queries only; reject unbounded results reaching the 20,000 ceiling rather than claim completeness |
| TestPlansList | `{project}/_apis/testplan/plans` | `6.0-preview.1` | Opaque continuation header/token; `value[].id`, `name`, `rootSuite` | Match. The 6.0 list example includes rootSuite. Preview route and actual pagination await V-04 |
| TestSuitesForPlan | `{project}/_apis/testplan/Plans/{planId}/suites` | `6.0-preview.1` | Continuation header/token; suite ID/name/type, `parentSuite`, plan reference | Match. Flat listing plus client traversal; preview behavior and complete trees await V-04 |
| SuiteTestCaseList | `{project}/_apis/testplan/Plans/{planId}/Suites/{suiteId}/TestCase` | `6.0-preview.2` | Continuation header/token; `value[].workItem.id`, `order` | Match. Use the TestPlan-area revision 2 route, not a newer Services version; V-04 pending |
| BuildDefinitionsList | `{project}/_apis/build/definitions` | `6.0` | Continuation header/token; ID/name/path and project reference | Match. Opaque token handling is tested; actual multi-page definitions remain a Live check |
| BuildsList | `{project}/_apis/build/builds` | `6.0` | Continuation header/token; ID/number/definition, repository, branch, state/result, identity, times, URI | Match. Send `$top` for explicit limits/history; continue on server-shortened pages |
| BuildTimeline | `{project}/_apis/build/builds/{buildId}/timeline` | `6.0` | No paging; `records[]` IDs/parents, names/types/order, state/result, issues, log references, attempt/times | Match. The spec marks timelineId optional via `x-ms-required: false`. Retry layouts need Triage.Live |
| BuildLogsList | `{project}/_apis/build/builds/{buildId}/logs` | `6.0` | No paging; JSON log IDs and Int64 lineCount | Match. Request JSON rather than the alternate zip representation |
| BuildLog | `{project}/_apis/build/builds/{buildId}/logs/{logId}` | `6.0` | No continuation; startLine/endLine for tails; text/plain bytes | Match. Long counters and encoding are covered offline. Inclusive bounds/line numbering require V-14 |
| BuildGet | `{project}/_apis/build/builds/{buildId}` | `6.0` | No paging; same build fields as BuildsList; verify returned ID | Match. Optional fields are tolerated; own-collection web links are rebuilt |
| TestRunsList | `{project}/_apis/test/runs` | `6.0` | `$top=100`, actual-count `$skip`, buildUri, includeRunDetails; run IDs, build reference, nested pipeline attempts, times, statistics/aggregate totals | Match. Numeric-string references and aggregate-only responses reflect previously confirmed Server 2020 shapes; retry ordering needs V-22 |
| TestResultsList | `{project}/_apis/test/Runs/{runId}/results` | `6.0` | `$top=1000`, `$skip`, detailsToInclude=None; ID/outcome, automated identity/title, references/group type | Match. 1000 is the documented maximum without detail flags (200 with detail flags). Duplicate IDs now fail explicitly |
| TestResultGet | `{project}/_apis/test/Runs/{runId}/results/{resultId}` | `6.0` | No paging; Iterations,WorkItems,SubResults flags; result text, duration/times, identities, bugs, failingSince.build, testCase, custom fields, iterations/subresults | Match after parser fix. Child custom fields now survive. Field availability and rerun shape still need V-21/V-22 |
| TestResultAttachmentsList | `{project}/_apis/test/Runs/{runId}/Results/{resultId}/attachments` | `6.0-preview.1` | No documented continuation; `value[].id`, fileName, comment, Int64 size, attachmentType | Match. URLs are rebuilt; size/type metadata may be absent. V-23 pending |
| TestSubResultAttachmentsList | `{project}/_apis/test/Runs/{runId}/Results/{resultId}/attachments?testSubResultId={subResultId}` | `6.0-preview.1` | No documented continuation; same attachment fields | Match. Defined under `x-ms-paths`: subresult is a query parameter, not a nested path. V-23 pending |
| TestResultAttachmentContent | `{project}/_apis/test/Runs/{runId}/Results/{resultId}/attachments/{attachmentId}` (optional `testSubResultId` query) | `6.0-preview.1` | Byte stream, no paging; Accept application/octet-stream | Match. The spec allows octet-stream and zip representations; the operation's "Zip" name does not require a zip response. Subresult download selection awaits V-23 |

`RequestBuilder` escapes route segments and query keys/values once, owns the
api-version parameter and prevents caller overrides. Continuation tokens remain
opaque, including spaces and punctuation. Empty continuation pages still advance;
repeated tokens, repeated offset pages and excessive page counts fail explicitly.
Offset paging advances by the returned count and does not assume a short page is
the last page. Work-item batches chunk rather than page.

JSON source generation allows numbers represented as strings for numeric DTO
fields, including run/build/result/attachment references. The special Test Case
reference converter accepts a JSON number or string; unusable optional references
become diagnostics. Invalid required IDs fail validation. Date fields are mapped
explicitly, unknown outcomes remain representable, absent optional metadata stays
absent, and server-provided URLs are ignored where links are rebuilt. The audit
did not promote undocumented response fields to required fields.

## Release workflow and external services

| Contract | Audit result |
| --- | --- |
| Action versions | `actions/checkout@v7` and `actions/setup-dotnet@v6` exist in their publishers' [checkout releases](https://github.com/actions/checkout/releases) and [setup-dotnet releases](https://github.com/actions/setup-dotnet/releases). Retained; major tags and windows-latest remain moving dependencies |
| PowerShell download | [PowerShell v7.6.6](https://github.com/PowerShell/PowerShell/releases/tag/v7.6.6) publishes the Windows x64 zip and hashes.sha256. Streamed archive SHA-256 matched `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860`. No downloaded PowerShell was installed locally |
| Hash contract | The published manifest uses a UTF-16LE BOM, which Get-Content detects. Offline workflow tests cover that format and reject a mismatch before Expand-Archive. The archive and checksum come from the same publisher; this is not an independent signing mechanism |
| Module pins | Published versions exist: [Pester 5.9.1](https://www.powershellgallery.com/packages/Pester/5.9.1), [PSScriptAnalyzer 1.25.0](https://www.powershellgallery.com/packages/PSScriptAnalyzer/1.25.0), [PlatyPS 1.0.3](https://www.powershellgallery.com/packages/Microsoft.PowerShell.PlatyPS/1.0.3). PSResourceGet receives bracketed exact versions; fallback Install-Module receives RequiredVersion. Imports now share these pins |
| Tag/version | Strict comparison of `v<MSBuild Version>` to GITHUB_REF_NAME; mismatched and prerelease tags rejected by offline workflow tests. VersionPrefix is 0.2.0 |
| Restore/build | CI explicitly restores with locked mode. Verify itself neither installs nor restores. Local packaging uses existing restored assets |
| Package/assets | Validate staged layout, help and DLL versions; construct and validate sibling temporary assets; rollback existing assets on replacement failure. Release contains zip, SHA-256 file and standalone installer |
| Publishing | GitHub release creation remains tag-triggered, uses the three exact assets, notes-file and verify-tag. It was not executed locally; commits/tags/pushes belong to the developer |

## Work-PC Live checks, least-confirmed areas first

Install the candidate 0.2.0 package on the work PC, then use a fresh PowerShell
process for each script. Set variables there; do not send their values or raw
responses back. The profile should name the target collection and default project.
All rows require `ADOTOOLKIT_LIVE_PROFILE` in addition to the variables shown.
Run scripts as `pwsh -NoProfile -File .\tests\Live\<script>`.

| Rank | Script | Additional variables | Evidence to seek |
| --- | --- | --- | --- |
| 1 | `TestCase.Live.ps1` | `ADOTOOLKIT_LIVE_TESTCASE_ID`; `ADOTOOLKIT_LIVE_SHARED_PARAM_CASE_ID` for shared-parameter coverage | Work-item batching, categories, Shared Steps/parameters and fields/expand. Select representative formatted content; V-02 requires visual comparison |
| 2 | `Bulk.Live.ps1` | `ADOTOOLKIT_LIVE_PLAN_ID`, `ADOTOOLKIT_LIVE_SUITE_ID` | TestPlan preview routes, actual continuation and membership completeness, WIQL ceiling. Small datasets cannot prove pagination or the 20,000 cap |
| 3 | `TestFailures.Live.ps1` | `ADOTOOLKIT_LIVE_TEST_BUILD_ID`; also `ADOTOOLKIT_LIVE_RERUN_BUILD_ID` and `ADOTOOLKIT_LIVE_REATTEMPT_BUILD_ID` to complete V-22 | Detailed results, IDs/optional fields, rerun custom fields, result/subresult attachments and download lengths. Choose builds that actually contain failures, attachments and both retry kinds |
| 4 | `Triage.Live.ps1` | `ADOTOOLKIT_LIVE_DEFINITION`, `ADOTOOLKIT_LIVE_BUILD_ID`; `ADOTOOLKIT_LIVE_RETRIED_BUILD_ID` for retry coverage | Timeline retries, build/definition continuation, log media type and tail bounds |
| 5 | `Shape.Live.ps1` | `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, `ADOTOOLKIT_LIVE_PLAN_ID`, `ADOTOOLKIT_LIVE_SUITE_ID` for complete sampling | Safe property/type observations; repeat with retry builds. A missing field in the sampled first run does not establish global absence |
| 6 | `Smoke.Live.ps1` | Either `ADOTOOLKIT_LIVE_TEST_BUILD_ID` or `ADOTOOLKIT_LIVE_DEFINITION`; add `ADOTOOLKIT_LIVE_PLAN_ID` and `ADOTOOLKIT_LIVE_SUITE_ID` for Test Case export | End-to-end retrieval and reports. Its failure report uses SkipAttachments; it does not validate the new download switch |
| 7 | `Connection.Live.ps1` | None | Recheck the already-confirmed connection/project baseline |

`INCONCLUSIVE` (exit 2) is not a pass. Retry IDs alone do not prove V-22; both
response shapes must be observed. Additionally export one representative multi-run
failure set to two separate folders, first with defaults and then with
`-AllRunAttachments`. Confirm all attempts remain visible, the default downloads
only the last run in attempt order, the switch downloads earlier eligible files,
and a latest run without attachments does not fall back. Inspect both report
languages, filtering, links and printing under the work browser's policy.

Controlled error-language checks, web-link targets and runner-specific output also
remain manual acceptance items. None is represented as a completed Live check.

## Local validation and developer handoff

The baseline full gate passed before changes. Release validation on 2026-09-21:

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify` with `ADOTOOLKIT_RELEASE_BUILD=1` | Exit 0; all five stages pass; 146 tooling tests pass without skips |
| Core tests in the full gate | 872 pass under en-US and 872 under fr-CA, zero failures or skips in either TRX report |
| Product build, package inspection and product Pester | Pass, including parameter/help parity in both source locales and the pending attachment switch |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version` | Prints `0.2.0` |
| `pwsh -NoProfile -File .\tools\package\Publish-AdoToolkitPackage.ps1 -NoBuild` | Exit 0 using the verified Release build and pinned PlatyPS; no restore or installation |
| `pwsh -NoProfile -File .\tools\package\New-AdoToolkitRelease.ps1` | Exit 0; creates all three assets in `artifacts/release/` |
| Archive validation | Eight expected files under `AdoToolkit/0.2.0/`; checksum matches; standalone installer is byte-identical to its source |

Local assets:

| File | Bytes |
| --- | ---: |
| `artifacts/release/AdoToolkit-0.2.0.zip` | 616699 |
| `artifacts/release/AdoToolkit-0.2.0.zip.sha256` | 87 |
| `artifacts/release/Install-AdoToolkit.ps1` | 12926 |

Local zip SHA-256:
`9cc6f11a3836649e123b2a62f5032a1146502a01acc0d14ff8afbd5f604ec8a1`.
No Live script, commit, tag, push or GitHub release publication was performed.

The developer owns the remaining release lifecycle:

1. Review the final diff, including previously uncommitted feature/style changes
   and newly added tests. Run the work-PC checks above and resolve failures; record
   inconclusive coverage explicitly.
2. Commit the reviewed 0.2.0 changes. The existing v0.1.1 tag is left untouched.
3. Create `v0.2.0` on that commit and push the commit and tag when ready to publish.
4. Observe the release workflow and verify its three uploaded assets, version and
   checksum. CI builds its own assets; compare its checksum to its own zip, since
   archive metadata can differ from a local build.
