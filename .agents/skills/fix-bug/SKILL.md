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
   | Tool | `tools/tests/<Area>.Tests.ps1` | `pwsh -NoProfile -File .\tools\dev.ps1 verify -Stage powershell-test`, or the one file after `Import-Module Pester -RequiredVersion 5.9.1`: a bare `Invoke-Pester` may load Pester 6 |

   Run the test before changing the implementation. Keep its failure message and the run's
   totals: they go into the fragment of step 4, and nothing later can produce them again. If
   it passes, correct the reproduction. A failure that shows only under a condition, such as
   a console code page, is recorded with that condition. Cmdlet tests require rebuilding and
   staging, which `project-check` does. A stage-only run exits `2` when its checks pass.
3. Fix the cause everywhere it is shared, following the subtree instructions.
   Update the guides and both help cultures that describe changed behavior.
   For required rendered-output changes, use `update-goldens`.
4. Write the change fragment, `docs/unreleased/<yyyyMMdd-HHmmss>-<slug>.json`, in the format
   of `docs/unreleased/README.md`: `kind` `fixed`, `security` for a vulnerability, or
   `internal` when the fix changes only tools, tests or agent files and nothing a user of the
   module sees; the `changelog` line; the `finding` with its cause and fix; the
   `regressionTest`; the `files`; and `preFix` with the failure and the totals of step 2. When
   no test can show the defect, `preFix` is `{ "none": "<reason>" }`; when its failure was not
   observed, `{ "notReproduced": "<reason>" }`. Never claim evidence that was not observed.
   Add `visible` or `contract` when the fix changes what a user sees or the public contract.
5. Rerun the regression, then `pwsh -NoProfile -File .\tools\dev.ps1 verify`, which also checks
   the fragment. Completion requires a passing regression and full-gate exit `0`.
6. Report the file and cause, the regression and its pre-fix failure, the fragment, any
   public contract change, and findings left unfixed with reasons.
