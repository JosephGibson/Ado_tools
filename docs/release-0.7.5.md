# AdoToolkit 0.7.5 release notes

Prepared on 2026-10-02 from the two commits on the branch `0.7.5`, which starts at `main`
after the pull request of 0.7.0 (tag `v0.7.0`): `8a7166f`, a repository cleanup and QA pass,
and `d73f16e`, which fixes quoted completion and path validation in the tooling. The release
commit adds the version, these notes and the changelog. 0.7.5 is a maintenance release: it
fixes the defects of that audit, corrects the French spacing, the help and the guides, and
hardens the tooling. It adds no feature. This is an offline review. No Azure DevOps Server
connection or `tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed
live. The areas the README lists as confirmed are still connections, projects, builds and
test runs.

## What changed

0.7.5 adds no cmdlet, no parameter and no configuration key. The configuration file format,
the JSON report schema and the seven-file package layout are unchanged. The
[0.7.0 notes](archive/release-0.7.0.md) still describe the failed-test report and the concurrent
requests. The IDs B1 to B13 and T1 to T13 point to the table under Bug fixes.

### Cmdlets and Core

| Area | 0.7.5 behavior |
| --- | --- |
| Transport failures | A request that no server answered, because the network failed or the connection was lost, says "The server could not be reached, or the connection was lost." and has no status code. 0.7.0 said "The server rejected the request." The error ID stays `AdoRequest` (B1) |
| Server error text | A server message cut inside a surrogate pair no longer escapes as a .NET error; the HTTP status decides the error, as for any unreadable message (B2) |
| Files | A temporary file that cannot be removed after a write is reported as a file output error: for the reports, the configuration file and the downloads alike (B3) |
| Local input checks | `Get-AdoWorkItem -Field` and `Invoke-AdoWiql -Query` refuse a value of whitespace only at parameter binding, before any request (B10). A hand-made connection whose collection URL is relative or not `http` or `https` is reported as an incomplete object, as one without a URL already was (B9) |
| `-Definition` | Accepts an integer of any type, so an ID that `ConvertFrom-Json` read as `Int64` works; a value outside 1 to 2,147,483,647 is still `AdoRequest` (B11) |
| Completion | Project and profile names complete from a prefix in single or double quotes, with or without the closing quote (B12) |
| Test Case reports | The text of a step converts when a link is closed by the end of a table cell (B8) |
| Failed-test retrieval | A custom field value that cannot be read, such as an object that repeats a property, is left out like an absent one instead of failing the result (B7) |
| French messages | A no-break space precedes the colon in 12 catalog messages: `BuildLogTailUnknown`, `BuildLogNotAvailable`, `BuildLogDirectoryRequired`, `InvalidConfiguration`, `UnknownConfiguration`, `FileOutput`, `Redirect`, `PageLog`, `ProgressEnumeration`, `ProgressFetch`, `ProgressExpansion` and `BugMetadataUnavailable` (B13) |

### Failed-test report

| Area | 0.7.5 behavior |
| --- | --- |
| Bug links | An attempt links a bug through the project of the bug, not the project of the build, so a bug of another project opens correctly (B6) |
| Expand all, Collapse all | Work from a history cell, which shows the Runs view and leaves the address on `#details`: the Details view is shown although the address does not change (B5) |
| Dates | A time at the limit of the calendar keeps its own offset instead of failing, in the report and in the history chart. The Test Case report already did; all three use one function (B4) |

Only `src/AdoToolkit.Core/Reporting/Assets/test-failures.js` changed among the report
assets, by the fix above. A golden holds a placeholder in place of the script, so none of the
50 report goldens changed.

### Installer and packaging

| Area | 0.7.5 behavior |
| --- | --- |
| `tools/package/Install-AdoToolkit.ps1` | When the move of the new copy and the rollback both fail, the previous copy stays as the backup instead of being deleted (T8). A blank entry in `PSModulePath` no longer stops the report of where the module was installed (T9). Its examples name the 0.7.5 zip. The script ships beside the module zip |
| Package scripts | `Move-AdoPackageDirectory` keeps its backup when the rollback fails (T8). `Assert-AdoPackage` resolves its path. Every `dotnet` command of `tools/package/Publish-AdoToolkitPackage.ps1` runs in the repository, so `global.json` selects the SDK wherever the caller stands. A `Directory.Build.props` without `VersionPrefix` is reported by name by `tools/package/New-AdoToolkitRelease.ps1` and `tools/check.ps1` (T10). `tools/package/Install-AdoToolkitPackage.ps1` and `tools/package/Set-AdoToolkitPackageSignature.ps1` require PowerShell 7.6 |

