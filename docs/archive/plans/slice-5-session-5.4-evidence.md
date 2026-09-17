# Slice 5 session 5.4 — Release review and evidence

Date: 2026-09-16. Session 5.4 is the final implementation session for Slice 5;
the module version remains **0.1.0**.

Implementation and offline acceptance S5-1 through S5-8 are complete. S5-9 manual
browser acceptance remains open, so **M7 is not yet signed off**. Signing,
installation, live server checks and fluent French review remain on the M8 work
track. No release has been published or installed by this review.

## Review and fixes

The review covered configuration and connection boundaries, HTTP/retry/paging,
work-item and Test Case retrieval, step/parameter parsing, report encoding,
build triage/log export, failed-test retrieval/history/attachments, PowerShell
binding and cancellation, packaging, and verification. Existing offline tests
exercise these areas; server assumptions remain explicitly unconfirmed.

| Finding | Fix and regression coverage |
| --- | --- |
| Attachment metadata was collected only for immediate sub-results, omitting nested data rows | Traverse all mapped sub-result levels for both listing and ownership; request each ID once per result. Regression verifies attachments on levels two and three stay with their owning attempt |
| Piped builds with the same definition, branch and timestamp could reuse a history window that excluded a different current build | Include the current build ID in the derived window cache key. Regression proves each current build appears exactly once |
| Finite but oversized `durationInMs` could overflow `TimeSpan` and abort retrieval | Treat out-of-range optional durations as absent, retaining the rest of the result. Five cases cover result, nested and rerun mappings |
| A successful `dotnet test` exit could hide skipped or undiscovered tests | Read TRX counters from fresh result directories for both cultures. Missing, skipped and inconclusive results are incomplete; failures stay failures. Nine tooling regression cases |
| Export help was a single long paragraph and overstated rollback and `-WhatIf` guarantees | Reformat English/French help; distinguish pre-commit rollback from cleanup warnings, explain upstream retrieval under `-WhatIf`, and state that directory destinations must exist |

Reused `AdoTestAttempt.WithAttachments` for retrieval copies to remove a second
field-by-field copy implementation. No dependencies, resource keys, CSP rules,
golden files or package version changed.

## Session 5.4 acceptance

- S5-3: an unreadable history build remains a warning and the report commits.
- S5-6: attachment bytes, generated names, PNG/JSON checks, local HTML links,
  inline limits, per-file/total budgets, retries and `-SkipAttachments` are covered.
- S5-7: injected failures, cancellation, locked destinations, existing generation
  folders, `-NoClobber`, `-WhatIf`, cleanup warnings and junction handling are covered.
- S5-1 through S5-8 appear in tagged tests; `TestFailures.Live.ps1` and bilingual
  help for all three failed-test cmdlets are present. The live script is linted
  by verification but is not executed at home.

## Verification

- Entry full gate: exit 0, `Status: pass`, no warnings; `project-check` **40.636 s**.
- Product gate after code fixes: exit 0; Release build has zero warnings/errors;
  **805 Core tests per culture** (`en-US`, `fr-CA`), zero failed/skipped;
  **83 product Pester tests**, zero failed/skipped; package inspection passes.
- Recorded release full gate: exit 0, `Status: pass`, no warnings;
  **98 tooling tests**, zero failed/skipped; `project-check` **31.971 s**.
  This run preceded the final evidence/status documentation update.
- No restores, dependency changes, installs, live ADO calls or Git lifecycle actions.

## Browser check (S5-9 / V-27)

Synthetic previews were generated through `TestFailureExporter` using a fake
attachment handler and the existing report/attachment fixtures. They are under
the ignored `artifacts/release-review/preview/` directory. This keeps review output
inside the workspace, rather than using the plan's suggested external temporary
directory. No real report or attachment data was used.

An isolated **Edge 153.0.4234.32** headless profile opened both previews through
`file://`. External HTTP(S) page requests were blocked. **18 checks passed** with
no JavaScript runtime exceptions:

| Check | en-US | fr-CA |
| --- | --- | --- |
| Hash-allowed script runs and filters by test name | Pass | Pass |
| Sibling PNG loads under `img-src 'self' data:` | Pass | Pass |
| Image dialog opens; Escape closes | Pass | Pass |
| Native details toggle | Pass | Pass |
| Chart bars contain SVG title text | Pass | Pass |
| Local HTML attachment opens | Pass | Pass |
| Clipboard unavailable: Copy selects text and reports fallback | Pass | Pass |
| Script execution disabled: cards, attempts, code, attachment links and chart data remain; enhancement controls stay hidden | Pass | Pass |

Screenshots and machine-readable observations are retained locally under
`artifacts/release-review/`. Title markup inspection does not prove visual hover
behavior. The clipboard check deliberately exercised the selection fallback.

The user was asked to perform the six-line manual checklist in both browsers.
Chrome was not found in the standard system/user install locations and was not
installed. **Manual Edge/Chrome results are pending.** S5-9 and V-27 remain open;
the headless results do not replace that acceptance or work-policy confirmation.

## French text for review

No new resource strings. Review the revised
[`Export-AdoBuildTestFailure` help](../commands/fr-CA/Export-AdoBuildTestFailure.md)
and the existing 5.4 labels for local copy, test output HTML, image description,
download failure, content mismatch, per-file/total limits, folder cleanup and
export progress. Prior 5.1–5.3 terminology lists remain applicable.

## Release handoff

1. Complete and record the manual Edge/Chrome S5-9 checklist; then mark M7 complete.
2. Follow W-1 through W-9 in the [roadmap](README.md#work-machine-track-m8): transfer
   source, build/sign/install at work, run live acceptance and confirm browser policy.
3. Complete fluent French review and record the work-machine results.

Package staging is local and unsigned. Only source is transferred to work; binaries
are built and signed there under the existing release contract.
