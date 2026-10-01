# Developer tooling

Reference for `tools/`. Daily work needs only the command table in `AGENTS.md`; read this
before changing a command, a stage, the product gate, packaging or the agent setup.

```powershell
pwsh -NoProfile -File .\tools\dev.ps1 <command>
```

`tools/dev.ps1` requires PowerShell 7.6.5 or later and runs from the repository root.

## Commands

| Command | Returns |
| --- | --- |
| `context [-Path <target>]` | Project name, stack, test counts, the commands, and up to 40 important paths. With `-Path`, also `ScopedInstructions`: the `AGENTS.md` and `CLAUDE.md` files that apply to the target |
| `find -Query <literal> [-Limit 20]` | Case-insensitive matches in paths, then in content, with line numbers. `LimitReached` means the limit, 1 to 100, was filled |
| `inspect` | Project files, test files, top-level directories, configuration and instruction files |
| `deps [-Limit 20]` | NuGet packages declared in project and props files, and the tools `verify` needs |
| `plan` | The stages `verify` would run, with the command line of the product gate. Runs nothing |
| `verify` | The full gate: status, findings and duration per stage |
| `verify -Stage <name>` | Only the named stages. Always incomplete |
| `verify -SkipTests` | Every check except tests. Always incomplete |
| `diagnose` | Presence and version of each required tool and of ripgrep |
| `bootstrap [-Install]` | The same list. `-Install` installs Pester 5.x, PSScriptAnalyzer and PlatyPS 1.x from PSGallery for the current user, and actionlint and ripgrep with winget. It does not install the .NET SDK |

- Output: one compact JSON document. Exit `0` for `ok` or `pass`, `1` for a failure, `2`
  for `incomplete` or `unavailable`.
- In PowerShell, `& .\tools\dev.ps1 <command> -Format Object` returns objects.
- Several stages: `& .\tools\dev.ps1 verify -Stage @('configuration', 'tooling-layout')`.

## Discovery

- ripgrep lists files when it is installed; a pruned file walk does otherwise. Both apply
  the lists in `tools/dev.ps1` and ignore `.gitignore` and user ripgrep settings, so the
  result does not depend on the tool.
- Excluded: build and output directories (`ExcludedDirectoryNames`), `secret` and
  `secrets` directories, sensitive file names (`SensitiveFileGlobs`), and links.
- Hidden shared configuration such as `.claude/` and `.github/` is included.
- Content search skips binary files and files above 1 MiB. Nothing is indexed or cached.

## Verification

| Stage | Checks |
| --- | --- |
| `powershell-lint` | Parses every PowerShell file and runs PSScriptAnalyzer with `PSScriptAnalyzerSettings.psd1` |
| `powershell-test` | Runs `tools/tests/*.Tests.ps1` with Pester 5.x |
| `configuration` | Parses JSON strictly and XML (`.xml`, `.config`, `.csproj`, `.props`, `.targets`, `.slnx`, `.resx`, `.ps1xml`) with DTDs prohibited |
| `documentation` | In every Markdown file: each relative link resolves, each heading anchor exists, and each code span that starts with `src/`, `tests/`, `tools/`, `docs/`, `.claude/`, `.agents/` or `.github/` names an existing path |
| `tooling-layout` | `.claude/settings.json` is valid, hook scripts live under `tools/`, every `@import` in `CLAUDE.md` and `.claude/rules/*.md` resolves, and every skill has a body and a Claude wrapper with the same name and description |
| `workflow-lint` | Runs actionlint on every file in `.github/workflows/`: the YAML, the workflow schema and the expressions. Its shellcheck and pyflakes integrations are switched off, because every run block is PowerShell |
| `project-check` | The product gate, `tools/check.ps1` |

- `verify` never installs or restores. A missing tool makes its stage `unavailable` and
  the result `incomplete`.
- The `documentation` stage skips files under `docs/archive/` other than the two index
  files, and files under `tests/Fixtures/` other than the catalog. In `CHANGELOG.md` and
  `docs/release-<version>.md` it checks links but not paths, because the entry or the
  notes of a release may name a file that has since gone. A code span with a placeholder
  (`<`, `*`, `{`) is not treated as a path. The three lists are in `tools/dev.ps1`.
