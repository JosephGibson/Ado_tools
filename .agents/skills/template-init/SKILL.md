---
name: template-init
description: Start a product from a copy of this template, setting its identity and chosen stack. Use for project initialization, not for maintenance or review of the template itself.
---

# Initialize a project

Infer the name, purpose and stack from the user's request. Ask only for missing
information needed to proceed; a stack that is not chosen yet is valid.

Run `pwsh -NoProfile -File .\tools\dev.ps1 init -ProjectName '<name>' -Description '<purpose>'`
with correctly quoted arguments. It updates the marked identity blocks in README.md
and AGENTS.md, validates both before writing, and preserves the shared workflow.

Scaffold the requested product under `src/` and tests under `tests/`, using its native
manifest. Product runtime requirements come from the user; do not assume PowerShell
5.1 or install unrelated services, agent plugins or permission rules.

Run `dev.ps1 plan`. If inferred checks do not fit, read `docs/tooling.md` and add
`tools/check.ps1`. Add nested instructions only for concrete product constraints.
Run the full `dev.ps1 verify` and report its actual status and missing prerequisites.

Keep this skill and the tooling reusable. Initialization does not delete itself.
