---
name: fix-bug
description: Fix a defect in AdoToolkit src/, tests/ or tools/ with a regression test that fails before the fix.
---

# Fix a bug

1. Read the code's citations; `docs/archive/README.md` resolves them. Behavior required
   by a recorded decision or pinned fixture is not a bug: report it without changing it.
   A `V-nn` server assumption needs its check in `tests/Live/` run by the developer at
   work; do not invent a replacement wire shape.
2. Extend the existing test class or file for the area. If necessary, expose an
   `internal` Core member or extract a script function without changing behavior.

   | Defect | Regression test | Run before and after |
   | --- | --- | --- |
   | Core | Same folder as the type under `tests/AdoToolkit.Core.Tests/` | `dotnet test tests/AdoToolkit.Core.Tests --configuration Release --no-restore --filter "FullyQualifiedName~<TestClass>"` |
   | Cmdlet | `tests/AdoToolkit.PowerShell.Tests/<Area>.Pester.ps1` | `pwsh -NoProfile -File .\tools\dev.ps1 verify -Stage project-check` |
   | Tool | `tools/tests/<Area>.Tests.ps1` | `pwsh -NoProfile -File .\tools\dev.ps1 verify -Stage powershell-test` |

   Run the test before changing the implementation and retain its failing output.
   If it passes, correct the reproduction. Cmdlet tests require rebuilding and staging,
   which `project-check` does. A stage-only run exits `2` when its checks pass.
3. Fix the cause everywhere it is shared, following the subtree instructions.
   Update the guides and both help cultures that describe changed behavior.
   For required rendered-output changes, use `update-goldens`.
4. Rerun the regression, then `pwsh -NoProfile -File .\tools\dev.ps1 verify`.
   Completion requires a passing regression and full-gate exit `0`.
5. Report the file and cause, regression and pre-fix failure, any public contract
   change, and findings left unfixed with reasons.
