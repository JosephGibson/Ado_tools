# Developer tooling

`tools/dev.ps1` is the single entry point for repository discovery, validation and
prerequisite checks. It requires PowerShell 7.6.5 or later. Run it from the
repository root:

```powershell
pwsh -NoProfile -File .\tools\dev.ps1 <command>
```

For daily orientation, `context` is enough. Read this reference when you change
the tooling or the product gate.

## Commands

| Command | Behavior |
| --- | --- |
| `context [-Path <target>]` | Compact orientation. With a target, it also lists the instruction files that apply to that path |
| `find -Query <literal> [-Limit 20]` | Case-insensitive search of paths, then content, with matching line numbers. `LimitReached` means the result budget filled |
| `inspect` | Detected manifests, entry points, directories and instruction files |
| `deps [-Limit 20]` | Declared NuGet, npm and `requirements.txt` dependencies. Other manifests are only listed |
| `plan` | The stages `verify` would run, with executables, arguments and dependencies. Runs nothing |
| `verify` | The full gate, with status, concise findings and per-stage duration |
| `verify -Stage <name>` | The selected stages and their prerequisites. The result is always incomplete |
| `verify -SkipTests` | All checks except tests. The result is always incomplete |
| `diagnose` | Availability and versions of local tools |
| `bootstrap [-Install]` | Lists missing prerequisites. `-Install` installs the supported ones |
| `init -ProjectName <name> -Description <text>` | Replaces the `project:start`/`project:end` block in `README.md` and `AGENTS.md`. Each file is replaced atomically, and a failed write is rolled back |

The CLI prints one compact JSON document. Exit codes are `0` for ok or pass, `1` for
failure, and `2` for unavailable or incomplete. Inside PowerShell,
`& .\tools\dev.ps1 <command> -Format Object` returns objects instead. Arrays stay
arrays even with zero or one element. To select several stages, pass an array:
`& .\tools\dev.ps1 verify -Stage @('configuration', 'tooling-layout')`.

Discovery uses ripgrep when it is installed and a pruned file walk otherwise. Both
use the same exclusion list, which is defined in `dev.ps1`, and ignore
machine-specific ignore settings. Dependency and build output, links, secret paths
and personal configuration are excluded. Shared hidden configuration is included.
Content search skips binary files and files over 1 MiB. `find` returns locations,
not full text, and nothing is indexed or cached.

## Verification

| Stage | Checks |
| --- | --- |
| `powershell-lint` | Parses every PowerShell file and runs PSScriptAnalyzer |
| `powershell-test` | Runs the tooling tests (`tools/tests/*.Tests.ps1`) with Pester 5.x |
| `configuration` | Parses JSON, XML and MSBuild files with DTDs and external resolution disabled |
| `tooling-layout` | Validates Claude hook settings, requires hook scripts to be under `tools/`, and checks instruction imports |
| `project-check` | Runs the product gate in `tools/check.ps1` |

Prerequisites must already be installed and packages restored. Verification never
installs anything. External stages run with `CI=true`, `NO_COLOR=1` and Corepack
network access disabled. Each one has a five-minute timeout, and on timeout its
process tree is stopped. Only the last 120 output lines are kept, each truncated to
2,000 characters. Results come from exit codes and structured outcomes, never from
console text.

### Product gate

Because `tools/check.ps1` exists, it replaces the .NET build and test stages that
`dev.ps1` would otherwise infer. The other stages still run. The gate:

1. Exits `2` if the .NET SDK, restored package assets, Pester 5.x or PlatyPS 1.x is
   missing.
2. Builds `AdoToolkit.slnx` in the Release configuration without restoring.
3. Runs the Core tests twice, with `ADOTOOLKIT_TEST_CULTURE` set to `en-US` and then
   `fr-CA`. Each run writes TRX results to a new folder under `artifacts/verify/`,
   and `tools/lib/test-results.ps1` checks their counters. Skipped or undiscovered
   tests and missing counters exit `2`, even when the test runner succeeded.
4. Stages the package with `Publish-AdoToolkitPackage.ps1 -NoBuild` and checks
   every published file against an allowlist.
