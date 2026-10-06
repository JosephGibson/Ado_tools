# Archive

Superseded documents, kept for reference. Every file was moved here unedited and is not
maintained; only this index and [plans/README.md](plans/README.md) change. Relative links
inside the files that point outside `docs/archive/` no longer resolve. Current
documentation starts at the [README](../../README.md).

| Document | Contents |
| --- | --- |
| [ado-toolkit-spec.md](ado-toolkit-spec.md) | Technical specification v1.0: architecture, behavior, security, testing, packaging, design decisions, open questions and the verification ledger |
| [plans/](plans/README.md) | Slice plans, session evidence, the Server 2020 audit fixes and the 0.8.0, 0.8.5, 0.9.15, 0.10.5 and 0.11.0 plans |
| [design.md](design.md) | Design rationale for the first version of the developer tooling in `tools/` |
| [release-0.2.0.md](release-0.2.0.md) | 0.2.0 release audit: findings, Server 2020 REST contracts and the rules for work-PC live checks |
| [release-0.3.0.md](release-0.3.0.md) | 0.3.0 release notes: failed-test report changes and the portable release |
| [release-0.4.0.md](release-0.4.0.md) | 0.4.0 release notes: bugs of failed tests, security fixes and V-30 |
| [release-0.5.0.md](release-0.5.0.md) | 0.5.0 release notes: profile defaults for builds and test plans, report grouping by named test run |
| [release-0.6.0.md](release-0.6.0.md) | 0.6.0 release notes: Test Case report redesign, `-IncludeDetail`, the repository review, V-31 and V-32 |
| [release-0.6.5.md](release-0.6.5.md) | 0.6.5 release notes: the changelog, the pull request check, workflow lint and the release handoff; no product change |
| [release-0.7.0.md](release-0.7.0.md) | 0.7.0 release notes: concurrent requests, the Open bugs view, attachments of every test run, the failed-test report rework and V-33 |
| [release-0.7.5.md](release-0.7.5.md) | 0.7.5 release notes: audit fixes, French spacing, help and guide corrections and tooling hardening |
| [release-0.7.10.md](release-0.7.10.md) | 0.7.10 release notes: squash merge of pull requests in the release procedure; no product change |
| [release-0.8.0.md](release-0.8.0.md) | 0.8.0 release notes: faster failed-test retrieval, `-SkipAttachments`, the planned history read, the report density pass and V-34 to V-36 |
| [release-0.8.5.md](release-0.8.5.md) | 0.8.5 release notes: the failed-test report restyle, with the Overview cards, the error clusters and the bug coverage; no cmdlet or schema change |
| [release-0.9.0.md](release-0.9.0.md) | 0.9.0 release notes: the agent worktrees, the three subagents, the review workflow, and the gate and the test runs side by side; no product change |
| [release-0.9.5.md](release-0.9.5.md) | 0.9.5 release notes: surrogate-safe report cuts, the pipeline identifier guard, and the gate, package and live-check fixes |
| [release-0.9.10.md](release-0.9.10.md) | 0.9.10 release notes: the plan critique skill and its hooks, `rev-parse` in the Git boundary and the `gh` release handoff; no product change |
| [release-0.9.15.md](release-0.9.15.md) | 0.9.15 release notes: the age and the owner of each open bug in the failed-test report, the New marker and V-37 |
| [release-0.10.0.md](release-0.10.0.md) | 0.10.0 release notes: `Export-AdoBuildTestFailure -Format Csv`, one formula-safe CSV file per build, and the manual Excel check |
| [release-0.10.5.md](release-0.10.5.md) | 0.10.5 release notes: the quality pass, 22 review fixes with plain-text console tables and typed errors for bodies that do not decompress, and faster tests |
| [release-0.11.0.md](release-0.11.0.md) | 0.11.0 release notes: `Update-AdoToolkit`, the newest GitHub release installed beside the running copy after checking both of its SHA-256 checksums, and V-38 |

Release notes for the current and the previous version are in `docs/`.

## Citations in the code

Comments in `src/`, `tests/` and `tools/`, test tags and cmdlet help notes cite these documents.

| Citation | Resolves to |
| --- | --- |
| `§n`, `§n.m` | A numbered section of [ado-toolkit-spec.md](ado-toolkit-spec.md) |
| `DD-0nn` | A [design decision](ado-toolkit-spec.md#23-design-decisions), §23 |
| `Q-nn` | An [open question](ado-toolkit-spec.md#24-open-questions-for-the-next-pass) and its answer, §24 |
| `V-01` to `V-29` | A [verification ledger](ado-toolkit-spec.md#25-verification-ledger) entry, §25 |
| `V-30` | The bug routes on Server 2020, under [Known limitations](release-0.4.0.md#known-limitations) in the 0.4.0 notes |
| `V-31`, `V-32` | The requests of `Export-AdoTestCase -IncludeDetail`, under [Known limitations](release-0.6.0.md#known-limitations) in the 0.6.0 notes |
| `V-33` | Concurrent requests of `Get-AdoBuildTestFailure` and `Export-AdoBuildTestFailure` on Server 2020, under [Known limitations](release-0.7.0.md#known-limitations) in the 0.7.0 notes |
| `V-34` to `V-36` | Server 2020 behavior that `tests/Live/Probes.Live.ps1` probes for the next version: result lists with details, compressed responses and the attachments of sub-results, under [Known limitations](release-0.8.0.md#known-limitations) in the 0.8.0 notes |
| `V-37` | `System.CreatedDate` and `System.AssignedTo` in a work item batch field projection, and the shape of the identity, under [Known limitations](release-0.9.15.md#known-limitations) in the 0.9.15 notes |
| `V-38` | Whether the work network's proxy lets `Update-AdoToolkit` reach `api.github.com`, `github.com` and `release-assets.githubusercontent.com`, checked by `tests/Live/Update.Live.ps1`, under [Known limitations](release-0.11.0.md#known-limitations) in the 0.11.0 notes |
| `V-39` to `V-42` | The error classifier of the failed-test report on Server 2020 and its agents: the error message in result listings, checked by `tests/Live/Probes.Live.ps1`, the MSTest texts in each language, the French Windows and .NET Framework texts, and the browser driver texts, under [Known limitations](../release-0.12.0.md#known-limitations) in the 0.12.0 notes |
| `S0-n` to `S5-n` | An [acceptance criterion](ado-toolkit-spec.md#22-delivery-slices-and-acceptance-criteria), §22; also a test tag |
| `F01` to `F16` | A finding in [plans/server-2020-audit-fixes.md](plans/server-2020-audit-fixes.md) |