### Help and guides

| Area | Change |
| --- | --- |
| Help topics | `ms.date` is 10-02-2026 in the 13 English and 21 French topics that changed. `Get-AdoBuildFailure` says that a canceled build also returns its deepest canceled records, as it does; `Export-AdoBuildTestFailure` no longer says that an unreadable history build does not stop it, and names V-33; completion help names both quote styles; `Remove-AdoProfile` describes `-WhatIf`; the build topics cite the right `V-nn` items (V-11, V-14, V-15, V-26). The French topics follow: spaces before colons, prose in step with the English, a link to `about_CommonParameters` |
| Guides | `build-report.md`, `configuration.md`, `getting-started.md`, `pipeline-triage.md` and `test-case-reports.md` mark unconfirmed server behavior with its `V-nn` item (V-14, V-19, V-27, V-30), state the expansion threshold of a Test Case, the default file names, the paged detail requests, the configuration reads and the errors by their IDs, use synthetic hosts, and name the 0.7.5 files |
| Concurrency | `configuration.md` no longer says that the result is the same at every value of `testResults.maximumConcurrentRequests`; see Findings not fixed |
| Release notes | The 0.6.5 notes moved to `docs/archive/`, whose index lists them. The 0.7.0 notes are those of the tag, with one corrected row, "Result order", and the repointed links |

### Tooling and live scripts

| Area | Change |
| --- | --- |
| Discovery | Every `.env*` name is excluded, as a file and as a directory, by ripgrep and by the filesystem walk alike (T1). A documentation path or link that leaves the repository, names an excluded or sensitive path, or goes through a junction is reported as missing, without being read (T2). `context -Path` scopes an absolute path as the same relative path (T3). `inspect` lists instruction files as an array for one file and for none (T7) |
| `verify` | An unknown stage name is rejected in a repository that has no stage (T4). External stages exchange UTF-8 whatever the console code page (T5). The count of failing tests that are not listed leaves out the container errors (T6) |
| Hooks | `tools/guard-git.ps1` also checks `exec`, `eval`, `then`, `elif` and command substitutions (`$(…)` and backticks), and treats what follows `-File` as data (T11). `tools/validate-edit.ps1` reports tooling that does not load, with exit code 2, instead of leaving an edit unchecked (T12) |
| Live scripts | `Get-AdoLivePendingCheck` in `tests/Live/Live.Common.ps1` keeps a verdict that was already printed; `tests/Live/TestCase.Live.ps1` prints `FAIL … CHECK_FAILED` only for a check without a verdict. `tests/Live/TestFailures.Live.ps1` names a missing build or attachment field as the failure of V-25 and V-23 instead of failing on its absence (T13). Unused variables were removed from the scripts |
| Instructions | `AGENTS.md`, the nested `AGENTS.md` files, the four skills and their Claude wrappers are shorter; `docs/tooling.md` follows the tooling. The comments of `PSScriptAnalyzerSettings.psd1` say why each rule is off |
| Dead code | Removed: the public `CollectionUrlNormalizer.Normalize(string)` and `ReportFileNames.Resolve(string?, int, ReportFormat, …)` overloads, `AdoHttpPipeline.DownloadAsync`, `OutcomeClassifier.IsNonAttemptGroup`, `TestResultRecord.StartedDate` and the constructors of `AttachmentLimitException`. The fixture `tests/Fixtures/Attachments/test-output.html`, which no test read, is deleted |

### Visible changes for 0.7.0 users

- Existing configuration files need no change.
- Public contract, messages: the text of a transport failure changed (B1). A script that
  matched "The server rejected the request." for a connection that failed must test the
  error ID `AdoRequest` and the missing status code instead. French messages and help
  have no-break spaces before colons (B13); a script must not match their text.
- Public contract, parameters: `Get-AdoWorkItem -Field` and `Invoke-AdoWiql -Query` fail with
  `ParameterArgumentValidationError` for a whitespace-only value (B10), and `-Definition`
  accepts more integer types (B11). A hand-made connection with a relative or non-HTTP
  collection URL is refused with a configuration error (B9).