5. Runs the product Pester tests (`tests/AdoToolkit.PowerShell.Tests/*.Pester.ps1`)
   against the staged module in a child process. A run with no tests, or with
   skipped or unrun tests, exits `2`. `ADOTOOLKIT_CONFIG_PATH` points to a
   configuration file that doesn't exist, so a developer's default profile never
   connects a test to a real server.

With `-SkipTests`, the gate builds and stages the package only.

### Changing checks

- Put product checks in `tools/check.ps1`. Call executables with explicit argument
  arrays, check their exit codes immediately, and exit `0`, `1` or `2`. Print short
  diagnostic lines. Never call `dev.ps1 verify` from the gate, because that
  recurses.
- Add a reusable built-in stage in `tools/lib/validation.ps1`, with fixture-based
  tests in `tools/tests/`. An external stage has `Name`, `Executable`, `Arguments`,
  and optionally `WorkingDirectory`, `DependsOn` and `TimeoutSeconds` (1–3600). An
  in-process stage returns `Failures`, `Summary`, `Warnings` and optionally
  `Unavailable`.
- Without a product gate, `dev.ps1` builds and tests .NET projects and runs existing
  `package.json` scripts. Python, Rust, Go and Java are detected but reported as
  incomplete until `tools/check.ps1` covers them.

## Prerequisites

Install tools only as an explicit, authorized step.

