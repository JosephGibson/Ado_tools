# AdoToolkit 0.6.0 release notes

Prepared on 2026-10-01 from the pending changes in `src/`, `tests/`, `tools/`, `docs/` and
the agent setup. This is an offline review. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed live. The areas
the README lists as confirmed are still connections, projects, builds and test runs.

## What changed

0.6.0 adds no cmdlet and no parameter. It fixes the defects found in a review of the whole
repository, removes what was left of the project template in the tooling, and reorganizes
the developer documentation. The configuration file format and the JSON report schema are
unchanged.

### Cmdlets and reports

| Area | 0.6.0 behavior |
| --- | --- |
| `-Open` | When the default application cannot be started, `Export-AdoTestCase` and `Export-AdoBuildTestFailure` write a warning that names the report and the reason, and still return the `FileInfo`. In 0.5.0 the launch error escaped after the report was written, so the command failed and returned nothing |
| Incomplete pipeline input | An object that no AdoToolkit command returned and that lacks a required property, such as `[pscustomobject]@{ Id = 44 } \| Get-AdoBuild`, gets one `AdoRequest` error for that input: `The AdoBuildDefinition input is incomplete: Name has no valid value. Pipe an object returned by an AdoToolkit command.` The rest of the input is processed and no request is sent for the refused object. This applies to `Get-AdoBuild`, `Get-AdoTestSuite`, `Get-AdoTestCase`, `Get-AdoBuildFailure`, `Get-AdoBuildTimeline`, `Get-AdoTestRun`, `Get-AdoBuildTestFailure`, `Export-AdoTestCase`, `Export-AdoBuildTestFailure` and `Remove-AdoProfile` |
| `-Connection` | A connection object without a collection URI, or with a request timeout outside 1–86400 seconds, is an `AdoConfiguration` error before any request |
| Downloads folder | Every failure to locate the Downloads folder, for example a folder redirected to an absent drive, is the localized file-output error. Some failures used to surface as a raw .NET exception |
| Markdown Test Case report | A line break inside a step's action or expected result is kept, as `<br>`. `~` and a line that starts with `=` are escaped, so step text cannot strike text through, open a code fence or turn the line above into a heading |
| Custom fields in the failed-test report | A list, an object or an identity is shown as its content: compact JSON, or `Name <unique name>` for an identity. 0.5.0 showed the .NET type name |
| `ShortName` and `ClassName` | A test without an automated name shows its whole title, and an automated name that is free text is not cut at its last dot: `Checkout v2.0 smoke` stays whole. Qualified names such as `Tests.Cart.AddsItem` are shortened as before |
| Stack traces | Constructor frames (`Type..ctor`, `Type..cctor`) keep their type and method in the highlighted trace |
| Run history | The progress record reaches its total. When several piped builds share an earlier build that could not be read, every set gets the `HistoryUnavailable` diagnostic, not only the first |
| French text | `build` is masculine in five messages (`Le build 401 n’a aucune série de tests.`), and a test run is « série de tests » in the report labels, the attachment warnings and the help. « Exécution » remains the word for a pipeline run |
| `Set-AdoProfile -DefaultProject ''` | Removes the default project, like the other text defaults. 0.5.0 stored an empty string |
| Wildcard metadata | `-Name` of `Get-AdoProfile` and `Get-AdoProject` is declared as accepting wildcards, which it always did |
| Identity text | An `AdoIdentityRef` converts to `Display Name <unique name>` instead of its type name, for example in a table column |

### Package

| Area | 0.6.0 behavior |
| --- | --- |
| Layout | The module is seven files. `fr/AdoToolkit.PowerShell.resources.dll` is gone: every message now comes from the Core string catalog, and the PowerShell project's own catalog, which repeated 32 Core strings and read four of them, was removed |
| Installer | `Install-AdoToolkit.ps1` accepts exactly the layout of its own release. Use the installer that ships with the zip |
| Release workflow | `actions/checkout` and `actions/setup-dotnet` are pinned by commit, with the version in a comment |

### Help and guides

