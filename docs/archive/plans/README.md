# Archived implementation plans

History, not a work queue. These plans delivered AdoToolkit v1 in slices 0 to 5 between
2026-09-15 and 2026-09-17. The plan and evidence files are unedited. Current rules are in
`AGENTS.md`; current work-PC live checks are in the newest `docs/release-<version>.md`.

## Plans and evidence

| Slice | Plan | Sessions | Acceptance IDs | Evidence |
| --- | --- | --- | --- | --- |
| 0 Foundation | [slice-0-foundation.md](slice-0-foundation.md) | 0.1–0.4 | S0-1 … S0-9 | [0.1](slice-0-session-0.1-evidence.md), [completion](slice-0-completion-evidence.md), [French review](slice-0-french-review.md) |
| 1 Work items and test step model | [slice-1-work-items.md](slice-1-work-items.md) | 1A–1C | S1-1 … S1-7 | [1A](slice-1-session-1A-evidence.md), [1B](slice-1-session-1B-evidence.md), [1C](slice-1-session-1C-evidence.md) |
| 2 Test Case reports | [slice-2-reports.md](slice-2-reports.md) | 2.1–2.2 | S2-1 … S2-8 | [2.1](slice-2-session-2.1-evidence.md), [2.2](slice-2-session-2.2-evidence.md) |
| 3 Bulk test export | [slice-3-bulk-export.md](slice-3-bulk-export.md) | 3.1–3.2 | S3-1 … S3-6 | None recorded |
| 4 Pipeline failure triage | [slice-4-pipeline-triage.md](slice-4-pipeline-triage.md) | 4.1–4.2 | S4-1 … S4-4 | [4.1](slice-4-session-4.1-evidence.md), [4.2](slice-4-session-4.2-evidence.md) |
| 5 Failed-test report | [slice-5-failed-test-report.md](slice-5-failed-test-report.md) | 5.1–5.4 | S5-1 … S5-10 | [5.1](slice-5-session-5.1-evidence.md), [5.2](slice-5-session-5.2-evidence.md), [5.3](slice-5-session-5.3-evidence.md), [5.4](slice-5-session-5.4-evidence.md) |

| Other record | Contents |
| --- | --- |
| [server-2020-audit-fixes.md](server-2020-audit-fixes.md) | 2026-09-17: findings `F01` to `F16`, each with its failing regression test and its correction |
| [0.8.0-test-failure-report.md](0.8.0-test-failure-report.md) | 2026-10-03: the 0.8.0 plan for faster failed-test retrieval and the report density pass, with its measurements, the developer's decisions and the evidence of each phase |
| [0.8.5-report-styling.md](0.8.5-report-styling.md) | 2026-10-03: the 0.8.5 plan for the failed-test report restyle, with its design, the developer's decisions over three rounds of previews and the evidence of each step |

- The acceptance criteria are defined in §22 of the
  [specification](../ado-toolkit-spec.md#22-delivery-slices-and-acceptance-criteria).
- Each acceptance ID belongs to exactly one slice plan.

## State when the plans were archived (2026-09-16)

| Milestone | Meaning | Recorded state |
| --- | --- | --- |
| M0 | Specification accepted, plans written | Reached 2026-09-15 |
| M1 | Project initialized | Reached in session 0.1 |
| M2 to M6 | Slices 0 to 4 done offline | Reached 2026-09-15 |
| M7 | Slice 5 done offline, v1 feature-complete | Sessions 5.1–5.4 implemented and verified; the manual browser check S5-9 was open |
| M8 | v1 installed at work | Open: the live criteria S0-9, S1-7, S2-8, S3-6, S4-4, S5-9 and S5-10 had not been run |

| Offline spike | Result |
| --- | --- |
| V-09, Pester ordering | Confirmed in session 0.1 |
| V-17, help culture folders | Confirmed in session 0.4: `fr-CA`, `fr-FR` and `fr` resolve the single `fr/` help file |
| V-27, report opened from `file://` | Automated Edge checks passed in both cultures; manual Edge and Chrome acceptance was open |

Later results are recorded in the release notes, not here.

## Reading the plans today

| The plans say | Today |
| --- | --- |
| The specification directly under "docs" | `docs/archive/ado-toolkit-spec.md` |
| The "docs/plans" folder | `docs/archive/plans/` |
| `/template-init`, `$template-init`, "the template" | Removed in 0.6.0; the repository is the product |
| A 300-second `project-check` timeout | 900 seconds, in `tools/lib/validation.ps1` |
| Kickoff prompts, entry and exit gates, cross-slice rules | Superseded by `AGENTS.md` and the skills in `.agents/skills/` |
| Sign and install the package on the work machine | Prebuilt, unsigned releases are installed with `Install-AdoToolkit.ps1`; see `docs/guides/getting-started.md` |
