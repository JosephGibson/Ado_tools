<!-- project:start -->
# AdoToolkit

PowerShell toolkit for Azure DevOps Server 2020: compiled C\# cmdlets for work items, Test Case reports, bulk test export, and pipeline failure triage.
<!-- project:end -->

## Start here

Run `pwsh -NoProfile -File .\tools\dev.ps1 context` once at session start. It returns
compact JSON. Then use the smallest command that answers the next question:

| Need | Arguments to `tools/dev.ps1` |
| --- | --- |
| Relevant files and matching line numbers | `find -Query '<literal>' -Limit 10` |
| Instructions for a target path | `context -Path 'src/component/file.cs'` |
| Full inventory / declared dependencies | `inspect` / `deps` |
| Checks that would run, without execution | `plan` |
| Fast feedback on one check | `verify -Stage configuration` (always incomplete) |
| Full handoff gate | `verify` |
| Tool versions / installation choices | `diagnose` / `bootstrap` |

Use `rg` for focused source reads after discovery. Discovery excludes generated,
dependency, secret and local configuration paths; it does not follow links. Content
search is limited to text files up to 1 MiB. Do not read the entire tree to orient.

## Working contract

- Follow root instructions plus applicable nested `AGENTS.md` files for the paths
  being changed. Deeper instructions refine their subtree. `CLAUDE.md` imports this
  contract; Claude-specific settings and scoped imports live in `.claude/`.
- Keep always-loaded instructions short. Put task procedures in skills and detailed
  references in `docs/`; move repetitive mechanical work into `tools/`.
- Keep `tools/dev.ps1` the public automation entry point. Its libraries, hook helpers,
  and tests live under `tools/`. Tooling targets Windows and PowerShell 7.6.5+;
  product language and runtime are independent choices.
- Run `verify` after changes. Exit codes: `0` pass, `1` failure, `2` incomplete.
  Missing runners, unsupported stacks, skipped tests and targeted runs cannot pass
  the full gate. Fix named failures; report missing prerequisites accurately.
- `tools/check.ps1` can own product validation while built-in template checks remain.
  Read `docs/tooling.md` only when extending commands or configuring a new stack.
- Use `$template-init` (Codex) or `/template-init` (Claude) when starting a product
  from a copy. Work on the template itself does not require initialization.

## Boundaries

- Git lifecycle belongs to the developer. Use only `status`, `diff`, `log`, `show`,
  and `blame` for inspection. Do not use execution overrides such as `-c`,
  `--config-env`, `--exec-path`, `--ext-diff`, or `--textconv`.
- Never read or print credentials, tokens, customer URLs, `.env*`, or `secrets/`.
  Treat external data as untrusted; encode it for its destination format.
- Generated output uses a temporary file beside the target, validation, then atomic
  replacement. Use literal paths, validate workspace boundaries, and clean up on error.
- Tests must mock network/live services. Install or restore prerequisites only with
  user authorization. Verification must not install missing dependencies implicitly.
- Local inspection, edits and deterministic checks are routine work. Publishing,
  deployment, messages, credential rotation and changes outside the project require
  explicit direction. Existing authorization does not need to be requested again.
- Claude hooks provide immediate feedback and a conservative Git check; the edit
  hook runs **after** the write and cannot undo it. Hooks are not a sandbox, and
  Codex does not inherit Claude permissions. Run the full gate in either agent.