| Area | Change |
| --- | --- |
| Corrected facts | `Get-AdoTestRun` orders runs by stage, phase and job attempt; `Disconnect-Ado` also clears the test category cache; `Get-AdoBuildTestFailure` caches the test results of earlier builds, not the history listing; `Export-AdoBuildTestFailure` creates a missing directory; `Set-AdoProfile` documents how to remove the default project; both exports document the `-Open` warning; `Disconnect-Ado` and `Remove-AdoProfile` say that they produce no output, and the pipeline inputs of `Remove-AdoProfile` and `Test-AdoConnection` are described as inputs |
| Parameter metadata | `SupportsWildcards` is `true` for the two `-Name` parameters above, `-WhatIf` and `-Confirm` list their aliases in every topic, and nullable types use one spelling |
| Related links | The connection and profile topics link to each other instead of to themselves |
| French topics | Examples are headed « Exemple », the `-WhatIf` and `-Confirm` descriptions of `Set-AdoProfile` and `Remove-AdoProfile` are translated, and the terms above are applied |
| Guides | `getting-started.md`, `build-report.md`, `pipeline-triage.md`, `test-case-reports.md` and `configuration.md` describe the changed behavior |

### Developer tooling and agent setup

| Area | Change |
| --- | --- |
| `tools/dev.ps1` | The `init` command, the project markers it replaced and the Node, Python, Rust, Go and Java detection are removed; the tooling describes this repository only. `bootstrap -Install` also installs PlatyPS 1.x, which the gate requires |
| `verify` | New `documentation` stage: every relative link, heading anchor and repository path in maintained Markdown must resolve; archived files and test fixtures are not checked. `tooling-layout` also requires every skill to have its body and its Claude wrapper. A repository with product code and no `tools/check.ps1` is incomplete instead of passing on the other stages |
| Product gate | The summary of `project-check` lists each step and its test counts. The package is checked with the same `Assert-AdoPackage` that packaging uses. The product Pester run reports through a file, so the advisory `What if:` warning is gone. The stage timeout is 900 seconds |
| Hooks | `tools/validate-edit.ps1` and the `configuration` stage also check `.resx` and `.ps1xml` files |
| Instructions | `AGENTS.md` is rewritten for the product. New nested files cover `tests/Live/`, `tools/package/`, `docs/` and `docs/commands/`, each with a path-scoped Claude rule. The `template-init` skill is removed; `fix-bug`, `update-goldens` and `release` are added for Claude and Codex |
| Documentation | `docs/tooling.md` and the fixture catalog are rewritten as references; the 0.2.0, 0.3.0 and 0.4.0 notes moved to `docs/archive/`, whose index now also resolves `V-30` and the audit findings `F01` to `F16` |
| Fixtures | `FixtureCatalogTests` requires one catalog entry per fixture file. Two fixtures that no test loaded for their scenario were deleted (one unused since 0.3.0, one never used), and a third got the test its scenario calls for |

### Visible changes for 0.5.0 users

- Existing configuration files need no change.
- `-Open` no longer fails an export. Scripts that relied on the error to detect a blocked
  browser must read the warning stream instead.
- A hand-made object piped into the commands listed above is refused when a required
  property is missing, even if 0.5.0 happened to accept it because the command did not read
  that property. Build the object with every property, or pipe the output of the matching
  `Get-` command.
- An incomplete `-Connection` object stops the command with an `AdoConfiguration` error.
- `Set-AdoProfile -DefaultProject ''` removes the value. A profile saved by 0.5.0 with an
  empty default project still loads.
- Markdown Test Case reports differ where a step has several lines or contains `~` or a
  leading `=`. HTML and JSON reports are byte for byte the same.
- In the failed-test report, structured custom-field values and some test names read
  differently, as described above. `FailedCount`, `FlakyCount` and classification do not
  change.
- French messages and report labels changed wording. Scripts must not match message text;
  error IDs and diagnostic codes are unchanged.
- A set built from piped builds can carry more `HistoryUnavailable` diagnostics. `Status`
  is unaffected, because history never changes it.
- The module folder has seven files instead of eight. The 0.5.0 installer refuses the 0.6.0
  zip and the reverse; each release ships its own installer. The Live scripts of this
  version need an installed 0.6.0 module.
- For developers: `tools/dev.ps1 init` no longer exists, `context` and `inspect` no longer
  return `Entrypoints`, and `verify` has a sixth stage.

