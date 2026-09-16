# Design decisions and research

Reviewed 2026-09-13 against primary documentation. This reference is intentionally
outside the agents' always-loaded instructions. These are template design choices,
not claims that one repository structure fits every project.

| Evidence | Decision |
| --- | --- |
| [Codex AGENTS.md discovery](https://developers.openai.com/codex/guides/agents-md) describes layered instructions from root to deeper directories | Keep a short shared root contract and a tools-specific contract; expose target instruction paths through context |
| [Codex skills](https://developers.openai.com/codex/skills) load descriptions first and bodies when needed | Keep initialization in one narrowly triggered skill and route Claude to the same body |
| [Claude memory](https://code.claude.com/docs/en/memory) documents scoped rules and notes that imports still consume context | Use a small root import and a scoped tooling import; move command details here and into tooling.md |
| [Claude best practices](https://code.claude.com/docs/en/best-practices) emphasize verification and constrained context | One compact orientation command, previewable checks, explicit incomplete outcomes and a full handoff gate |
| [Claude hooks](https://code.claude.com/docs/en/hooks) document deterministic events, exec-form args and post-tool timing | Retain shell-free hook argument arrays and fast syntax feedback; correct the old claim that a post-edit hook rejects a write |
| [Claude permissions](https://code.claude.com/docs/en/permissions) distinguish file rules from process sandboxing | Keep project-anchored sensitive-path rules and remove broad Git allow entries; do not describe these as a sandbox |
| [ripgrep guide](https://github.com/BurntSushi/ripgrep/blob/master/GUIDE.md) explains filtering, hidden files, binary handling and configuration | Explicit exclusions shared with the fallback, no user rg config, bounded locator results and matching line numbers |
| [PSScriptAnalyzer](https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/overview?view=ps-modules) returns static analysis diagnostics | Parse every eligible PowerShell file and return actual findings; missing analyzer means incomplete |
| [Pester 5 configuration](https://pester.dev/docs/v5/usage/configuration) exposes structured run results and output control | Pin the supported major, suppress chatter, and account for discovery/setup failures and skipped/empty runs |
| [.NET build](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build) documents implicit restore and its suppression | Use explicit project paths and --no-restore; separate environment preparation from validation |
| [npm scripts](https://docs.npmjs.com/cli/using-npm/scripts/) describes package scripts and lifecycle behavior | Inspect declared scripts and package ownership; do not report an absent script as a passed check |
| [Ruff](https://docs.astral.sh/ruff/) and [ast-grep](https://ast-grep.github.io/guide/introduction.html) supply focused linting and structural search | Recommend conditionally; avoid mandatory tools without a concrete task |

## Changes with the largest payoff

The original public script mixed navigation, package inspection, execution,
and installation. It now delegates to four responsibility-based libraries. Keeping
one public command avoids asking agents to choose among numerous wrappers; file
boundaries make targeted implementation reads smaller. Tooling tests moved out of
the future product's test directory.

Validation previously guessed root-level commands, chose the first solution, and
could call nonexistent npm scripts with --if-present. Python/Rust/Go defaults could
select the wrong environment or trigger downloads, and Java could be detected without
contributing a check. The revised plan handles explicit .NET/Node projects and
requires a project-owned gate for environments it cannot safely infer. This trades
some out-of-box breadth for checks whose coverage is visible and reviewable.

The root contract now states decisions and routes to commands. Initialization moved
from a self-deleting procedural interview to a deterministic, repeatable identity
update plus a short skill for the product-specific work. It no longer assumes all
PowerShell products target 5.1 or changes global compatibility rules to cover both
tooling and product code.

## Deliberate omissions

- No Git lifecycle automation, automatic commits, branch orchestration or Git hooks.
  Read-only Git inspection remains available to the developer/agent.
- No vector index, repository snapshot, checked-in tree dump or context cache.
  They introduce freshness and dependency problems without evidence of benefit here.
- No duplicate AGENTS/CLAUDE manuals, agent-specific copies of the same skill, or
  unconditional language rules in the root context.
- No mandatory fd, bat, jq, fzf or broad MCP bundle. Ripgrep and PowerShell already
  cover the current scripted discovery/JSON needs; interactive utilities can remain
  developer preferences.
- No dependency-hiding lockfile diff attributes. Generated lockfiles remain reviewable.
- No CI provider scaffolding before a host is chosen. A future CI job should prepare
  prerequisites explicitly and run the exact same full `dev.ps1 verify` gate.

## Remaining extensions when a product warrants them

Add product-specific formatter/analyzer configuration, pinned SDK/dependency versions,
a check script for its environment, and CI that runs the shared gate. Add narrower
file/test selection only with dependency-aware coverage; stage selection already
provides a fast loop without claiming full coverage. Consider structured compiler
diagnostics or retained failure artifacts once concise stage summaries prove
insufficient. Larger monorepos may warrant cached discovery, but should measure
latency and invalidation costs first.