| Tool | Used for | Installation |
| --- | --- | --- |
| PowerShell 7.6.5+ | `dev.ps1` and agent hooks | `winget install --id Microsoft.PowerShell --exact --source winget` |
| .NET SDK | Build and tests; the version is selected by `global.json` | [Microsoft installer](https://dotnet.microsoft.com/download/dotnet), then `dotnet restore AdoToolkit.slnx --locked-mode` |
| Pester 5.x | Tooling and product tests | `Install-Module Pester -MinimumVersion 5.0 -MaximumVersion 5.999.999 -Scope CurrentUser -Repository PSGallery` |
| PSScriptAnalyzer | PowerShell lint | `Install-Module PSScriptAnalyzer -Scope CurrentUser -Repository PSGallery` |
| PlatyPS 1.x | Compiled help in the product gate and packaging | `Install-Module Microsoft.PowerShell.PlatyPS -MinimumVersion 1.0 -MaximumVersion 1.999.999 -Scope CurrentUser -Repository PSGallery` |
| ripgrep (recommended) | Faster discovery | `winget install --id BurntSushi.ripgrep.MSVC --exact --source winget` |
| ast-grep (optional) | Structural search; `diagnose` only reports it | `cargo install ast-grep --locked` |

`bootstrap -Install` installs only ripgrep, Pester and PSScriptAnalyzer. Pester is
limited to 5.x because the tooling depends on its result format. Upgrading it
requires regression tests.

The release workflow sets `ADOTOOLKIT_RELEASE_BUILD=1`. In that mode, verification,
product tests and help generation require the exact versions in
`tools/BuildModules.psd1`, which also supplies the workflow's installation versions.
A newer installed version cannot substitute for a missing pin. Local development
continues to accept Pester 5.x and PlatyPS 1.x. `diagnose` includes PlatyPS when
command help sources are present.

Without administrator rights, install the .NET SDK for your account only, with
Microsoft's `dotnet-install.ps1` script, and put it first on `PATH` in every session
that builds:

```powershell
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
& .\dotnet-install.ps1 -JsonFile .\global.json -InstallDir $dotnet -NoPath
$env:DOTNET_ROOT = $dotnet
$env:PATH = "$dotnet;$env:PATH"
dotnet --version    # run from the repository root; it must succeed
```

Run every repository script in PowerShell 7 (`pwsh`), not Windows PowerShell 5.1.
The package scripts declare `#Requires -Version 7.6`, so 5.1 stops with a clear
message. If a downloaded copy of the repository refuses to run scripts because they
aren't signed, remove the download mark once:
`Get-ChildItem -Recurse -File -Include *.ps1, *.psm1, *.psd1 | Unblock-File`.

The repository `nuget.config` clears inherited package sources and maps every package
to nuget.org. Feeds from your user or machine configuration, and their credential
prompts, are not used. If nuget.org is blocked on your network, restore fails; the
prebuilt release avoids the need to build.

## Packaging and releases

| Script | Purpose |
| --- | --- |
| `tools/package/Publish-AdoToolkitPackage.ps1` | Checks prerequisites, restores in locked mode, builds the Release configuration and stages `artifacts/AdoToolkit/<version>/` with compiled English and French help. `-NoBuild` packages the existing build without restoring, as the product gate and the release workflow do; `-NoRestore` skips only the restore |
| `tools/package/New-AdoToolkitRelease.ps1` | Writes `artifacts/release/AdoToolkit-<version>.zip`, its `.sha256` checksum file (the `sha256sum` format) and a copy of `Install-AdoToolkit.ps1` |
| `tools/package/Install-AdoToolkit.ps1` | Standalone end-user installer. Checks the zip against its checksum, requires exactly the module layout in one `AdoToolkit/<version>/` folder, and installs it for the current user. The previous copy of that version is replaced only after the new copy is complete. `-ExpectedThumbprint` also requires valid signatures |
| `tools/package/Set-AdoToolkitPackageSignature.ps1`, `Install-AdoToolkitPackage.ps1` | Optional signed flow for a staged package when a code-signing certificate is available |

Files are written to a temporary name beside their target, validated, and then moved
into place. Symbolic links and junctions are rejected on every package and install
path. Cloud-file placeholders, such as a OneDrive-redirected Documents folder, are
reparse points without a link target and are allowed. `Install-AdoToolkit.ps1` carries
its own copy of the module layout because it ships without the repository. A tooling
test keeps that copy identical to `Assert-AdoPackage`.

Package validation reads each DLL's assembly identity and version without loading
its code. All four DLLs must match the manifest version; `-NoBuild` rejects stale
binaries and requires a rebuild before packaging.

Release generation stages and validates the archive, checksum and installer before
replacing any asset. A failed replacement restores the previous set; backups are
retained if recovery itself fails. This is rollback on failure, not a filesystem
transaction across three files: a process crash can leave `.previous-*` files for
manual recovery. Relative package/output paths follow the PowerShell location,
including after `Set-Location`, and must stay inside the permitted repository root.

To publish a release:

1. Set `VersionPrefix` in `Directory.Build.props` and run `verify`.
2. Push a tag named `v<VersionPrefix>`, for example `v0.2.0`.

`.github/workflows/release.yml` then runs on a Windows runner. It installs the pinned
PowerShell 7.6 after checking its published hash, installs Pester, PSScriptAnalyzer
and PlatyPS, and checks the tag against the module version. It then restores in
locked mode, runs `verify`, packages the verified build, and creates the GitHub
release with the three assets and install notes. Releases are unsigned by decision;
the checksum and the installer's layout check protect the download.

## Live checks

`tests/Live/*.Live.ps1` run only when you start them, on a machine that can reach the
server, against the newest release installed for your account. Signed and unsigned
releases are both accepted, but a signed release must verify completely. The
checks print `PASS`, `FAIL` or `INCONCLUSIVE` lines with structural notes, and the
shape check also prints `SHAPE` lines. No work values are printed. Exit codes are
`0`, `1` and `2`, respectively.

| Script | Checks | Environment variables |
| --- | --- | --- |
| `Connection.Live.ps1` | Installation, access and project listing | `ADOTOOLKIT_LIVE_PROFILE` |
| `Smoke.Live.ps1` | The user workflow through the cmdlets: connection, test runs, failed-test retrieval, report rendering, and optionally a Test Case report. Failures show the error code, operation and JSON path. Reports are rendered to a temporary folder that is deleted | `ADOTOOLKIT_LIVE_PROFILE`, then `ADOTOOLKIT_LIVE_TEST_BUILD_ID` or `ADOTOOLKIT_LIVE_DEFINITION`, and optionally `ADOTOOLKIT_LIVE_PLAN_ID` with `ADOTOOLKIT_LIVE_SUITE_ID` |
| `Shape.Live.ps1` | Samples projects, BuildGet, build logs/timeline, test runs/results/detail, result/sub-result attachments and test plans/suites/cases. Prints allowlisted property paths and JSON kinds. Unknown keys become `<unknown>`; dynamic bags, including `customFields[].value`, are opaque. Samples do not establish complete enumeration | `ADOTOOLKIT_LIVE_PROFILE`, and optionally `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, `ADOTOOLKIT_LIVE_PLAN_ID` and `ADOTOOLKIT_LIVE_SUITE_ID` |
| `TestFailures.Live.ps1` | V-19–V-25. Optional fields may be absent. V-22 requires observed rerun details and distinct nested retry attempts, with complete paging; supplying build IDs alone does not pass | `ADOTOOLKIT_LIVE_PROFILE`, `ADOTOOLKIT_LIVE_TEST_BUILD_ID`; retry checks also need `ADOTOOLKIT_LIVE_RERUN_BUILD_ID` and `ADOTOOLKIT_LIVE_REATTEMPT_BUILD_ID` |
| `Triage.Live.ps1` | V-11/V-14: timeline retries, continuation headers and log range semantics, including 64-bit line counts. This file exists in the repository | `ADOTOOLKIT_LIVE_PROFILE`, `ADOTOOLKIT_LIVE_DEFINITION`, `ADOTOOLKIT_LIVE_BUILD_ID`; optionally `ADOTOOLKIT_LIVE_RETRIED_BUILD_ID` |
| `TestCase.Live.ps1` | V-01/02/03/05/10/13. V-10 acceptance contradicts the exclusion assumption; rejection is confirmed only if separate fields/expand control requests succeed. V-02 needs visual comparison; markup presence alone cannot prove formatting semantics | `ADOTOOLKIT_LIVE_PROFILE`, `ADOTOOLKIT_LIVE_TESTCASE_ID`; shared parameters also need `ADOTOOLKIT_LIVE_SHARED_PARAM_CASE_ID` |
| `Bulk.Live.ps1` | V-04/V-06. Repeated continuation tokens or the 50-page ceiling yield incomplete enumeration and cannot pass V-04 | `ADOTOOLKIT_LIVE_PROFILE`, `ADOTOOLKIT_LIVE_PLAN_ID`, `ADOTOOLKIT_LIVE_SUITE_ID` |

The profile must have a default project. Run a check in a new PowerShell window,
for example `pwsh -NoProfile -File .\tests\Live\Smoke.Live.ps1`.

With the corresponding variables set **at work**, run:

```powershell
pwsh -NoProfile -File .\tests\Live\Connection.Live.ps1
pwsh -NoProfile -File .\tests\Live\Shape.Live.ps1
pwsh -NoProfile -File .\tests\Live\Smoke.Live.ps1
pwsh -NoProfile -File .\tests\Live\TestFailures.Live.ps1
pwsh -NoProfile -File .\tests\Live\Triage.Live.ps1
pwsh -NoProfile -File .\tests\Live\TestCase.Live.ps1
pwsh -NoProfile -File .\tests\Live\Bulk.Live.ps1
```

Repeat `Shape.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID` set to each retry build
to observe its `pipelineReference` and sub-results. It samples the first run and an
unsuccessful result, so an absent path does not prove that the server never sends it.
V-07/V-18 still need controlled failing requests at work; the connection success
check cannot confirm error bodies or error-language behavior. V-15/V-26 links,
V-16 terminology, V-27 browser policy and V-29 runner text need manual acceptance.
There is no existing live probe for the optional V-28 alternative endpoints.

`Live.Common.ps1` contains pure schema/validation helpers. `tools/tests/LiveAudit.Tests.ps1`
tests them and isolated validation blocks using synthetic responses; `verify` never
runs any `*.Live.ps1` script. See the [approved audit fix evidence](archive/plans/server-2020-audit-fixes.md).

## Agent hooks

`.claude/settings.json` runs `tools/guard-git.ps1` before shell commands and
`tools/validate-edit.ps1` after file edits. The edit hook runs after the file is
written: it reports problems but cannot undo the change. Hooks and file permissions
reduce mistakes, but they are not a sandbox. Codex uses its own permissions.