## Bug fixes

Found in this review. Paths are under `src/AdoToolkit.Core/` or
`src/AdoToolkit.PowerShell/` unless they start with another folder.

| Area | Finding | Fix | Regression test | Files |
| --- | --- | --- | --- | --- |
| Downloads folder | Only `COMException` was translated. A Win32 failure, which .NET maps to `FileNotFoundException` and other types, escaped as a raw exception | Every failing result becomes `AdoFileOutputException` with the localized message | `KnownFoldersTests` (6 cases) | `IO/KnownFolders.cs` |
| `-Open` | A launch failure escaped after the report was committed: the command failed, returned no `FileInfo`, and the failed-test export left its attachment folder unreported | `DocumentOpener` turns launch failures into the new `ExportOpenFailed` warning | `TestCaseExporterTests.LaunchFailureAfterCommitIsAWarningAndStillReturnsTheFile`, `TestFailureExporterTests.LaunchFailureAfterCommitIsAWarningAndStillReturnsTheResult` | `IO/DocumentOpener.cs` (new), `Reporting/TestCaseExporter.cs`, `Reporting/TestFailures/TestFailureExporter.cs` |
| Pipeline input | PowerShell builds a parameter's type from any property bag, so an incomplete object reached the commands and ended in `ArgumentNullException` or `NullReferenceException`. This is the 0.5.0 finding that was left open | `InputGuard` finds null required members, nested objects included; `EnsureInput` and `EnsureComplete` report them with the new `IncompleteInput` message; a supplied connection is validated | `InputGuard.Pester.ps1` (13) | `Commands/Infrastructure/InputGuard.cs` (new), `Commands/AdoCmdletBase.cs` and ten cmdlets |
| Test names | `ShortName` split a Test Case title, or a free-text automated name, at its last dot or plus: `Checkout v2.0 smoke` became `0 smoke` | A title is shown whole, and a name with white space before the separator is not a qualified name | `AttemptClassificationTests.TitlesAndFreeTextNamesAreNotSplit` | `TestRuns/AttemptGrouper.cs` |
| Custom fields | List, object and identity values printed their .NET type name in the failed-test report | `FieldText` renders compact JSON; `AdoIdentityRef.ToString()` gives the name | `FieldValueRenderingTests` (11 cases), `AdoIdentityRefTests` (3 cases) | `Reporting/TestFailures/HtmlTestFailureRenderer.cs`, `Connections/AdoIdentityRef.cs` |
| Markdown escaping | `~` and a line-leading `=` were not escaped, so step text could open a code fence, strike text through or make a setext heading | Both are escaped | `MarkdownRenderingTests.BlockSyntaxAtLineStartAndTildesAreEscaped`, `StepTextCannotOpenACodeFenceOrASetextHeading` | `Reporting/SinkEncoding.cs` |
| Markdown line breaks | A step with several lines was rendered as one paragraph, because a single newline is not a break in Markdown | `<br>` before each newline between non-empty lines | `MarkdownRenderingTests.MultiLineStepTextKeepsItsLineBreaks` | `Reporting/Markdown/MarkdownTestCaseRenderer.cs` |
| French | Five messages made `build` feminine; the report and the attachment warnings called a test run « exécution », against the glossary and the other strings | Corrected in the catalog and in the French help | `FrenchTerminologyTests` (47 cases: the catalog and every French help topic) | `Resources/Strings.fr.resx`, `docs/commands/fr-CA/` |
| Profiles | `Set-AdoProfile -DefaultProject ''` stored an empty string, unlike `-DefaultBranch ''` | A blank value removes the key | `Profiles.Pester.ps1` "removes the default project with a blank value, like the other text defaults" | `Commands/SetAdoProfileCommand.cs` |
| Wildcards | `-Name` of `Get-AdoProfile` and `Get-AdoProject` accepted wildcards without declaring it, and their help said `SupportsWildcards: false` | `[SupportsWildcards]`; help corrected | `Projects.Pester.ps1` and `Profiles.Pester.ps1` wildcard tests; `HelpMetadata.Pester.ps1` now compares wildcard support | `Commands/GetAdoProfileCommand.cs`, `Commands/GetAdoProjectCommand.cs`, four help topics |
| Stack traces | The lexer took the second dot of `Type..ctor` as the separator, so the type was highlighted as part of the namespace and the method was `ctor` | The second of two adjacent dots belongs to the method name | `StackTraceLexerTests.ConstructorFramesKeepTheirTypeAndName` | `Reporting/Highlighting/StackTraceLexer.cs` |
| History progress | The total counted the current build, which is not read, so progress stopped one short | The total is the number of earlier builds | `RunHistoryTests.HistoryProgressEndsAtItsTotal` | `TestRuns/RunHistoryService.cs` |
| History cache | A history build that failed to read was cached without its failure, so later sets of the same invocation showed it as unavailable with no diagnostic | The cache keeps the failure and each set repeats the diagnostic | `RunHistoryTests.CachedUnreadableHistoryBuildKeepsItsWarningForLaterSets` | `TestRuns/RunHistoryService.cs`, `TestRuns/TestFailureInvocationCache.cs` |
| Help | The wrong facts and metadata listed under "Help and guides" | Corrected in both cultures | `HelpMetadata.Pester.ps1` now also compares wildcard support and the `-WhatIf` and `-Confirm` aliases | `docs/commands/en-US/`, `docs/commands/fr-CA/` |
| Gate output | PowerShell re-emitted the warning and `What if:` records of the product Pester child on the gate's host, past redirection, which produced the advisory warning noted in 0.4.0 and 0.5.0 | The child runs with text output and reports through a file | `Check.Tests.ps1` `Invoke-AdoReportingChild` tests (3) | `tools/lib/test-results.ps1`, `tools/check.ps1` |
| Gate summary | A passing `project-check` showed only `Build succeeded.` | Lines marked `check: ` become the summary | `Dev.Tests.ps1` "summarizes a passing gate by the result lines it marks" | `tools/lib/validation.ps1`, `tools/check.ps1` |
| Bootstrap | `bootstrap -Install` reported PlatyPS as unsupported although the gate requires it | It installs PlatyPS 1.x | `Dev.Tests.ps1` "installs every missing build module, including the help compiler, within its supported range" | `tools/lib/setup.ps1` |
| Edit hook | String catalogs and the format file were not syntax-checked after an edit or by `configuration` | `.resx` and `.ps1xml` are checked as XML | `Dev.Tests.ps1` "checks … as XML in the edit guard and in the configuration stage" | `tools/validate-edit.ps1`, `tools/dev.ps1` |
| Live check V-01 | `TestCase.Live.ps1` printed `PASS` when a shared-step reference had child steps, the one case in which the toolkit loses steps | Children that repeat the shared steps pass; other children are a `FAIL`; unread shared steps are `INCONCLUSIVE` | `LiveAudit.Tests.ps1` V-01 tests (9 cases) | `tests/Live/Live.Common.ps1`, `tests/Live/TestCase.Live.ps1` |
| Release workflow | Actions were referenced by tag, which can be moved, in a job that holds a publishing token. Left open in 0.4.0 | Pinned by commit | `ReleaseWorkflow.Tests.ps1` "pins every action to a commit and names its version" | `.github/workflows/release.yml` |
| Instructions and references | `AGENTS.md`, `docs/tooling.md` and the skills described `init`, `template-init`, other stacks, an eight-file package and option names that no longer existed; nothing checked them | Rewritten; the `documentation` stage and the skill check keep them true | `Documentation.Tests.ps1` (8) | `tools/lib/validation.ps1`, `tools/dev.ps1`, the instruction files |
| Fixture catalog | The catalog was a session log that did not name every file, and three fixtures were loaded by no test of their scenario | One entry per file, enforced; two fixtures deleted, one tested | `FixtureCatalogTests`, `StepExpanderNumberingTests.MissingReferenceKeepsItsGroupNumberBeforeTheNextStep` | `tests/Fixtures/README.md`, `tests/AdoToolkit.Core.Tests/Architecture/FixtureCatalogTests.cs` |
| Repository settings | `.gitattributes` marked lock files as generated, against its own comment, and named lock files this repository does not have | `packages.lock.json` stays visible in review | — | `.gitattributes` |

