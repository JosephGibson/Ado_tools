# AdoToolkit 0.10.5 release notes

Prepared on 2026-10-05 from the branch `0.10.5`, which starts at `main` after the pull
request of 0.10.0 was squash-merged as `68579c7`; the files there are those of 0.10.0. The
reviewed changes are the working tree on top of it: the branch has no commit of its own.
0.10.5 adds no feature. It is the quality pass recorded in the
[0.10.5 plan](archive/plans/0.10.5-quality-pass.md): a review of the whole repository by area
with the `repo-review` workflow, checks of the seams between areas, exploratory runs of the
staged module and a critique of the plan found 23 defects. 22 are fixed here, each with a
regression test where a test can show it, and one, a permission setting, is left to the
developer. The three test suites lost 47 tests that another test already covers, each removal
proven by a mutation run, and a dozen slow tests got at least twice as fast. The guides and
help were corrected in both cultures. No public contract changes. This is an offline review.
No Azure DevOps Server connection and no `tests/Live/*.Live.ps1` run was used, so nothing below
is confirmed live. The areas the README lists as confirmed are still connections, projects,
builds and test runs.

## What changed

0.10.5 sends no new request and adds no cmdlet, parameter, output type, configuration setting
or string. No report golden changed, and the HTML report, the CSV file, the JSON report schema
and the seven-file package layout are those of 0.10.0. The [0.10.0 notes](archive/release-0.10.0.md)
describe the last feature.

### The module

| Area | 0.10.5 behavior |
| --- | --- |
| A body that does not decompress | A response marked gzip or deflate whose body does not decompress is unreadable. Under an error status it keeps the error of that status, as any unreadable error body does: a 404 is still `ObjectNotFound`, a 403 still an authorization error. Under a success status it is `AdoResponseFormatException`, a response format error with the operation's name, and it is not retried. Both used to leave the pipeline as a raw `System.IO.InvalidDataException`, which ended the cmdlet with a terminating .NET error. In a failed-test export the format error is recoverable: the attachment is recorded as failed and the export goes on |
| Errors while a body is read | An `HttpIOException` raised while the body is read is retried only when its kind says that the connection ended or failed: `ResponseEnded`, `ConnectionError`, `NameResolutionError` or `Unknown`, as a send that fails is (§6.5). `InvalidResponse`, a malformed answer, is sent once and reported with `IsRetryable` false; it used to be sent three times. Any other `IOException` is retried as before |
| The project of a work item | `WorkItemService.Map` refuses a `System.TeamProject` of `.` or `..`, which a URL path collapses, with a response format error for that work item. Its links used to leave the collection. The three other paths that map a server project already refused it |
| Attachment anchors in the failed-test report | When an attempt lists one attachment twice, from its result's list and from a sub-result's (V-36), the sub-result's entry gets `-s<sub-result>` in its anchor. Both entries stay listed, and an attempt without a repeat keeps the anchors it always had, so no golden changed. The two entries used to share one `id`, and the report's own check then refused the export |
| Completion of profile names | `Get-AdoProfile -Name` takes a wildcard pattern, so its completion escapes `[`, `]`, `*`, `?` and the backtick in the name it inserts: `Lab [1]` now completes to a pattern that matches `Lab [1]` and not `Lab 1`. `Connect-Ado`, `Set-AdoProfile` and `Remove-AdoProfile` take the name literally and complete it unescaped, as before |
| Default console tables | 33 columns of 16 default table views, every column that shows text from the server, replace each control character but tab and line feed with a space: the views of Test Cases, test steps, test plans, test suites, WIQL results, work items, projects, connection tests, build definitions, builds, timeline records, build failures, test runs, failed-test sets, failed tests and their bugs. A title holding ESC, BEL or a C1 control sequence can no longer drive an interactive terminal. The objects keep the text as the server sent it, so `Format-List`, `Select-Object` and the reports show it unchanged |
| The installer | `tools/package/Install-AdoToolkit.ps1` removes only `.staging-*` leftovers before it installs. After it has committed, it removes a `<version>.previous-<guid>` backup only when the folder of that version exists. A backup that a failed rollback kept is the only copy of its version and stays until that version is installed again (`tools/package/AGENTS.md`); it used to be deleted at the start of the next installation, even one that then failed |

### Code