- An external stage runs with `CI=true` and `NO_COLOR=1`, for at most 300 seconds
  (`project-check`: 900). On timeout its process tree is stopped. The last 120 output
  lines are kept, 2,000 characters each.
- Pass, fail and incomplete come from exit codes and structured outcomes, never from
  console text. Output lines of an external stage that mention something skipped, missing
  or not installed become advisory warnings; they never change the result.

### Product gate

`tools/check.ps1` exits `0`, `1` or `2`. Lines that start with `check: ` are its results;
`verify` shows them as the stage summary.

1. Exits `2` when the .NET SDK, restored package assets, Pester 5.x or PlatyPS 1.x is
   missing.
2. Builds `AdoToolkit.slnx` in Release without restoring.
3. Runs the Core tests twice, with `ADOTOOLKIT_TEST_CULTURE` set to `en-US`, then `fr-CA`,
   and `ADOTOOLKIT_UPDATE_GOLDEN` cleared. Each run writes TRX results to a new folder
   under `artifacts/verify/`; `Get-AdoTestOutcome` reads the counters. A skipped or
   undiscovered test, or a missing counter, exits `2`.
4. Stages the package with `tools/package/Publish-AdoToolkitPackage.ps1 -NoBuild` and
   checks it with `Assert-AdoPackage`: the exact seven files, help for all 22 commands,
   the manifest and the assembly versions.
5. Runs `tests/AdoToolkit.PowerShell.Tests/*.Pester.ps1` against the staged module in a
   child process (`Invoke-AdoReportingChild`). The child writes its result lines to the
   file named by `ADOTOOLKIT_GATE_REPORT`, so nothing a cmdlet prints reaches the gate's
   output. No tests, a skipped test or a test that did not run exits `2`.
   `ADOTOOLKIT_CONFIG_PATH` names a file that does not exist.

With `-SkipTests`, the gate builds and stages the package only.

### Changing checks

- Product checks go into `tools/check.ps1`: call executables with argument arrays, test
  the exit code at once, print a `check: ` line per result, and never call `verify`.
- A built-in stage goes into `tools/lib/validation.ps1`, with fixture-based tests in
  `tools/tests/`. An in-process stage has `Name`, `Action` and `ActionArguments` and
  returns `Failures`, `Summary`, `Warnings` and optionally `Unavailable`. An external
  stage has `Name`, `Executable`, `Arguments` and optionally `TimeoutSeconds` (1 to 3600)
  and `ExitCodeContract = 'dev'`, which reads exit `2` as incomplete.

## Prerequisites

Install tools only as an explicit, authorized step.

