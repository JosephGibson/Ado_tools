# Developer tooling reference

Read this when configuring or extending the template. Daily agents start with
`dev.ps1 context` and need not load this reference.

## Commands and output

Use `pwsh -NoProfile -File .\tools\dev.ps1 <command>`. Run from the repository root;
the script resolves its repository from its own location.

| Command | Behavior |
| --- | --- |
| `context [-Path <target>]` | Bounded orientation; target adds applicable root/nested instruction paths without reading their bodies |
| `find -Query <literal> [-Limit 20]` | Case-insensitive paths first, then content; matching line numbers, 1 MiB content limit, `LimitReached` indicates the result budget filled |
| `inspect` | Full detected manifests, entry points, directories and instruction paths |
| `deps [-Limit 20]` | Declared npm, NuGet and requirements.txt dependencies; other manifests listed without inventing a dependency graph |
| `plan` | Preview stage names, executable arguments, working directories and dependencies; executes no checks |
| `verify` | Complete applicable gate, with status, concise findings and per-stage duration |
| `verify -Stage <name>` | Selected stage plus prerequisites; pass of the selected checks still exits 2 |
| `verify -SkipTests` | Inner loop only; always incomplete |
| `diagnose` | Local tool availability and supported versions |
| `bootstrap [-Install]` | Preview prerequisites; explicit install adds supported missing tools |
| `init -ProjectName <name> -Description <purpose>` | Update marked README/AGENTS identity blocks; preflight both, escape Markdown, atomic replacement per file, rollback on a caught write error |

Exit codes are 0 for `ok/pass`, 1 for `fail`, 2 for `unavailable/incomplete`.
`-Format Object` returns objects when invoked with `&` inside PowerShell; a separate
`pwsh -File` process uses JSON. Arrays remain arrays even with zero or one element.
For several stages inside PowerShell use `& ./tools/dev.ps1 verify -Stage @('configuration','tooling-layout')`.

Discovery uses ripgrep when present and pruned filesystem traversal otherwise.
Both use the template's explicit exclusions, not machine-specific ignore settings.
Hidden shared configuration is discoverable; dependency/build directories, links,
secret paths and personal configuration are excluded. File listings can include
binary or large assets; content search skips binary content and files over 1 MiB.
`find` is a locator, not a full-text export. No persistent index/cache is maintained.

## Validation coverage

| Detected input | Checks |
| --- | --- |
| PowerShell | Parser plus PSScriptAnalyzer; only `*.Tests.ps1` files invoke Pester 5.x |
| JSON / XML / .NET project XML | Parsing; XML DTDs and external resolution disabled |
| Claude configuration | Hook objects, helper locations and local instruction imports; hooks stay under `tools/` |
| .NET projects | Each project builds with `--no-restore`; explicit test projects run after their successful build with `--no-build --no-restore` |
| Node package.json | Existing lint/build/test scripts, from each package directory, using its nearest declared package manager or lockfile; missing tests are incomplete |
| Python / Rust / Go / Java | Detected and reported incomplete until a project-owned check defines its environment and checks |

Recognized source under `src/` with no matching manifest also reports incomplete.

.NET test inference recognizes explicit `IsTestProject=true`, Microsoft.NET.Test.Sdk
and TUnit references. Conditional MSBuild properties, custom test runners, linked
projects and solution orchestration belong in a project check.
Node workspace scripts may aggregate other packages; use a project check to avoid
duplicate work or choose a workspace order. Package manager declarations take
precedence over lockfiles; conflicting lockfiles without a declaration are incomplete.

All dependencies must already be installed/restored. External stages run with
`CI=true`, `NO_COLOR=1`, and Corepack network bootstrapping disabled. This does not
sandbox arbitrary project scripts: their author must ensure they terminate and mock
live services. Each external stage has a five-minute timeout, terminates its process
tree on timeout, and retains at most the last 120 output lines, capped at 2,000
characters each, before summarizing. Internal stages return structured findings.

## Owning product checks

Add `tools/check.ps1` when the built-in product plan is insufficient. It **replaces
inferred product checks**; PowerShell/configuration/tooling checks still run.
The script accepts `[switch] $SkipTests`, runs from the repository root in a clean
PowerShell child, emits concise diagnostic lines, and exits 0/1/2 as above.
Never call `dev.ps1 verify` from it: that would recurse.

Use explicit executable argument arrays and preserve exit codes immediately.
For example, a prepared Python project could run its environment's
`./.venv/Scripts/python.exe -m pytest`, returning 1 on test failure and 2 when
the interpreter or pytest is missing. Ruff checks belong here too. Prepare the
environment in a separate, authorized setup step. Cargo checks can use
`--offline --locked`; Go projects should explicitly configure an offline module
cache and local toolchain. The template does not guess those environments.

For a reusable new adapter, add its stage construction in
`tools/lib/validation.ps1` and fixture-based tests in `tools/tests/`.
A stage has `Name`, `Executable`, `Arguments`, optional `WorkingDirectory`,
`DependsOn`, and `TimeoutSeconds` (1–3600). In-process stages return
`Failures`, `Summary`, `Warnings` and optionally `Unavailable`.
The default gate decides success from exit codes or structured state, never from
a reassuring line in console output.

## Prerequisites and useful tools

| Tool | Value and integration | Installation (explicit user action) |
| --- | --- | --- |
| PowerShell 7.6.5+ | Runs the developer CLI and Claude hooks | `winget install --id Microsoft.PowerShell --exact --source winget` |
| ripgrep | Fast shared discovery and targeted searches; fallback works without it | `winget install --id BurntSushi.ripgrep.MSVC --exact --source winget` |
| Pester 5.x | Tooling regression tests; required for the full gate | `Install-Module Pester -MinimumVersion 5.0 -MaximumVersion 5.999.999 -Scope CurrentUser -Repository PSGallery` |
| PSScriptAnalyzer | PowerShell correctness checks; required for the full gate | `Install-Module PSScriptAnalyzer -Scope CurrentUser -Repository PSGallery` |
| .NET SDK | Detected .NET projects; install the SDK selected by the product's global.json | Use the matching SDK installer from [Microsoft](https://dotnet.microsoft.com/download/dotnet); restore separately before verification |
| Ruff (Python only) | Fast lint/format checks through the product gate | `uv tool install ruff` when uv is already part of the chosen stack |
| ast-grep (optional) | Structural navigation/refactors when text search is insufficient; diagnosed only | `cargo install ast-grep --locked` when Rust is already installed |

`bootstrap -Install` can install ripgrep, Pester and PSScriptAnalyzer. It does not
install optional tools or select a product SDK. Pester is deliberately constrained
to 5.x because the adapter uses its result contract; upgrade it with regression tests.
No additional installation is needed when `diagnose` finds all required tools.

Claude configuration uses documented executable-plus-argument hooks. The post-edit
hook parses supported file types **after** the edit and reports problems; it does
not reject or revert the write. File permissions and the Git guard reduce mistakes
but do not constrain every possible subprocess. Configure the agent's actual sandbox
for stronger enforcement. Codex uses its own permissions.
