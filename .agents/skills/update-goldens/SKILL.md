---
name: update-goldens
description: Regenerate and review tests/Fixtures/Reports after an intended change to report markup, text, styles or French wording.
---

# Update report goldens

The 54 goldens in `tests/Fixtures/Reports/` are written only by these tests:

| Test | Goldens |
| --- | --- |
| `GoldenReportTests` | 30 HTML/Markdown/JSON reports; 4 HTML-only rich/detailed reports |
| `MultiCaseDocumentTests.MultiCaseDocumentMatchesReviewedGolden` | 6 multi-case reports |
| `GoldenTestFailureReportTests` | 14 failed-test HTML reports |

1. Write the change's own regression test and make the product change. Golden differences
   show changed bytes, not correctness.
2. Run the comparison:

   ```powershell
   dotnet test tests/AdoToolkit.Core.Tests --configuration Release --no-restore --filter "FullyQualifiedName~GoldenReportTests|FullyQualifiedName~GoldenTestFailureReportTests|FullyQualifiedName~MultiCaseDocumentMatchesReviewedGolden"
   ```

3. Repeat with `ADOTOOLKIT_UPDATE_GOLDEN=1` for that command only. Restore its previous
   environment value in `finally`; never edit a golden by hand.
4. Inspect `git diff --stat -- tests/Fixtures/Reports` and every changed file's diff.
   Fix unrelated differences in the implementation and regenerate.
5. Repeat step 2 without the update flag, then run
   `pwsh -NoProfile -File .\tools\dev.ps1 verify`, requiring exit `0`.
   The gate clears the flag and compares in both cultures.

Goldens use UTF-8 without BOM and LF endings. HTML goldens replace the script body with
`__SCRIPT_ASSET__` and its hash with `sha256-__SCRIPT_SHA256__`; the tests restore them
for comparison. Rich/detailed variants show HTML-only formatting and `-IncludeDetail`.

A new variant needs a model in `ReportFixture`, `MultiCaseFixture` or
`TestFailureReportFixture`, inclusion in the test's variant list, and a catalog entry in
`tests/Fixtures/README.md`.

Report which files changed and why. For layout or wording changes, name the rendered
English and French files for developer review in the handoff; release notes list these
under "Existing tests changed".