| Tool | Used for | Installation |
| --- | --- | --- |
| PowerShell 7.6.5+ | `tools/dev.ps1`, the hooks, the packaging scripts | `winget install --id Microsoft.PowerShell --exact --source winget` |
| .NET SDK | Build and tests; `global.json` selects the version | [Microsoft installer](https://dotnet.microsoft.com/download/dotnet), then `dotnet restore AdoToolkit.slnx --locked-mode` |
| Pester 5.x | Tooling and product tests | `Install-Module Pester -MinimumVersion 5.0 -MaximumVersion 5.999.999 -Scope CurrentUser -Repository PSGallery` |
| PSScriptAnalyzer | `powershell-lint` | `Install-Module PSScriptAnalyzer -Scope CurrentUser -Repository PSGallery` |
| PlatyPS 1.x | Compiled help in the gate and in packaging | `Install-Module Microsoft.PowerShell.PlatyPS -MinimumVersion 1.0 -MaximumVersion 1.999.999 -Scope CurrentUser -Repository PSGallery` |
| actionlint | `workflow-lint` | `winget install --id rhysd.actionlint --exact --source winget` |
| ripgrep (recommended) | Faster discovery | `winget install --id BurntSushi.ripgrep.MSVC --exact --source winget` |

- Pester stays on 5.x because the tooling depends on its result format.
- winget extends `PATH` for new processes: open a new terminal after it installs a tool.
  Any actionlint version passes locally; the workflows install the version pinned in the
  [setup action](#setup-action).
- `ADOTOOLKIT_RELEASE_BUILD=1`, set by the release workflow, makes the gate, the product
  tests and help generation require the exact versions in `tools/BuildModules.psd1`. A
  newer installed version does not satisfy a missing pin.
- `nuget.config` clears inherited package sources and maps every package to nuget.org.
  Where nuget.org is blocked, restore fails; the prebuilt release needs no build.
- Without administrator rights, install the .NET SDK for the account with Microsoft's
  `dotnet-install.ps1` and put it first on `PATH` in each session that builds:

  ```powershell
  $dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
  & .\dotnet-install.ps1 -JsonFile .\global.json -InstallDir $dotnet -NoPath
  $env:DOTNET_ROOT = $dotnet
  $env:PATH = "$dotnet;$env:PATH"
  dotnet --version    # from the repository root; it must succeed
  ```

- Run every script in PowerShell 7 (`pwsh`). If a downloaded copy refuses to run unsigned
  scripts, remove the download mark once:
  `Get-ChildItem -Recurse -File -Include *.ps1, *.psm1, *.psd1 | Unblock-File`.

## Packaging and releases

Rules for these scripts are in `tools/package/AGENTS.md`; the `release` skill is the
procedure. It prepares a release of the checked-out branch (version, release notes, the
`CHANGELOG.md` section, validation), runs no Git write, and ends with two commands for the
developer:

| Command | Does | Starts |
| --- | --- | --- |
| 1 | Stages and commits every change, creates the annotated tag `v<version>` and pushes the branch and the tag together | The [release workflow](#release-workflow), which publishes at once |
| 2 | Prints and opens GitHub's compare page for the branch against `main`, with the title and body of the pull request filled in | The [pull request check](#pull-request-check), once the developer creates the pull request |

| Script | Purpose |
| --- | --- |
| `tools/package/Publish-AdoToolkitPackage.ps1` | Checks prerequisites, restores in locked mode, builds Release and stages `artifacts/AdoToolkit/<version>/` with compiled English and French help. `-NoBuild` packages the existing build without restoring; `-NoRestore` skips only the restore |
| `tools/package/New-AdoToolkitRelease.ps1` | Writes the module zip, its checksum and the installer to `artifacts/release/`. With `-PowerShellArchivePath`, `-PowerShellChecksumPath` and `-PowerShellVersion`, also writes `AdoToolkit-<version>-win-x64.zip` and its checksum |
| `tools/package/Test-AdoToolkitPortable.ps1` | Extracts the portable zip and starts its launcher offline, with empty `PATH` and `PSModulePath`; checks the module, the PowerShell version, the architecture and the loaded paths |
| `tools/package/Install-AdoToolkit.ps1` | End-user installer: checks the zip against its checksum, requires exactly the module layout under one `AdoToolkit/<version>/` folder, installs for the current user and replaces the same version only when the new copy is complete. `-ExpectedThumbprint` also requires valid signatures |
| `tools/package/Set-AdoToolkitPackageSignature.ps1`, `tools/package/Install-AdoToolkitPackage.ps1` | The signed flow for a staged package, when a code-signing certificate exists |

- Files are written beside their target, validated, then moved. Symbolic links and
  junctions are rejected on every package and install path; cloud-file placeholders, such
  as a OneDrive-redirected Documents folder, are allowed.
- Package validation reads each assembly's identity and version without loading it. The
  three DLLs must match the manifest version, so `-NoBuild` rejects stale binaries.
- Release generation validates every archive, checksum and the installer before replacing
  an asset, and restores the previous set when a replacement fails. A crash can leave
  `.previous-*` files for manual recovery.
- Relative package and output paths follow the PowerShell location and must stay inside
  the repository.
- The portable zip is built from an official, unmodified Windows x64 PowerShell zip and
  its published `hashes.sha256`, both already on disk; nothing is downloaded. The version
  must be a 7.6 patch version that matches the file name. Unsafe paths, links, duplicate
  entries and incomplete runtimes are rejected. `bundle.json` records the module version,
  the runtime version and the runtime hash.

```powershell
pwsh -NoProfile -File .\tools\package\Publish-AdoToolkitPackage.ps1 -NoRestore
pwsh -NoProfile -File .\tools\package\New-AdoToolkitRelease.ps1 `
    -PowerShellArchivePath .\artifacts\runtime-download\PowerShell-<runtime>-win-x64.zip `
    -PowerShellChecksumPath .\artifacts\runtime-download\hashes.sha256 `
    -PowerShellVersion <runtime>
pwsh -NoProfile -File .\tools\package\Test-AdoToolkitPortable.ps1 `
    -ArchivePath .\artifacts\release\AdoToolkit-<version>-win-x64.zip `
    -ModuleVersion <version> -PowerShellVersion <runtime>
```

The launcher loads only the module beside it, without user profiles, and sets
`RemoteSigned` for its own process. It respects Group Policy and persists no execution
policy or `PATH` change.

### Release workflow

`.github/workflows/release.yml` runs when a tag `v<VersionPrefix>` is pushed:

1. Checks out the tag without keeping the write token in the Git configuration.
2. Installs the toolchain with the [setup action](#setup-action).
3. Requires the tag to equal the module version.
4. Restores in locked mode, runs `verify`, and packages the verified build with
   `-NoBuild`.
5. Checks the portable zip with `tools/package/Test-AdoToolkitPortable.ps1`.
6. Creates the GitHub release with five assets. Only this step receives the token.

- The release text is the `CHANGELOG.md` section of the version, the lines under its
  `## <version>` heading, then the installation instructions. A relative link in the
  section is pointed at the file as tagged. When the section is missing or empty, step 6
  fails before anything is published.
- `verify` requires that section for `VersionPrefix` already, in the form the `release`
  skill writes, so a version without it fails locally, in the pull request check and in
  step 4. The test is in `tools/tests/ReleaseWorkflow.Tests.ps1`.
- The steps name the tag through `RELEASE_TAG`, never through the ref of the run, which
  is a branch in a manual run.
- Scripts and modules are unsigned by decision. Checksums detect a corrupted download;
  they do not replace signatures or a trusted source.

When a run does not publish:

| Cause | Recovery |
| --- | --- |
| A passing fault: a download, the runner | Re-run the failed job from the page of the run |
| The run did not start, or the workflow file was at fault and is corrected on a branch | Start the workflow by hand on that branch (Actions, Release, Run workflow) with the tag as input. The run takes `.github/workflows/release.yml` from the branch and every other file from the tag, so the tag must hold `.github/actions/setup/action.yml` |
| The tagged files are at fault: `verify` fails, or the tag does not match the version | Nothing was published. Correct and commit on the branch, remove the tag as below, then tag and push again as command 1 does |

```powershell
git push <remote> :refs/tags/v<version>; git tag -d v<version>
```

### Pull request check

`.github/workflows/verify.yml` runs on a pull request to `main`, with a token that can only
read the repository:

1. Checks out without keeping the token in the Git configuration.
2. Installs the toolchain with the [setup action](#setup-action).
3. Restores in locked mode and runs `verify`. Any exit code other than `0` fails the check.

A pull request is therefore verified with the toolchain that the release is verified with.
Its tests are in `tools/tests/VerifyWorkflow.Tests.ps1`.

### Setup action

`.github/actions/setup/action.yml` is a composite action that both workflows run after
checkout. It installs:

| Tool | Version | Checked against |
| --- | --- | --- |
| .NET SDK | The one `global.json` selects | — |
| PowerShell | `POWERSHELL_VERSION` | Its published `hashes.sha256` and `POWERSHELL_SHA256` |
| actionlint | `ACTIONLINT_VERSION` | `ACTIONLINT_SHA256` |
| Pester, PSScriptAnalyzer, PlatyPS | Exactly those in `tools/BuildModules.psd1` | The installed version |

- Each pin is declared once, in this file. Update a version and its SHA-256 together:
  the PowerShell hash from that release's `hashes.sha256`, the actionlint hash from that
  release's `actionlint_<version>_checksums.txt`.
- The action exports `POWERSHELL_VERSION`; the release job bundles that runtime and names
  it in the release text.
- The actions that the workflows and this action use are pinned by commit.
  `.github/dependabot.yml` opens a pull request each week when one has a newer release
  and rewrites the commit with its version comment; the pull request check verifies it.
  Dependabot does not know the PowerShell and actionlint pins.
- `tools/tests/ReleaseWorkflow.Tests.ps1` runs the steps of the action with mocked
  downloads.

## Live checks

Rules are in `tests/Live/AGENTS.md`. The developer runs a check in a new PowerShell
window at work, for example `pwsh -NoProfile -File .\tests\Live\Smoke.Live.ps1`. Every
check needs `ADOTOOLKIT_LIVE_PROFILE`, a profile with a default project. A signed release
must verify completely; an unsigned one is accepted.

| Script | Checks | Further variables |
| --- | --- | --- |
| `tests/Live/Connection.Live.ps1` | Installation, access and the project listing | — |
| `tests/Live/Smoke.Live.ps1` | The user workflow through the cmdlets: connection, test runs, failed-test retrieval, report rendering and optionally a Test Case report. A failure shows the error code, operation and JSON path | `ADOTOOLKIT_LIVE_TEST_BUILD_ID` or `ADOTOOLKIT_LIVE_DEFINITION`; optionally `ADOTOOLKIT_LIVE_PLAN_ID` with `ADOTOOLKIT_LIVE_SUITE_ID` |
| `tests/Live/Shape.Live.ps1` | Samples projects, a build, its logs and timeline, test runs, results, details, attachments, and test plans, suites and cases. Prints allowlisted property paths and JSON kinds; an unknown key becomes `<unknown>` and dynamic bags are opaque. A sample does not prove that a path never occurs | Optionally `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, `ADOTOOLKIT_LIVE_PLAN_ID`, `ADOTOOLKIT_LIVE_SUITE_ID` |
| `tests/Live/TestFailures.Live.ps1` | V-19 to V-25 and V-30. V-22 passes only with observed rerun details and distinct retry attempts under complete paging. V-30 checks the Bug category, the state categories, the Test Case relations and the module's own bug lookup, printing counts only | `ADOTOOLKIT_LIVE_TEST_BUILD_ID`; retries also need `ADOTOOLKIT_LIVE_RERUN_BUILD_ID` and `ADOTOOLKIT_LIVE_REATTEMPT_BUILD_ID` |
| `tests/Live/Triage.Live.ps1` | V-11 and V-14: timeline retries, continuation headers, log ranges and 64-bit line counts | `ADOTOOLKIT_LIVE_DEFINITION`, `ADOTOOLKIT_LIVE_BUILD_ID`; optionally `ADOTOOLKIT_LIVE_RETRIED_BUILD_ID` |
| `tests/Live/TestCase.Live.ps1` | V-01, V-02, V-03, V-05, V-10 and V-13. V-01 passes when a shared-step reference has no child steps, or children that repeat the shared steps; children that are other steps are a `FAIL`, because the toolkit skips them. V-02 needs a visual comparison | `ADOTOOLKIT_LIVE_TESTCASE_ID`; shared parameters also need `ADOTOOLKIT_LIVE_SHARED_PARAM_CASE_ID` |
| `tests/Live/TestCaseDetail.Live.ps1` | V-31 and V-32, the requests of `Export-AdoTestCase -IncludeDetail`. V-31 reads the Test Case with its relations: it passes when the field kinds and the relation attributes are as assumed, a named work item link and a description were observed, and the installed export shows the same number of links. V-32 sends the test points query: it passes when every point can be placed in a plan and a suite, paging by `$top` and `$skip` was observed, and the export shows the same number of points. Counts only; the report it renders goes to a temporary folder that the script deletes | `ADOTOOLKIT_LIVE_TESTCASE_ID`: a case with a description, a link to another work item and two or more test points |
| `tests/Live/Bulk.Live.ps1` | V-04 and V-06. A repeated continuation token or the 50-page ceiling makes the enumeration incomplete, which cannot pass V-04 | `ADOTOOLKIT_LIVE_PLAN_ID`, `ADOTOOLKIT_LIVE_SUITE_ID` |

- Repeat `tests/Live/Shape.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID` set to each
  retry build to see its `pipelineReference` and sub-results.
- Not covered by a script: V-07 and V-18 need controlled failing requests; V-15 and V-26
  links, V-16 terminology, V-27 browser policy and V-29 runner text need manual
  acceptance; V-28 has no probe.
- `tools/tests/LiveAudit.Tests.ps1` tests the helpers in `tests/Live/Live.Common.ps1` and
  single validation blocks offline. The findings behind those tests are in the
  [audit fix record](archive/plans/server-2020-audit-fixes.md).

## Agent setup

| File | Holds |
| --- | --- |
| `AGENTS.md` | The always-loaded contract; `CLAUDE.md` imports it |
| `src/AGENTS.md`, `tests/AGENTS.md`, `tests/Live/AGENTS.md`, `tools/AGENTS.md`, `tools/package/AGENTS.md`, `docs/AGENTS.md`, `docs/commands/AGENTS.md` | Rules for one subtree. Codex reads them by directory |
| `.claude/rules/*.md` | One import per nested file, scoped by `paths`, so Claude loads the same rules when it touches matching files |
| `.agents/skills/<name>/SKILL.md` | The body of a skill; Codex reads it |
| `.claude/skills/<name>/SKILL.md` | A wrapper with the same name and description that tells Claude to read the body |
| `.claude/settings.json` | Permissions and hooks for Claude |

| Rule file | Loads | When Claude touches |
| --- | --- | --- |
| `.claude/rules/product-src.md` | `src/AGENTS.md` | `src/**` |
| `.claude/rules/product-tests.md` | `tests/AGENTS.md` | `tests/**` |
| `.claude/rules/live-checks.md` | `tests/Live/AGENTS.md` | `tests/Live/**` |
| `.claude/rules/tooling.md` | `tools/AGENTS.md` | `tools/**`, `PSScriptAnalyzerSettings.psd1` |
| `.claude/rules/packaging.md` | `tools/package/AGENTS.md` | `tools/package/**`, `tools/BuildModules.psd1`, `.github/**`, `Directory.Build.props` |
| `.claude/rules/docs.md` | `docs/AGENTS.md` | `docs/**`, `README.md`, `CHANGELOG.md` |
| `.claude/rules/help-sources.md` | `docs/commands/AGENTS.md` | `docs/commands/**`, the cmdlet files `src/AdoToolkit.PowerShell/Commands/*Command.cs` |

| Skill | Use | Invoked by |
| --- | --- | --- |
| `fix-bug` | Fix a defect with a test that fails first | The agent or the user |
| `update-goldens` | Regenerate and review the report goldens | The agent or the user |
| `release` | Bump the version, archive old notes, write the release notes and the `CHANGELOG.md` section, validate the package, hand over the commit, tag, push and pull request commands | The user only |
| `rewrite` | Turn a draft into a prompt for another session | The user only |

| Hook | Event | Behavior |
| --- | --- | --- |
| `tools/guard-git.ps1` | Before a shell command | Blocks every Git command except `status`, `diff`, `log`, `show` and `blame`, and those with an option that runs a program. It reads the command text, so it is a speed bump, not a boundary |
| `tools/validate-edit.ps1` | After an edit | Parses the edited PowerShell, JSON or XML file and reports a syntax error. The file is already written; the hook cannot undo it |

- `.claude/settings.json` denies reading and editing secret-like paths, and allows the
  `tools/dev.ps1` commands, the Release build, Core test runs and the five Git commands
  without a prompt. Every other command follows the user's permission mode.
- Hooks and permissions reduce mistakes; they are not a sandbox. Codex uses its own
  permissions.
- To add a rule: write it into the nested `AGENTS.md` of the subtree. A new nested file
  also needs a `.claude/rules/` import and a row in the map of `AGENTS.md`.
- To add a skill: write the body, then the wrapper. `tooling-layout` fails when one is
  missing or the descriptions differ.