- Public contract, output: no type, property or format changed. Where B7 applies, the
  attempt has no entry for the unreadable custom field.
- The failed-test report links the bug of an attempt through the project of the bug (B6).
- Core types, for code that uses the assembly directly: the two public overloads named under
  Dead code are gone, and `AdoMessage` has the new member `Transport`.
- `Install-AdoToolkit.ps1` differs from that of 0.7.0 in the two ways above and in its
  examples; both fixes matter only when an installation fails or `PSModulePath` is odd.

## Bug fixes

B1 to B13 are defects of the product; T1 to T13 are defects of the tooling. They were found
by the audit of the commit `8a7166f`, whose message records that each was fixed test-first,
and by `d73f16e`. For these notes the tests were not run again against the 0.7.0 source.
Three fixes carry a failure before the fix, recorded by the earlier QA notes on the 0.7.0
page: B12, T1 and T2 (completion with double quotes returned nothing; two `.env*` cases
failed on the safety predicate; the old validator reported nothing for an excluded path or a
junction). For the others, the tests exist in the final tree and pass, which shows that the
fix holds and not that the test failed before it.

| ID | Finding, cause and fix | Regression test | Files |
| --- | --- | --- | --- |
| B1 | A transport failure used the message of a rejected request. The pipeline now uses the new message `Transport`, in both catalogs | `RetryPolicyTests`: the cases that end in failure assert the message and no status code | `src/AdoToolkit.Core/Http/AdoHttpPipeline.cs`, `src/AdoToolkit.Core/Resources/AdoMessage.cs`, both `Strings*.resx` |
| B2 | A server message with half a surrogate pair raised `InvalidOperationException` from the reader of the message. The translator treats that, `JsonException` and a regular-expression timeout as "no message" | `ErrorTranslationTests.MissingOrMalformedErrorFieldsDoNotHideKnownStatus`, new row `abc\ud83d` | `src/AdoToolkit.Core/Http/ErrorTranslator.cs` |
| B3 | `AtomicFileWriter.Write` and `AtomicFileReplace.Write` removed a leftover temporary file with a plain `File.Delete` in `finally`. A file that could not be deleted raised a raw I/O error that replaced the file output error being reported. `WriteAsync` already guarded it; all three now share `AtomicFileReplace.DeleteTemporary` | `AtomicFileWriterTests.ALockedTemporaryFileStillReportsAFileOutputError` and `…OfADirectReplaceStillReportsAFileOutputError` | `src/AdoToolkit.Core/IO/AtomicFileReplace.cs`, `src/AdoToolkit.Core/IO/AtomicFileWriter.cs` |
| B4 | `ToOffset` throws for a time at the limit of the calendar; the failed-test report and the chart called it unguarded. `ReportTime.InOffset` guards it and the three renderers use it | `ReportTimeTests.ATimeAtTheLimitOfTheCalendarKeepsItsOwnOffset`, `RunHistoryChartTests.AFinishTimeAtTheLimitOfTheCalendarKeepsItsOwnOffset` | `src/AdoToolkit.Core/Reporting/ReportTime.cs`, `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs`, `src/AdoToolkit.Core/Reporting/Charts/RunHistoryChart.cs`, `src/AdoToolkit.Core/Reporting/Html/HtmlTestCaseRenderer.cs` |
| B5 | **Expand all** from a history cell did not switch to the Details view: the Runs view leaves the address on `#details`, so setting it raised no event. The script calls `show('details')` when the address already names it | `RunsViewTests.ExpandAllShowsTheDetailsViewWhenTheHashAlreadyNamesIt`. It reads the script text; no browser ran it | `src/AdoToolkit.Core/Reporting/Assets/test-failures.js` |
| B6 | An attempt built the link of its bug with the project of the build. It uses `TeamProject` of the bug, then the project of the build | `BugsViewTests.AnAttemptLinksItsBugThroughTheProjectOfTheBug` | `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` |
| B7 | A custom field value that cannot be mapped threw out of the attempt mapper. It is skipped like an absent value (F07) | `TestAttemptMapperTests.ACustomFieldThatCannotBeMappedIsLeftOutWithoutFailingTheResult` | `src/AdoToolkit.Core/TestRuns/TestAttemptMapper.cs` |
| B8 | After a table cell closed inside a link, the converter read the link label from a position that the cell had trimmed away. The start is clamped to the output | `PlainTextConverterTests`, new row with a link closed by its cell | `src/AdoToolkit.Core/RichText/PlainTextConverter.cs` |
| B9 | A hand-made connection with a relative or non-HTTP collection URL passed `EnsureInput`. It is reported as missing `CollectionUri` | `InputGuard.Pester.ps1`, two new rows (`Collection`, `ftp://…`) | `src/AdoToolkit.PowerShell/Commands/AdoCmdletBase.cs` |
| B10 | `ValidateNotNullOrEmpty` accepted a value of spaces. Both parameters use `ValidateNotNullOrWhiteSpace` | `WorkItems.Pester.ps1` and `TestPlans.Pester.ps1`: the error ID is `ParameterArgumentValidationError` and no request is sent | `src/AdoToolkit.PowerShell/Commands/GetAdoWorkItemCommand.cs`, `src/AdoToolkit.PowerShell/Commands/InvokeAdoWiqlCommand.cs` |
| B11 | `-Definition` took only `Int32`. An integer of any type from 1 to `Int32.MaxValue` is a definition ID | `Builds.Pester.ps1`, "accepts a definition ID of another integer type", with an `Int64` and an out-of-range value | `src/AdoToolkit.PowerShell/Commands/AdoCmdletBase.cs` |
| B12 | The completers stripped single quotes only. `NameCompletion.PrefixPattern` removes one surrounding pair of either kind and keeps quotes that belong to the name | `Profiles.Pester.ps1` and `Projects.Pester.ps1`, four prefixes each | `src/AdoToolkit.PowerShell/Completion/NameCompletion.cs`, `src/AdoToolkit.PowerShell/Completion/ProfileNameCompleter.cs`, `src/AdoToolkit.PowerShell/Completion/ProjectNameCompleter.cs` |
| B13 | 12 French messages had an ordinary space before a colon, which the rule in `src/AGENTS.md` forbids; the test checked two other rules only | `FrenchTerminologyTests.ANoBreakSpacePrecedesAColon`, for the catalog and the help sources | `src/AdoToolkit.Core/Resources/Strings.fr.resx`, `docs/commands/fr-CA/` |
| T1 | `.envrc` and `.environment` were not excluded from discovery. The patterns are built from one list | `tools/tests/Dev.Tests.ps1`, "excludes every .env prefix in both discovery implementations" | `tools/dev.ps1`, `tools/lib/discovery.ps1` |
| T2 | The documentation check accepted excluded paths and junction targets without an anchor. `Test-IsSafeRepositoryTarget` checks each component | `tools/tests/Documentation.Tests.ps1`, four tests | `tools/lib/discovery.ps1`, `tools/lib/validation.ps1` |
| T3 | `context -Path` appended an absolute path to the root | `tools/tests/Workflow.Tests.ps1`, "scopes instructions for an absolute path…" | `tools/lib/discovery.ps1` |
| T4 | `$plan.Name` of an empty plan raised a strict-mode error instead of naming the unknown stage | `tools/tests/Workflow.Tests.ps1`, "rejects an unknown stage name in a repository that has no stage" | `tools/lib/validation.ps1` |
| T5 | External stages used the console code page, so non-ASCII output and arguments changed | `tools/tests/Workflow.Tests.ps1`, "returns non-ASCII output and arguments…" | `tools/lib/validation.ps1` |
| T6 | "… and n further failing tests" counted the container errors already in the list | `tools/tests/Workflow.Tests.ps1`, "counts the failing tests it does not list…" | `tools/lib/validation.ps1` |
| T7 | `inspect` printed one instruction file as a string and none as `null` | `tools/tests/Dev.Tests.ps1`, "lists the instruction files of an inspection as an array…" | `tools/lib/discovery.ps1` |
| T8 | A failed rollback deleted the only old copy: the flag that protects the backup was cleared after the move, so the cleanup ran when the move threw | `tools/tests/Package.Tests.ps1`, "keeps the previous package when the commit and its rollback both fail" | `tools/package/Package.Common.ps1`, `tools/package/Install-AdoToolkit.ps1`. The installer has no test of its own |
| T9 | A blank `PSModulePath` entry stopped the installer: the filter kept it and the path conversion rejected it | `tools/tests/Package.Tests.ps1`, "reports the installation when PSModulePath holds a blank entry" | `tools/package/Install-AdoToolkit.ps1` |
| T10 | A `Directory.Build.props` without `VersionPrefix` failed on a missing node instead of naming it; `tools/package/Publish-AdoToolkitPackage.ps1` ran `dotnet` where the caller stood | `tools/tests/Check.Tests.ps1` and `tools/tests/Package.Tests.ps1`: "names VersionPrefix…", "runs every dotnet command in the repository…" | `tools/check.ps1`, `tools/package/New-AdoToolkitRelease.ps1`, `tools/package/Publish-AdoToolkitPackage.ps1` |
| T11 | The git guard did not look inside `eval`, `exec`, `then`, `elif`, `$(…)` or backticks, and read the arguments of `-File` as a command | `tools/tests/Dev.Tests.ps1`, seven new blocked and three new allowed command lines | `tools/guard-git.ps1` |
| T12 | A syntax error in the tooling made `tools/validate-edit.ps1` exit with a code that the host ignores, so no edit was checked | `tools/tests/Dev.Tests.ps1`, "reports tooling that does not load…" | `tools/validate-edit.ps1` |
| T13 | `TestFailures.Live.ps1` failed on a missing build or attachment field instead of naming it, and `TestCase.Live.ps1` printed a second verdict for a check that already had one | `tools/tests/LiveAudit.Tests.ps1`, four tests | `tests/Live/Live.Common.ps1`, `tests/Live/TestCase.Live.ps1`, `tests/Live/TestFailures.Live.ps1` |