Every regression test was run before its fix and failed, then passed after it: the Core
tests against the unfixed source, the Pester tests against the module staged from the 0.5.0
source, and the tooling tests against the unchanged scripts. The one exception is
`MissingReferenceKeepsItsGroupNumberBeforeTheNextStep`, which covers existing, correct
behavior with a fixture that had no test.

### Structural changes

| Change | Reason |
| --- | --- |
| One string catalog, in Core | The PowerShell catalog repeated Core strings, so a wording fix had to be made twice and most of its entries were never read. `ResourceParityTests.CoreHoldsTheOnlyStringCatalog` keeps it single |
| `WorkItemBatchReader` | Four services carried their own copy of the work item batch request and its error handling |
| `NameFilter` | Four cmdlets carried their own copy of the normalized wildcard match |
| `DocumentOpener` | Both exporters share one launch policy |
| One package layout list | The gate had its own allowlist beside `Assert-AdoPackage`; it now calls that function. The installer keeps a copy because it ships alone, and a test keeps the two equal |
| Product-only tooling | The multi-stack plan, `init` and their tests described a template, not this product |

### Existing tests changed

| Test | Change |
| --- | --- |
| `ResourceParityTests` | Rewritten for the single catalog: matching keys and placeholders, exactly the message keys and diagnostic codes, and no second catalog |
| `TestBugResolutionTests` | One French literal follows the corrected message (`du build 401`) |
| `HelpMetadata.Pester.ps1` | Also compares wildcard support, and no longer skips the aliases of `-WhatIf` and `-Confirm` |
| `Package.Tests.ps1` | Seven package files and five signable files; a package that still carries the PowerShell satellite is rejected |
| `Dev.Tests.ps1`, `Workflow.Tests.ps1` | Rewritten for the product-only tooling; the tests of `init` and of the other stacks are removed with the code |
| Report goldens | `testcase-french.en-US.md` and `testcase-french.fr-CA.md` gain `<br>`; the five `testfailures-*.fr-CA.html` files change « Exécution » to « Série de tests » in two labels and one note. No other golden changed |