| File | Change |
| --- | --- |
| `src/AdoToolkit.Core/Http/ErrorTranslator.cs` | `InvalidDataException` joins the exceptions of an unreadable error body |
| `src/AdoToolkit.Core/Http/AdoHttpPipeline.cs` | `ExecuteAsync` turns an `InvalidDataException` of the consumer into `AdoResponseFormatException`. Two internal seams for faster tests: an optional `TimeProvider` passed to `InactivityReadStream`, and an optional `maximumPages` on `GetPagesAsync`, whose default is still `MaximumPages` |
| `src/AdoToolkit.Core/Http/InactivityReadStream.cs` | Each read runs its inactivity timer on the given `TimeProvider`, `TimeProvider.System` by default |
| `src/AdoToolkit.Core/Http/RetryPolicy.cs` | `IsTransient` reads the `HttpRequestError` of an `HttpIOException` |
| `src/AdoToolkit.Core/WorkItems/WorkItemService.cs` | `RequestBuilder.IsPathSegment` on the work item's project |
| `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` | The attachment anchor, as above |
| `src/AdoToolkit.Core/Reporting/TestFailures/TestFailureSignal.cs` | A comment: New means the build before passed the test, as the code decides |
| `src/AdoToolkit.PowerShell/Completion/ProfileNameCompleter.cs` | Escapes wildcards for `Get-AdoProfile -Name` only |
| `src/AdoToolkit.PowerShell/AdoToolkit.Format.ps1xml` | Each server-text column is a script block that replaces `[\p{Cc}-[\t\n]]` with a space |
| `tools/package/Install-AdoToolkit.ps1` | The leftover rule, as above; the zip names of its examples carry 0.10.5 |

### The development tooling and the live checks

| File | Change |
| --- | --- |
| `tools/lib/discovery.ps1` | `Invoke-RepositoryRipgrep` reads ripgrep's output as UTF-8 through a process of its own, as `Invoke-BoundedProcess` already did. Decoded with the console code page, a file whose name is not ASCII was dropped from discovery and a match inside it failed `find` |
| `tests/Live/Smoke.Live.ps1` | `Invoke-AdoLiveStep` prints a code ending in `_REQUIRED`, a missing input such as `PROFILE_DEFAULT_PROJECT_REQUIRED`, as `INCONCLUSIVE`, and the script then exits 2. It used to print `FAIL` and exit 1 (`tests/Live/AGENTS.md`) |
| `tests/Live/TestFailures.Live.ps1` | V-37 sends its work item batch in chunks of at most 200 IDs, as the module reads bugs. A build with more than 200 bugs used to give a false `FAIL V-37 CHECK_FAILED` |
| `tests/AdoToolkit.Core.Tests/Localization/HardCodedStringTests.cs` | The scan also finds a target-typed `new(...)` of an `Ado*Exception`, a `ProgressRecord` or `ErrorDetails`: assigned to a variable, returned by an expression-bodied member, or set as `ErrorDetails = new(...)`. `src/` uses that form at five call sites; none holds a literal |
| `tests/AdoToolkit.Core.Tests/Localization/FrenchTerminologyTests.cs` | The colon rule also refuses a colon with no space before it, after a letter, `»` or `)`, in the catalog and in the prose of the fr-CA help outside front matter, fenced blocks and code spans. It found no violation |
| `tools/tests/LiveAudit.Tests.ps1` | The V-30 bug lookup test cited `F17`, which no document defines; it cites V-30 |
| `docs/tooling.md` | The rows of `tests/Live/Smoke.Live.ps1` and `tests/Live/TestFailures.Live.ps1` say what changed above |

### Tests

The executed counts of 0.10.5 are those of the final gate; the counts of test methods and `It`
blocks are those of section 10.4 of the plan.

| Suite | 0.10.0 | 0.10.5 |
| --- | --- | --- |
| Core, en-US | 1588 executed, 655 test methods | 1565 executed, 632 methods: 30 removed or merged, 7 regression tests added |
| Core, fr-CA | 1439 executed | 1423 executed |
| Product Pester | 183 tests, 146 `It` blocks | 181 tests, 144 `It`: 4 removed or merged, 2 regression tests added |
| Tooling Pester | 339 tests, 236 `It` blocks | 331 tests, 227 `It`: 13 removed or merged, 4 regression tests added |
| Fixtures | 189 | 189 |

