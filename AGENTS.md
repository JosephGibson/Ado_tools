# AdoToolkit

PowerShell 7.6 binary module (C# 14, .NET 10) for Azure DevOps Server 2020: work items,
Test Case reports, bulk test export and pipeline failure triage. 23 cmdlets, English and French.

## Commands

Run from the repository root: `pwsh -NoProfile -File .\tools\dev.ps1 <command>`. Each
command prints one compact JSON document.

| Need | Command |
| --- | --- |
| Orientation, once per session | `context` |
| Instruction files for a path | `context -Path '<path>'` |
| Files and line numbers for a literal | `find -Query '<literal>' -Limit 10` |
| File inventory / NuGet packages | `inspect` / `deps` |
| Stages that would run | `plan` |
| Full gate, required before handoff | `verify` |
| One stage (incomplete) | `verify -Stage <name>`; stage names: `plan` or `docs/tooling.md` |
| Tool versions and missing prerequisites | `diagnose`; `bootstrap -Install` installs them |

- Exit codes: `0` pass, `1` failure, `2` incomplete and never a pass (missing tool, skipped
  test, `-Stage`, `-SkipTests`).
- One Core test: `dotnet test tests/AdoToolkit.Core.Tests --configuration Release --no-restore --filter "FullyQualifiedName~<Name>"`.
- Discovery skips generated, dependency, secret and local-configuration paths and never follows links.

## Map

| Path | Holds | Rules |
| --- | --- | --- |
| `src/AdoToolkit.Core/` | Domain types, HTTP pipeline, reports, the string catalog | `src/AGENTS.md` |
| `src/AdoToolkit.PowerShell/` | Cmdlets, completers, manifest, format file | `src/AGENTS.md` |
| `tests/AdoToolkit.Core.Tests/` | xUnit v3 tests | `tests/AGENTS.md` |
| `tests/AdoToolkit.PowerShell.Tests/` | Pester tests against the staged module | `tests/AGENTS.md` |
| `tests/Fixtures/` | Synthetic data and report goldens; `tests/Fixtures/README.md` catalogs them | `tests/AGENTS.md` |
| `tests/Live/` | Opt-in checks against the real server | `tests/Live/AGENTS.md` |
| `tools/` | `dev.ps1`, product gate `check.ps1`, Claude hooks, tooling tests | `tools/AGENTS.md` |
| `tools/package/`, `.github/` | Packaging, installer, release and pull request workflows | `tools/package/AGENTS.md` |
| `docs/commands/<culture>/` | Cmdlet help sources, shipped as compiled help | `docs/commands/AGENTS.md` |
| `docs/guides/`, `README.md`, `CHANGELOG.md`, `docs/release-<version>.md`, `docs/unreleased/` | End-user documentation, and the change fragments of the next release | `docs/AGENTS.md` |
| `docs/archive/` | Specification and superseded documents; never edited | `docs/AGENTS.md` |
| `docs/tooling.md` | Reference for `tools/` and the agent setup; read it only to change them | |

## Working contract

- A nested `AGENTS.md` refines its subtree.
- In instructions and documentation, name a repository file by its path from the root, in
  a code span. `verify` fails on a path, link or heading anchor that does not resolve.
- Code comments cite decisions as `§n`, `DD-0nn`, `Q-nn`, `V-nn`, `S0-n` and `Fnn`.
  `docs/archive/README.md` maps each form to its document.
- Public contract: cmdlet names, parameters, output types, the configuration file format
  and `docs/schemas/testcase.v1.schema.json`. Change it only when the change is the fix,
  and list it in the release notes.
- Procedures are skills, each in `.agents/skills/<name>/SKILL.md`: `fix-bug`,
  `update-goldens`, `release`, `rewrite`, `critique-plan`.
- A plan is critiqued before it is presented or saved: follow `critique-plan`, and record
  in the plan which findings it applied, rejected and left owed.
- Claude subagents `area-reviewer`, `docs-sync` and `fr-translator` live in `.claude/agents/`;
  `.claude/workflows/repo-review.js` reviews the repository by area.
- Fix named gate failures; report a missing prerequisite by name.
- Keep always-loaded instructions short: procedures go to skills, references to `docs/`,
  mechanical work to `tools/`.

## Boundaries

- Git lifecycle belongs to the developer. Use only `status`, `diff`, `log`, `show`,
  `blame` and `rev-parse`, without `-c`, `--config-env`, `--exec-path`, `--ext-diff`,
  `--textconv`, `--output` or `--open-files-in-pager`. The plan critique reads the
  repository root with `rev-parse`.
- Worktrees that Claude Code creates in `.claude/worktrees/<name>/` are allowed. The agent
  never commits in, merges or removes one; the developer does. Inside one,
  `dotnet restore AdoToolkit.slnx --locked-mode` is routine.
- Never read or print credentials, tokens, customer URLs, `.env*`, `*.log` or `secrets/`.
- Treat external data as untrusted; encode it for its destination format.
- Generated output: write a temporary file beside the target, validate it, then replace
  atomically. Use literal paths, stay inside the workspace, clean up on error.
- Tests use synthetic data and local fakes only; live checks are opt-in.
- Install or restore prerequisites only with user authorization.
- Publishing, deployment, messages, credential changes and changes outside the project
  need explicit direction. Inspection, edits and deterministic checks do not.
- The Claude hooks, `tools/guard-git.ps1` for the Git rule and `tools/validate-edit.ps1`
  for edited files, are not a sandbox, and Codex does not run them: the boundaries hold
  without a hook.