### Findings not fixed

| Finding | Reason |
| --- | --- |
| V-01: the parser skips the child elements of a shared-step reference, as the specification requires. If Server 2020 puts the Test Case's later steps inside the reference, they are missing from reports | The shape must be observed at work; the specification forbids guessing it. The live check now fails on exactly this case |
| V-03: the JSON and XML shapes of shared parameters are assumptions | Same reason; `TestCase.Live.ps1` with `ADOTOOLKIT_LIVE_SHARED_PARAM_CASE_ID` decides |
| A Test Case report links a URL with user information (`https://user@host/…`) that appears in step text | Declined in 0.4.0: the link text shows the whole URL. Unchanged |
| French punctuation spacing (`;`, and the no-break space before `:`) is not uniform across messages and help | Cosmetic, and both forms occur in Canadian French. Unchanged |
| JSON responses have no size limit | Declined in 0.4.0. Unchanged |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Incomplete-input check | It tests that required properties are present, not that their values are consistent. An object with an invented ID still reaches the server and is answered there |
| `-Open` warning text | The reason is the operating system's message, in the language of Windows, not of the report |
| Markdown line breaks | `<br>` is rendered by the common Markdown viewers; a viewer that strips HTML shows the lines joined, as 0.5.0 did |
| Limits carried over | The 0.5.0 [known limitations](release-0.5.0.md#known-limitations) on profile defaults, run names, attachment links and Server 2020 pipeline names still apply |

## Work-PC Live checks

Install the 0.6.0 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.6.0, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.6.0 with seven files in its folder |
| 2 | `TestCase.Live.ps1` with `ADOTOOLKIT_LIVE_TESTCASE_ID` set to a Test Case that uses Shared Steps and has steps after them | `PASS V-01 POSITIVE_REF_NO_CHILDREN` or `PASS V-01 POSITIVE_REF_CHILDREN_REPEAT_SHARED_STEPS`. `FAIL V-01 REFERENCE_CHILDREN_ARE_NOT_THE_SHARED_STEPS` means the server nests other steps inside the reference and reports lose them: report that verdict, nothing else |
| 3 | The same script with `ADOTOOLKIT_LIVE_SHARED_PARAM_CASE_ID` | `PASS V-03 LOCAL_SHARED_AND_ACCENTED_NAMES`. `FAIL V-03 PARAMETER_DIAGNOSTIC` means the assumed parameter shapes did not parse; `INCONCLUSIVE` names the kind of case that is still needed |
| 4 | `Get-AdoTestCase -Id <id> \| Export-AdoTestCase -Open`, then the same with `Export-AdoBuildTestFailure -Open` | The report opens. If the browser is blocked, a warning names the report and the reason, and the `FileInfo` is still returned |
| 5 | Export a build whose failed tests have custom fields, in English and French | No value reads like a .NET type name; French labels say « Série de tests » and messages say « Le build » |
| 6 | Export a Test Case with multi-line steps as Markdown and open it in the viewer used at work | The lines of a step are separate |
| 7 | The [0.5.0 checks](release-0.5.0.md#work-pc-live-checks), including V-30 | Still pending |

## Local validation and developer handoff

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0. All six stages pass without a warning: 57 PowerShell files linted, 189 tooling Pester tests, 144 configuration files, 81 Markdown files, the hook and skill layout, and the product check |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as the release workflow runs it | Exit 0 with the exact module pins in `tools/BuildModules.psd1` |
| Core tests in the final gate | 1067 passed under en-US and 1067 under fr-CA, with no failures or skips in either TRX report. 89 are new: 47 in `FrenchTerminologyTests`, 11 in `FieldValueRenderingTests`, 6 in `KnownFoldersTests`, 3 each in `MarkdownRenderingTests` and `AdoIdentityRefTests`, 1 in `FixtureCatalogTests` and 18 in existing classes |
| Product Pester tests against the staged module | 140, all passed: the gate fails on any failed, skipped or not-run test. 16 are new: 13 in `InputGuard.Pester.ps1`, 2 in `Profiles.Pester.ps1` and 1 in `Projects.Pester.ps1` |
| Tooling Pester tests | 189, all passed, in eight files. `Documentation.Tests.ps1` is new |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version` | Prints `0.6.0`; the staged manifests in `artifacts/verify/AdoToolkit/0.6.0/` and `artifacts/AdoToolkit/0.6.0/` report `ModuleVersion` 0.6.0 |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.6.0` with seven files |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.6.0-win-x64.zip -ModuleVersion 0.6.0 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.6.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.6.0/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt`, `bundle.json` (module 0.6.0, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the pin) and 658 runtime files, 669 files in all. Each `.sha256` file matches its ZIP. The installer is byte-identical to its source. Run with `-Destination` on a scratch folder, it installs the seven files; the module then loads with 22 cmdlets and French help and messages, and the installer refuses the 0.5.0 ZIP |

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.6.0-win-x64.zip` | 110274782 | `1e32001eaa91daa8101514464fc81e36676feb518612e8caad1b54e42d472dbf` |
| `AdoToolkit-0.6.0.zip` | 667868 | `47c292d335fa76486294cb86b9868333421219ab70aec1b0bb21111ec87a9e33` |
| `Install-AdoToolkit.ps1` | 12880 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes.

The action commits in the release workflow were looked up through the public GitHub API on
2026-10-01: the tags `v7` and `v7.0.1` of `actions/checkout` and `v6` and `v6.0.0` of
`actions/setup-dotnet` name the pinned commits. The workflow itself was not run.

No Live script, commit, tag, push or GitHub release publication was performed. The
developer owns the rest of the release:

1. Review the final diff: the input guard in `AdoCmdletBase` and `InputGuard`, the single
   string catalog and the seven-file package, the report changes and the seven regenerated
   goldens, the tooling without `init` and other stacks, the new `documentation` stage, the
   instruction files, rules and skills, the archived notes, and the pinned workflow actions.
2. Commit the reviewed 0.6.0 changes, including the new files and the three deleted
   PowerShell resource files.
3. Create `v0.6.0` on that commit and push the commit and tag when ready to publish.
4. Watch the release workflow and check its five uploaded assets, version and checksums.
   CI builds its own assets, so compare each checksum with CI's own `.sha256` files;
   archive metadata can differ from a local build.
5. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