Also in `8a7166f`, without a test of their own: a missing route value in
`src/AdoToolkit.Core/Http/RequestBuilder.cs` is an `ArgumentException`, like an unsafe one,
not a `KeyNotFoundException`; `tools/check.ps1` takes the first `dotnet` on `PATH`; comments
that no longer matched the code were corrected.

### Existing tests changed

| Test | Change |
| --- | --- |
| `CancellationTests` | The download tests use `DownloadStreamAsync`, because `DownloadAsync` was removed. The slow-body case drips 12 reads of 20 ms against an inactivity limit of 200 ms, so that it no longer depends on the scheduler |
| `RetryPolicyTests` | The failing cases assert the new message and the missing status code |
| `ReportFileNamesTests` | Call the remaining overload with `ReportFileNames.TestCase` |
| `IdChunkingTests`, `WiqlServiceTests` | Read the work item fixture once, not for each item |
| `RetrievalCancellationTests` | Dispose the handler with `using` |
| `FrenchTerminologyTests`, `SinkEncodingTests` | The new rule leaves out two catalog keys that goldens hold (see Findings not fixed); one test is renamed for what it pins |
| `TestRuns.Pester.ps1` | The two runs start two hours before the clock, so that the default attachment window holds them. The test had passed `-AttachmentWindowDays 365` for fixture dates of 2026-09-14, which would have aged out |
| `Export.Pester.ps1`, `TestFailureExport.Pester.ps1`, `TestRuns.Pester.ps1` | The provenance checks expect the error ID `AdoConnectionMismatch`, not any error; the Markdown export passes `-Culture en-US`; `BuildLogs.Pester.ps1` writes to a fresh folder |
| `Dev.Tests.ps1`, `Package.Tests.ps1` | The lint stage is mocked where it is not the subject; the pinned Pester version is read from `tools/BuildModules.psd1`; the `-WhatIf` tests no longer mock commands that they never reach |
| Fixtures | `tests/Fixtures/README.md` follows the deleted file and the helpers that read each folder. No golden changed |

