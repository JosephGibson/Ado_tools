---
name: update-goldens
description: Regenerates the golden report files in tests/Fixtures/Reports after an intended change to rendered report output, and reviews the difference. Use when a golden report test fails because report markup, text, styles or French wording changed on purpose.
---

# Update the report goldens

`tests/Fixtures/Reports/` holds 50 golden reports. Three test classes compare rendered
bytes with them and rewrite them only when `ADOTOOLKIT_UPDATE_GOLDEN` is `1`.

| Test | Goldens |
| --- | --- |
| `GoldenReportTests` | `testcase-<variant>.<culture>.<html\|md\|json>`, 30 files, and `testcase-<rich\|detailed>.<culture>.html`, 4 files |
| `MultiCaseDocumentTests.MultiCaseDocumentMatchesReviewedGolden` | `testcases-multi.<culture>.<html\|md\|json>`, 6 files |
| `GoldenTestFailureReportTests` | `testfailures-<variant>.<culture>.html`, 10 files |

## Steps

1. Make the product change and write its own test first. A golden shows that output
   changed, not that the change is right.
2. See which goldens differ:

   ```powershell
   dotnet test tests/AdoToolkit.Core.Tests --configuration Release --no-restore --filter "FullyQualifiedName~GoldenReportTests|FullyQualifiedName~GoldenTestFailureReportTests|FullyQualifiedName~MultiCaseDocumentMatchesReviewedGolden"
   ```

3. Regenerate, with the variable set for that one command only:

   ```powershell
   $env:ADOTOOLKIT_UPDATE_GOLDEN = '1'
   try { dotnet test tests/AdoToolkit.Core.Tests --configuration Release --no-restore --filter "FullyQualifiedName~GoldenReportTests|FullyQualifiedName~GoldenTestFailureReportTests|FullyQualifiedName~MultiCaseDocumentMatchesReviewedGolden" }
   finally { $env:ADOTOOLKIT_UPDATE_GOLDEN = $null }
   ```

4. Review every changed file: `git diff --stat -- tests/Fixtures/Reports`, then
   `git diff -- tests/Fixtures/Reports/<file>`. Each changed line must follow from the
   intended change. Any other difference is a defect: fix the code and regenerate.
5. Run the command from step 2 again, without the variable. It passes.
6. Run `pwsh -NoProfile -File .\tools\dev.ps1 verify`. The gate clears the variable and
   compares the goldens under `en-US` and `fr-CA`.

## Rules

- Never edit a golden by hand.
- A golden is exact UTF-8 without a BOM and with LF line ends.
- In an HTML golden the script body is `__SCRIPT_ASSET__` and its hash is
  `sha256-__SCRIPT_SHA256__`; the test puts them into the rendered report before comparing.
- `rich` and `detailed` are HTML-only variants of `GoldenReportTests`: they show formatted
  steps and the details of `-IncludeDetail`, which Markdown and JSON do not render.
- A new variant needs its model in `ReportFixture`, `MultiCaseFixture` or
  `TestFailureReportFixture`, its name in the test's variant list and an entry in
  `tests/Fixtures/README.md`.
- Report which goldens changed and why. A change to layout or wording needs the user's
  review of the rendered English and French reports: name the files in the handoff. The
  `release` skill lists them under "Existing tests changed" in the release notes.
