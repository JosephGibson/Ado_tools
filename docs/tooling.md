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
   skipped or unrun tests, exits `2`.

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

## Agent hooks

`.claude/settings.json` runs `tools/guard-git.ps1` before shell commands and
`tools/validate-edit.ps1` after file edits. The edit hook runs after the file is
written: it reports problems but cannot undo the change. Hooks and file permissions
reduce mistakes, but they are not a sandbox. Codex uses its own permissions.