### Findings not fixed

| Finding | Reason |
| --- | --- |
| Near the history budget, cancelled requests can change the budget left for older builds | Carried from 0.7.0. The 0.7.0 notes, the changelog and `docs/guides/configuration.md` now say so. Independent budgeting needs a design decision |
| `RichTextImageAlt` and `PartialTestCase` keep an ordinary space before a colon in French | Rendered reports hold them; they change only through the `update-goldens` skill, with the French wording reviewed. `FrenchTerminologyTests` lists them |
| B5 was not run in a browser | The test reads the script text. Check 3 below does it |
| The rollback of `Install-AdoToolkit.ps1` (T8) has no test of its own | The logic is that of `Move-AdoPackageDirectory`, which has one. Forcing a failed rename in the standalone script needs a seam that it does not have |
| The findings of 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Concurrent requests (V-33) | Unconfirmed at work, as in the [0.7.0 notes](archive/release-0.7.0.md#known-limitations). If Server 2020 refuses or delays several requests, set `testResults.maximumConcurrentRequests` to `1` |
| Server behavior behind V-14, V-19, V-27 and V-30 | The guides now say that these are unconfirmed. This release confirmed or contradicted none of them |
| Limits carried over | The [0.7.0](archive/release-0.7.0.md#known-limitations), [0.6.5](archive/release-0.6.5.md#known-limitations) and [0.6.0](archive/release-0.6.0.md#known-limitations) known limitations still apply, with V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.7.5 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.7.5, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.7.5 with seven files in its folder |
| 2 | V-33, as in rank 2 of the [0.7.0 checks](archive/release-0.7.0.md#work-pc-live-checks): the same build with the default configuration and with `maximumConcurrentRequests` at `1` | Equal counts, `Status` and diagnostic codes, and no refused or throttled request. Report the error ID, the counts and the two durations only |
| 3 | `$set \| Export-AdoBuildTestFailure -Open` for a build whose tests link a bug of another project, in the browser used at work; open a history cell, then click **Expand all** | The bug link of an attempt opens the bug in its own project. **Expand all** shows the Details view with the cards opened. No Content Security Policy error in the console |
| 4 | `TestFailures.Live.ps1` and `TestCase.Live.ps1` with the 0.7.5 package | One verdict per check ID, none printed twice. The verdicts of V-19 to V-25 and V-30, and of V-01, V-02, V-03, V-05, V-10 and V-13, as before. A missing field is named in a `FAIL` line, never a crash |
| 5 | At the prompt, `Connect-Ado -Profile "<first letters of a real profile>` and `Connect-Ado -Project "<first letters of a project>`, then Tab, after `Get-AdoProject` | Each completes to a quoted, pasteable name, and no request is sent |
| 6 | Run `Get-AdoProject` with the PC off the network, or the proxy refusing the connection | The message says that the server could not be reached or the connection was lost, with error ID `AdoRequest` and no status code. A proxy that answers with its own HTTP error is another case and keeps its status |
| 7 | Checks 2 to 9 of the [0.6.0 notes](archive/release-0.6.0.md#work-pc-live-checks) with the 0.7.5 package | As listed there: V-31 and V-32, V-02, V-01, V-03, the `-Open` warning, custom fields, Markdown line breaks and the 0.5.0 checks. All still pending |

## Local validation and developer handoff

Run on 2026-10-02 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify` | Exit 0. All seven stages pass without a warning: 61 PowerShell files linted, 276 tooling Pester tests, 147 configuration files, 82 Markdown files with 219 links and 203 path references, the hook and skill layout, 2 workflow files linted with actionlint, and the product check |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1`. The previous state of the variable was restored afterwards |
| Core tests in the final gate | 1392 passed under en-US and 1392 under fr-CA, with no failures or skips |
| Product Pester tests against the staged module | 160 passed, none skipped |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.7.5` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.7.5` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.7.5-win-x64.zip -ModuleVersion 0.7.5 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.7.5, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.7.5/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt` and `bundle.json` (module 0.7.5, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the pin), 669 files in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |

The tests of this release were not run against the 0.7.0 source; see Bug fixes. No browser
opened a report, and no Live script ran. Not run: the installer against the 0.7.5 ZIP, and
the workflows, which only GitHub runs.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.7.5-win-x64.zip` | 110356701 | `938d6e2111382d95ca69ecf01b3a57f0d1bcc7ce092367f4125ba1e3eb5c8d55` |
| `AdoToolkit-0.7.5.zip` | 749787 | `7085a6120400a20e5f179cdd80e0610c45ac828a835e6e3897eaff92de08bb09` |
| `Install-AdoToolkit.ps1` | 13032 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. The developer owns publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.7.5`
   and pushes the branch and the tag together, which publishes the release at once.
2. Run the second command and create the pull request on the page it opens. Merge it when
   the Verify check passes.
3. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. If the run does not publish, `docs/tooling.md`
   lists the recovery for each cause.
4. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