Removed or merged, each after a run in which a temporary edit of the code it guards failed the
test that covers it instead; section 10.2 of the
[plan](archive/plans/0.10.5-quality-pass.md#102-evidence-for-removals-and-merges) gives the edit
of each run:

| Removed | Still covered by |
| --- | --- |
| `ResponseJsonTests.TheLimitIs256MiB` | `ADeclaredLengthOverTheLimitFailsBeforeReading` |
| `GenerationFolderCommitTests.LockedReportFailsTheMoveAndRemovesTheNewFolder` | `FailureAtEachStepLeavesThePreviousGenerationByteIdenticalWithNoTemporaryItems` (S5-7) |
| `WireAuditRegressionTests.F07…` and `F05…` | `TestAttemptMapperTests.RerunCustomFieldsOverride…`; `RetrievalConcurrencyTests.HistoryBudgetCounts…` |
| `TestFailureRetrievalTests.EmptyPageProbeIsSkippedForHistoryBuilds…` | `RetrievalRequestBoundTests.HistoryBuildsOnlyCostRunAndResultPages` |
| `TestPlanPagingTests.RepeatedPlanTokenFailsInsteadOfLooping`, `BuildQueryTests.RepeatedBuildTokenFails` | `PagingTests.RepeatedPagesOrTokensFail` |
| `TestPlanPagingTests.TestplanEndpointsUseTheExactPinnedVersions…` | `TestPlanPagingTests.PlansContinueThroughShortAndEmptyTokenPagesInOrder` |
| `WorkItemServiceTests.SafeBatchPostRetriesWithFreshRequestsAndTheSameBody` | `RetryPolicyTests.RetryAfterIsHonoredAndPostContentIsRecreated`, `HttpPipelineContractTests.RegistryContainsOnlyUniqueExactServer2020Endpoints` |
| Two `RunHistoryChartTests` | `CurrentOutlineUnavailableHatchingAndEquivalentTableArePresent` (S5-3) |
| `RenderedHeaderAndLinkTests.Fixture29LinksOnlySafeSchemesAfterParsing` | `OnlyAllowedAbsoluteContentSchemesBecomeLinks` (S2-6) |
| Two `ReportHeaderAndLinkTests`, four `TestCaseReportStructureTests`, four `TestCaseDetailRenderingTests`, two `MultiCaseDocumentTests` and one `FrenchTypographyTests` that assert markup of golden inputs | `GoldenReportTests`, `MultiCaseDocumentMatchesReviewedGolden` (S3-5) and `RenderedHeaderAndLinkTests.MetadataAndEveryConstructedLinkAreRendered` (S2-6) |
| `SmallAttachmentTests.SkipAttachmentsDownloadsNothingAndNeedsNoDownloader` | `TestFailureExporterTests.SkipAttachmentsDownloadsNothingAndCreatesNoFolder` (S5-6) |
| `ContentSecurityPolicyTests.StaticScriptAndPlaceholderDoNotVaryByReportOrCulture` | `ExactWrittenUtf8BytesHaveOneHashAndNoOtherScriptSources` (S5-5) and the goldens |
| `CompactReportTests.AHundredFailuresWithFourteenAttemptsStayWithinTheSizeBudget` | Merged into `ErrorTextTests` 200 × 14, which now also carries attachments, dates and both stage cultures |
| `HttpPipelineContractTests.HandlerDecodesGzipAndDeflate`, `BuildQueryTests.TopStillFollowsShortAndEmptyPages…` | Merged into `HandlerUsesDefaultWindowsAuthenticationAndConservativeTransport` and `BuildAndDefinitionPagingContinueThroughEmptyPagesAndEncodeOpaqueTokensOnce` |
| Theory rows that walk a branch another row walks: `NearTheHistoryBudget…` 11, 14 and 20; `RequestsInFlight…` 3; `TerminatingErrorStops…` 403; `RequestGateTests…` 1; the stalled error body 404 and 400 | The rows kept beside them |
| `Export.Pester.ps1`, the same case twice; `TestPlans.Pester.ps1`, the suite subtree; `Projects.Pester.ps1`, no connection; `TestRuns.Pester.ps1`, compression | `BulkTestCase.Pester.ps1` (S3-5); Core `SuiteTraversalTests`; `Usability.Pester.ps1`; the Bound 6 row of the concurrent retrieval test |
| Eight duplicate tooling tests in `Package`, `Dev`, `Stages` and `Workflow`, and five merged ones in `Gate`, `Verify`, `Workflow` and `Dev` | The tests named in section 5.3 and 5.2 of the plan |

Faster, each at least twice as fast with its assertions kept, measured alone before and
after; a speed-up that did not halve its test was reverted:

| Test | Seconds, before → after |
| --- | --- |
| `CancellationTests.DownloadInactivityBudgetResetsAfterEachRead`, on a manual clock (`tests/AdoToolkit.Core.Tests/Http/ManualTimeProvider.cs`) | 1.34 → 0.04 |
| `PagingTests.PageCeilingFailsInsteadOfReportingPartialSuccess`, with a ceiling of three pages; the 10,000-page constant of §6.4 is asserted | 0.41 → 0.07 |
| `RequestGateTests.TimeSpentWaitingForASlot…` | 0.63 → 0.27 |
| `OrderedParallelTests`, degree 1 | 0.56 → 0.12 |
| `RetrievalConcurrencyTests.NearTheHistoryBudget…`, `SetAndRendered…` | 4.49 → 1.22; 1.06 → 0.46 |
| `ParallelDownloadTests`, Bound 3 and four Bound 1 rows | 0.33 → 0.09; 0.38 → 0.02 |
| `ErrorTranslationTests.StalledErrorBody…` | 0.37 → 0.07 |
| `ErrorTextTests` 200 × 14 with the merged 100 × 14 test | 15.51 → 7.88 |
| `TestRuns.Pester.ps1` concurrent retrieval, Bound 1 and Bound 6 with compression, with a per-response `Delay` in `tests/AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1` | 1.73 → 0.41; 2.03 → 0.87 |
| `SyntheticServer.Pester.ps1` paced response | 0.68 → 0.26 |
| `Verify.Tests.ps1`, the gate failing beside an in-process stage | 2.64 → 0.77 |
| `Dev.Tests.ps1`, the default invocation on a copied tooling tree | 1.80 → 0.57 |

New: the 13 regression tests under [Bug fixes](#bug-fixes).

### Existing tests changed

None was edited to make it pass.

| Test | Change | Reason |
| --- | --- | --- |
| `tools/tests/LiveAudit.Tests.ps1`, the V-30 bug lookup | Its name cites V-30 instead of `F17` | R-11 |
| `HardCodedStringTests.HumanMessageCallSitesDoNotContainStringLiterals` | Scans three more forms of construction | R-12 |
| `FrenchTerminologyTests` colon rule | Also refuses a colon with no space | R-13 |
| `tools/tests/Package.Tests.ps1`, the leftover sweep | Its leftover is `0.1.0.previous-<guid>` beside an installed 0.1.0, not `0.0.9.previous-<guid>` alone | R-10: the old leftover is an orphaned backup, which the installer now keeps |
| `ErrorTextTests` 200 × 14 | Attachments, dates, failure fields, English and French stages, no attempt open; a budget of 54,100,000 bytes for 52,911,987 measured | The merge of `CompactReportTests` |
| `HandlerUsesDefaultWindowsAuthenticationAndConservativeTransport`, `BuildAndDefinitionPaging…`, `TestRuns.Pester.ps1` concurrent retrieval, and the tests of `Gate`, `Verify`, `Workflow` and `Dev` that took over a merged test | Each takes the assertions of the test merged into it | The merges |
| `RetrievalConcurrencyTests.AuthorizationFailureInHistory…` | At a bound of one, also asserts one request in flight at most | `SetAndRendered…` gave that check up |
| `RequestGateTests.TimeSpentWaitingForASlot…`, `PagingTests.PageCeiling…`, `CancellationTests.DownloadInactivityBudgetResetsAfterEachRead`, `SyntheticServer.Pester.ps1` paced response, `Verify.Tests.ps1` gate beside a stage, `Dev.Tests.ps1` default invocation | Shorter waits, a manual clock, a lower ceiling or a copied tree; `TimeSpentWaitingForASlot…` drops its wall-clock assertion, which its third success already implies | The speed-ups |

No golden changed and no acceptance tag changed.

### Documentation

| Area | Change |
| --- | --- |
| `docs/commands/en-US/Export-AdoBuildTestFailure.md`, `docs/commands/fr-CA/Export-AdoBuildTestFailure.md` | **New**, in the report and in the CSV column, means that the build before ran the test and it passed, as `TestFailureSignal` decides; they said it did not fail. Nothing is said, and the column is empty, when that build could not be read, did not run the test or ended it with another outcome |
| `docs/commands/en-US/Test-AdoConnection.md`, `docs/commands/fr-CA/Test-AdoConnection.md` | The project URL check lists the projects of the parent URL, one request per 100 projects and a last one that finds no more; it said one more request |
| `docs/guides/build-report.md` | `-Project` is added only to the commands that read from the server, as `Export-AdoBuildTestFailure` and `Get-AdoConnection` have none. A `.csv` path takes one build and later sets get `TestFailureReportSingleFile`. Two troubleshooting rows name their error by ID, and one is added for `TestFailureReportSingleFile`. The trend text matches the help. The help list links `Connect-Ado` and `Get-AdoConnection` |
| `docs/guides/pipeline-triage.md` | The `-Format Csv` row says that a `.csv` file takes a single build |
| `docs/guides/configuration.md` | A response marked compressed that does not decompress, as above |
| `docs/guides/getting-started.md` | The completion of `Get-AdoProfile -Name`, the help list links `Remove-AdoProfile`, and the zip names carry 0.10.5 |
| `README.md` | The version, a paragraph on this version and the links to these notes |
| Archive | The 0.9.15 notes moved to `docs/archive/release-0.9.15.md` and the finished 0.9.15 and 0.10.5 plans to `docs/archive/plans/`, unedited apart from the release row of the 0.10.5 plan's progress table, written before the move. The archive indexes list them, the V-37 row of `docs/archive/README.md` resolves to the new place, and the changelog and the 0.10.0 notes point there |

### Visible changes for 0.10.0 users

- A response that is marked compressed but does not decompress gives an AdoToolkit error in
  the session's language: the error of its HTTP status under an error status, a response
  format error otherwise. It used to end the cmdlet with a terminating
  `System.IO.InvalidDataException`. In a failed-test export it fails that attachment only.
- A malformed response found while its body is read is sent once instead of three times.
- A work item whose project the server names `.` or `..` is a response format error for that
  work item.
- A failed-test report in which an attempt lists one attachment from its result and from a
  sub-result is written, with both entries; in 0.10.0 the export failed.
- Pressing Tab after `Get-AdoProfile -Name` inserts the name with its wildcard characters
  escaped, so that it matches that profile only; the other profile cmdlets insert the name as
  before.
- The default console tables show a space for each control character but tab and line feed
  in server text. A script that reads properties, or formats them with `Format-List`, sees
  the text as the server sent it, as before.
- `Install-AdoToolkit.ps1` keeps a `<version>.previous-<guid>` folder whose version folder is
  missing.
- Configuration files, the reports, the CSV columns, the JSON report schema and the
  installation layout are unchanged, and the file names of the assets carry 0.10.5.

### Public contract changes

None. No cmdlet, parameter, output type, configuration setting, CSV column or schema changed.
A body that does not decompress now ends as `AdoResponseFormatException`, or as the error of
its HTTP status, instead of a raw `InvalidDataException`: that is the fix. The table views
change what the console shows, not the objects, and completion changes only the text it
inserts.

## Bug fixes

| ID | Finding, cause and fix | Regression test | Files |
| --- | --- | --- | --- |
| R-1 | A 4xx error body labelled gzip that does not decompress threw `InvalidDataException` past the read's filter, so the status was lost and the cmdlet ended with a raw terminating error (§6.7). The filter now takes `InvalidDataException` | `ErrorTranslationTests.ErrorBodyThatCannotBeDecompressedKeepsTheKnownHttpStatus`, 403 and 404 | `src/AdoToolkit.Core/Http/ErrorTranslator.cs` |
| R-2 | A success body that does not decompress left the pipeline as a raw `InvalidDataException`, and one bad attachment body stopped a whole export. `ExecuteAsync` now throws `AdoResponseFormatException`, not retried | `HttpPipelineContractTests.SuccessBodyThatCannotBeDecompressedIsAResponseFormatErrorWithoutRetry` | `src/AdoToolkit.Core/Http/AdoHttpPipeline.cs` |
| R-3 | `IsTransient` treated every `IOException` of a body as a lost connection, so an `HttpIOException` with `InvalidResponse` was sent three times (§6.5). It now reads the error's kind | `RetryPolicyTests.BodyReadErrorsAreRetriedOnlyWhenTheConnectionWasLost`: `ResponseEnded` and `ConnectionError` retried, `InvalidResponse` sent once | `src/AdoToolkit.Core/Http/RetryPolicy.cs` |
| R-4 | `WorkItemService.Map` took `System.TeamProject` into links unchecked, so `..` built a work item link outside the collection. It now refuses a dot segment | `WorkItemServiceTests.DotSegmentProjectIsAFormatErrorAndBuildsNoLink`, `..` and `.` | `src/AdoToolkit.Core/WorkItems/WorkItemService.cs` |
| R-5 | An attachment anchor was built from the result and attachment IDs only, so an attachment that a result and its sub-result both list gave two `<li>` with one `id`, and the validator refused the export. The sub-result now tells the two apart | `AttachmentRenderingTests.AnAttachmentListedByTheResultAndByItsSubResultKeepsTwoAnchors` | `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` |
| R-6 | The completer inserted names unescaped into `Get-AdoProfile -Name`, a wildcard parameter. It now escapes them there | `tests/AdoToolkit.PowerShell.Tests/Profiles.Pester.ps1`, completes a profile name for the -Name pattern of Get-AdoProfile so that it matches only that profile | `src/AdoToolkit.PowerShell/Completion/ProfileNameCompleter.cs` |
| R-7 | ripgrep's output was decoded with the console code page, which garbled a name that is not ASCII. It is now read as UTF-8 | `tools/tests/Dev.Tests.ps1`, lists and searches a file whose name is not ASCII, with and without ripgrep | `tools/lib/discovery.ps1` |
| R-8 | `Invoke-AdoLiveStep` printed every code as `FAIL`, a missing input included. A `_REQUIRED` code is now `INCONCLUSIVE`, exit 2 | `tools/tests/LiveAudit.Tests.ps1`, SMOKE-2 reports a missing input as INCONCLUSIVE and anything else as FAIL | `tests/Live/Smoke.Live.ps1` |
| R-9 | V-37 sent every bug ID in one batch. It now sends at most 200 IDs per batch | `tools/tests/LiveAudit.Tests.ps1`, V-37 reads more than 200 bugs in batches of at most 200 IDs | `tests/Live/TestFailures.Live.ps1` |
| R-10 | The installer swept every `previous-*` folder before its own commit, the only copy of a version included. It now sweeps only a backup beside its version folder, after the commit | `tools/tests/Package.Tests.ps1`, keeps a backup that is the only copy of its version through a failed and a successful install | `tools/package/Install-AdoToolkit.ps1` |
| R-11 | A test name cited `F17`, which no document defines | None: a test name | `tools/tests/LiveAudit.Tests.ps1` |
| R-12 | The hard-coded string scan matched `new Type(...)` only | `HardCodedStringTests.EveryFormOfConstructionIsScanned`, on synthetic snippets | `tests/AdoToolkit.Core.Tests/Localization/HardCodedStringTests.cs` |
| R-13 | The French colon rule failed only on an ordinary space before `:` | `FrenchTerminologyTests.TheColonRuleCatchesAnOrdinarySpaceAndNoSpace` | `tests/AdoToolkit.Core.Tests/Localization/FrenchTerminologyTests.cs` |
| R-14 | The help said **New** means the test did not fail in the build before; the code says it passed | None: documentation | Both `Export-AdoBuildTestFailure` topics, `docs/guides/build-report.md` |
| R-15 | The help said the project URL check sends one more request | None: documentation | Both `Test-AdoConnection` topics |
| R-17 | The guide added `-Project` to every command, two that have none included | None: documentation | `docs/guides/build-report.md` |
| R-18 | The CSV section left out that a `.csv` path takes one build | None: documentation | `docs/guides/build-report.md`, `docs/guides/pipeline-triage.md` |
| R-19 | The help list of the getting-started guide left out `Remove-AdoProfile`, which the guide uses | None: documentation | `docs/guides/getting-started.md` |
| R-20 | The help list of the build-report guide left out `Connect-Ado` and `Get-AdoConnection` | None: documentation | `docs/guides/build-report.md` |
| R-21 | A troubleshooting row named its error by neither ID nor category (`docs/AGENTS.md`) | None: documentation | `docs/guides/build-report.md` |
| R-22 | The finished 0.9.15 plan was never archived | None: archiving | `docs/archive/plans/0.9.15-bug-age-and-assignee.md` |
| R-23 | The default table views wrote server text as sent, so ESC and C1 sequences in a title reached an interactive terminal (`AGENTS.md`: encode external data for its destination). Each server-text column now replaces control characters with a space | `tests/AdoToolkit.PowerShell.Tests/WorkItems.Pester.ps1`, shows a title holding terminal control characters as plain text and keeps them on the object | `src/AdoToolkit.PowerShell/AdoToolkit.Format.ps1xml` |

### Pre-fix failures

Run on 2026-10-05 for this release, one fix at a time, by the procedure of the `release`
skill: each file of a fix was saved to the session's scratch folder, its content at `68579c7`
copied over it, the covering test run alone, then the saved file copied back with a fresh
timestamp, and `git diff --stat` showed the branch's own counts each time. Core tests ran with
`dotnet test … --filter`, tooling tests with Pester 5.9.1 imported explicitly, and product
tests against the module staged by `tools/check.ps1 -SkipTests` from the restored source, with
`ADOTOOLKIT_CONFIG_PATH` naming a file that does not exist.

| ID | 0.10.0 source restored | Failure | Totals |
| --- | --- | --- | --- |
| R-1 | `ErrorTranslator.cs` | `Assert.IsAssignableFrom() Failure: Value is an incompatible type`, expected `AdoException`, actual `System.IO.InvalidDataException`, both rows | Failed 2, passed 0, total 2 |
| R-2 | `AdoHttpPipeline.cs`, with `InactivityReadStream.cs`, `CancellationTests.cs` and `PagingTests.cs`: the test seams of the same file would not compile against the 0.10.0 pipeline without their own 0.10.0 tests | `Assert.Throws() Failure: Exception type was not an exact match`, expected `AdoResponseFormatException`, actual `System.IO.InvalidDataException` | Failed 1, passed 0, total 1 |
| R-3 | `RetryPolicy.cs` | `Assert.Throws() Failure: No exception was thrown` on the `InvalidResponse` row: it was retried and the second answer succeeded | Failed 1, passed 2, total 3 |
| R-4 | `WorkItemService.cs` | `Assert.Throws() Failure: No exception was thrown`, expected `AdoResponseFormatException`, both rows | Failed 2, passed 0, total 2 |
| R-5 | `HtmlTestFailureRenderer.cs` | `System.IO.InvalidDataException : The report output is empty or invalid.` | Failed 1, passed 0, total 1 |
| R-6 | `ProfileNameCompleter.cs`, then staged | `Expected: 'Lab [1]' But was: 'Lab 1'` | Failed 1 of 1 selected |
| R-7 | `discovery.ps1` | Under a console whose output code page is 850, this computer's OEM code page: `Expected @('docs/guides/d<é>marrage.md', 'src/worker.ps1'), but got 'src/worker.ps1'` on the ripgrep row. Under this session's UTF-8 console, 65001, both rows passed against the 0.10.0 source too; see [Findings not fixed](#findings-not-fixed) | Code page 850: failed 1, passed 1. Code page 65001: passed 2 |
| R-8 | `Smoke.Live.ps1` | `Expected: 'INCONCLUSIVE SMOKE-2 PROFILE_DEFAULT_PROJECT_REQUIRED' But was: 'FAIL SMOKE-2 PROFILE_DEFAULT_PROJECT_REQUIRED'` | Failed 1 of 1 selected |
| R-9 | `TestFailures.Live.ps1` | `Expected Invoke-AdoTestBatch to be called 2 times exactly, but was called 1 times` | Failed 1 of 1 selected |
| R-10 | `Install-AdoToolkit.ps1` | `Expected $true, but got $false` at `tools/tests/Package.Tests.ps1:353`: the install that failed its thumbprint check had already removed the only copy | Failed 1 of 1 selected |
| R-12 | The scan's one 0.10.0 pattern in place of the four, in a copy of the current file: the fix and its regression test share the file, so the 0.10.0 file would hold no test to run | `Assert.Single() Failure: The collection was empty` | Failed 1, passed 1 (the repository scan), total 2 |
| R-13 | The 0.10.0 rule alone, an ordinary space before `:`, in a copy of the current file, for the same reason | `Assert.Equal() Failure: Values differ`, expected `True`, actual `False`, on `Profil introuvable: {0}` and `« Phase »: {0}` | Failed 2, passed 4, total 6 |
| R-23 | `AdoToolkit.Format.ps1xml`, then staged | `Expected regular expression 'Synthetic \[2Jtitle bell 31m' to match …`: the work item row still held ESC, BEL and the C1 CSI | Failed 1 of 1 selected |

R-7 also passed on the final tree under code page 850. Every regression test passes on the
final tree, in the final gate below. The quality pass recorded the same failures when it wrote
each test first; section 10.1 of the plan holds them.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| R-16: `.claude/settings.json` denies `.env` and `.env.*` but not every `.env*` name that `AGENTS.md` and `tools/dev.ps1` treat as secret | Permission configuration, proposed by a review agent: the developer decides. The boundary in `AGENTS.md` holds without it |
| The regression test of R-7 fails on the 0.10.0 source only when the console's output code page is not UTF-8 | Observed in the pre-fix runs above. Under a UTF-8 console the 0.10.0 code decoded ripgrep's output correctly, so on such a computer the test would not catch a return of the defect. Forcing the code page inside the test would change the console that the gate's processes share with the developer's terminal |
| A malformed body keeps the transport message | R-3 stops the retries and marks the error not retryable, but `AdoRequestException` still says that the server could not be reached or the connection was lost. A message of its own would be a new catalog key in both languages |
| Two tests cannot fail when the inactivity watchdog breaks: `BuildLogServiceTests…(total: false)` and `CancellationTests.DownloadsEnforceInactivityAndTotalBudgets(false)`; `tools/tests/LiveAudit.Tests.ps1:295` passes on empty output | Found while removing tests, outside the scope of the pass; listed in section 5.4 of the plan |
| Golden-pinned failed-test markup tests (D28–D55 and eight more), T1, T9 and four theory rows | Not removed: each needs a mutation run of its own, or its run did not fail it. Section 5.4 of the plan lists them as proposals |
| `ErrorTextTests` 200 × 14 is still the slowest Core test, about 9 s en-US and 6 s fr-CA in the suite | Rendering and validating at the documented scale is its purpose; only a faster renderer or validator would halve it. A pipe between the two would give at best 1.5× |
| `Verify.Tests.ps1`, the timed-out stage, still takes about 8 s | Halving the stage limit would leave its idle margin at 2.5 s while every tooling file runs at once: a faster but flaky gate |
| The findings of 0.10.0, 0.9.15, 0.9.10, 0.9.5, 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; the quality pass found each one documented or decided. See the [0.10.0](archive/release-0.10.0.md#findings-not-fixed), [0.9.15](archive/release-0.9.15.md#findings-not-fixed), [0.9.10](archive/release-0.9.10.md#findings-not-fixed), [0.9.5](archive/release-0.9.5.md#findings-not-fixed), [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Only the default table views clean control characters | `Format-List`, `Select-Object`, `Out-String` of a property and string interpolation show server text as it was sent: the user asked for the value. The server text that an error message quotes was already cleaned by `ErrorTranslator`. A view that a user defines is the user's |
| R-5 may never occur | Whether Server 2020 lists a sub-result's attachment in its result's list too is V-36, still owed. The fix needs no server: an attempt without a repeat renders as in 0.10.0 |
| V-37 and the Excel check | Owed since 0.9.15 and 0.10.0 and unchanged by this version, apart from R-9, which makes the V-37 check sound on a build with more than 200 bugs |
| Limits carried over | The [0.10.0](archive/release-0.10.0.md#known-limitations), [0.9.15](archive/release-0.9.15.md#known-limitations), [0.9.10](archive/release-0.9.10.md#known-limitations), [0.9.5](archive/release-0.9.5.md#known-limitations), [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply, with V-01, V-02, V-03, V-27, V-28 and V-31 to V-37 |

## Work-PC Live checks

Install the 0.10.5 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. 0.10.5 owes no new `V-nn` check.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `tests/Live/TestFailures.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, and `ADOTOOLKIT_LIVE_ASSIGNED_BUG_ID` set to a bug known to have an assignee | V-37, as in check 1 of the [0.10.0 notes](archive/release-0.10.0.md#work-pc-live-checks): `PASS V-37 BUG_FIELDS_PRESENT_AND_SHAPED`. The check now reads the bugs in batches of 200, so a build with more bugs gives a verdict too |
| 2 | `tests/Live/Smoke.Live.ps1` with the usual inputs, and `tests/Live/Probes.Live.ps1` | Smoke passes as before; a profile without a default project now gives `INCONCLUSIVE SMOKE-2 PROFILE_DEFAULT_PROJECT_REQUIRED` and exit 2. The V-36 probe says whether a result's list repeats a sub-result's attachment, the case of R-5; if it does, a failed-test report of that build is written and lists both entries |
| 3 | In Windows Terminal, `Get-AdoWorkItem`, `Get-AdoBuild` and `Get-AdoBuildTestFailure` with their default tables | The tables look as in 0.10.0: the same columns, widths and alignment, with the text of the server. Only a control character would show as a space |
| 4 | Checks 2 and 3 of the [0.10.0 notes](archive/release-0.10.0.md#work-pc-live-checks), with the 0.10.5 package, and the Excel check described there | The report and the CSV file of a build with open bugs, the installation, and the earlier checks: the `S0-9` verdict of `tests/Live/Connection.Live.ps1`, V-33, `-SkipAttachments`, the probes of V-28, V-34 and V-35, and V-31, V-32, V-02, V-01 and V-03. All still pending |

## Local validation and developer handoff

Run on 2026-10-05 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0 with no warning, the product gate running beside the in-process stages. All seven stages pass: 71 PowerShell files linted, 331 tooling Pester tests, 147 configuration files, 88 Markdown files with 309 links and 308 path references, 4 hook helpers, 5 skills and 3 subagents in the layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.10.5 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with no warning, with the exact module pins in `tools/BuildModules.psd1` — Pester 5.9.1, PSScriptAnalyzer 1.25.0 and Microsoft.PowerShell.PlatyPS 1.0.3 — and the same counts. The variable was set for that process only and was never set in the shell |
| Core tests in the final gate | 1565 passed under en-US and 1423 under fr-CA, with no failures or skips: 23 and 16 fewer than 0.10.0, the removals and merges less the seven regression tests |
| Product Pester tests against the staged module | 181 passed, none skipped, for 183 in 0.10.0. The tooling tests are 331, for 339 |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.10.5` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.10.5` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/`. The archive's SHA-256 was checked first against the published `hashes.sha256` and the setup action's `POWERSHELL_SHA256`: all three agree |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.10.5-win-x64.zip -ModuleVersion 0.10.5 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.10.5, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.10.5/`. The portable ZIP has the same seven under `module/`, both launchers, `README.txt` and `bundle.json` (module 0.10.5, PowerShell 7.6.6, win-x64, PowerShell SHA-256 `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860`, equal to the published hash and to the pin of the setup action), 669 entries in all. Each `.sha256` file holds the hash of its ZIP, and the installer is byte-identical to its source |
| `tools/package/Install-AdoToolkit.ps1 -Path ./artifacts/release/AdoToolkit-0.10.5.zip -WhatIf` | Exit 0: the checksum and the layout are accepted and the target is named, `Documents\PowerShell\Modules\AdoToolkit\0.10.5`. Nothing was written, and no module was installed |
| `pwsh -NoProfile -File .\tools\dev.ps1 diagnose` | `ok`, with every required tool present and both recommended tools present: ripgrep 15.2.0 and `gh` 2.102.0. The handoff below therefore opens the pull request with `gh` |
| The pre-fix runs | The 13 runs of [Pre-fix failures](#pre-fix-failures), before the release edits. Afterwards `git diff --shortstat` against `68579c7` showed the tree as before them: 70 files, 690 insertions and 1316 deletions |

No test failed in either gate run, so nothing is recorded as a flake.

No Live script ran: V-37, the Excel check and every earlier check are the developer's. Not
run: an actual installation, the workflows, which only GitHub runs, and the squash merge,
which happens on GitHub after these notes. The quality pass ran `repo-review` before these
notes; the release did not run it again.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.10.5-win-x64.zip` | 110433272 | `655f985d00a28ff69a841d2e160fc1b6848dd8a75f94ad5ee836c959b27a8704` |
| `AdoToolkit-0.10.5.zip` | 826371 | `77f3c466f0d5e6f9081a4233e1f7bdc0f1a82c8b96a50462997bcfe357f71f21` |
| `Install-AdoToolkit.ps1` | 13567 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag `v0.10.5` exists. The branch `0.10.5` has no
upstream and no commit of its own; every change is uncommitted. The developer owns
publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.10.5`
   and pushes the branch and the tag together, which publishes the release at once, and then
   opens the pull request with its final title, falling back to the prefilled compare page if
   `gh pr create` cannot.
2. Run the second command. It waits for the Verify check of the pull request and opens the
   page in a browser once that check passes.
3. Merge with Squash and merge and keep the commit title that GitHub proposes: the title of
   the pull request, then its number.
4. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. If the run does not publish, `docs/tooling.md`
   lists the recovery for each cause.
5. Run the work-PC checks above with the published package, V-37 first, and the Excel check
   of the 0.10.0 notes, and record any inconclusive coverage.
6. Once the pull request has closed, start the next branch from the updated `main` with the
   command line the handoff gives: it fetches the merge, creates the branch without tracking
   and sets its upstream.
