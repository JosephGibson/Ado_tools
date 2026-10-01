---
name: fix-bug
description: Fixes a defect in AdoToolkit (src/, tests/ or tools/) with a test that fails before the fix. Use when asked to fix a bug, a wrong result, an unhandled error, a crash or a review finding in this repository.
---

# Fix a bug

Done when a test that failed before the fix passes and
`pwsh -NoProfile -File .\tools\dev.ps1 verify` exits `0`.

## 1. Confirm that it is a bug

- Read the citations in and around the code (`§n`, `DD-0nn`, `Q-nn`, `V-nn`) and the
  section they name; `docs/archive/README.md` resolves them.
- Behavior that a recorded decision or a pinned fixture requires is not a bug. Report it
  and stop.
- Server behavior marked `V-nn` is unconfirmed. Do not guess a new shape: make the live
  check in `tests/Live/` decide it, and say that it must run at work.

## 2. Write the failing test first

| Defect in | Test goes in | Run it |
| --- | --- | --- |
| `src/AdoToolkit.Core/` | `tests/AdoToolkit.Core.Tests/`, same folder as the type | `dotnet test tests/AdoToolkit.Core.Tests --configuration Release --no-restore --filter "FullyQualifiedName~<TestClass>"` |
| A cmdlet in `src/AdoToolkit.PowerShell/` | `tests/AdoToolkit.PowerShell.Tests/<Area>.Pester.ps1` | `pwsh -NoProfile -File .\tools\dev.ps1 verify -Stage project-check` |
| `tools/` | `tools/tests/<Area>.Tests.ps1` | `pwsh -NoProfile -File .\tools\dev.ps1 verify -Stage powershell-test` |

- Extend an existing test class or file for the area before adding a new one.
- If Core code cannot be reached from a test, add an `internal` seam that keeps behavior
  unchanged (`AdoToolkit.Core.Tests` sees Core internals), then test through it.
- Run the test before changing the product and keep the failing output. A test that
  already passes proves nothing: fix the test.
- Pester tests run against the staged module, so a cmdlet test fails or passes only after
  `project-check` has rebuilt and restaged it.

## 3. Fix the cause

- Fix the cause, not the symptom, and every place that shares it.
- A new user-facing string goes into `Strings.resx` and `Strings.fr.resx` with an
  `AdoMessage` member (`src/AGENTS.md`).
- A change to rendered report output changes the goldens: use the `update-goldens` skill.
- A change that users can see updates the help topics in both cultures and the guides
  (`docs/AGENTS.md`).
- Keep the public contract. If the fix must change it, say so in the report.

## 4. Verify and report

1. Rerun the test from step 2: it passes.
2. Run `pwsh -NoProfile -File .\tools\dev.ps1 verify`. Exit `2` names a missing
   prerequisite; it is not a pass.
3. Report: the file and the cause, the test and its failure before the fix, any change to
   the public contract, and anything found but not fixed, with the reason.
