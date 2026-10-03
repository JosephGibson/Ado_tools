# Archive

Superseded documents, kept for reference. Every file was moved here unedited and is not
maintained; only this index and [plans/README.md](plans/README.md) change. Relative links
inside the files that point outside `docs/archive/` no longer resolve. Current
documentation starts at the [README](../../README.md).

| Document | Contents |
| --- | --- |
| [ado-toolkit-spec.md](ado-toolkit-spec.md) | Technical specification v1.0: architecture, behavior, security, testing, packaging, design decisions, open questions and the verification ledger |
| [plans/](plans/README.md) | Slice plans, session evidence and the Server 2020 audit fixes |
| [design.md](design.md) | Design rationale for the first version of the developer tooling in `tools/` |
| [release-0.2.0.md](release-0.2.0.md) | 0.2.0 release audit: findings, Server 2020 REST contracts and the rules for work-PC live checks |
| [release-0.3.0.md](release-0.3.0.md) | 0.3.0 release notes: failed-test report changes and the portable release |
| [release-0.4.0.md](release-0.4.0.md) | 0.4.0 release notes: bugs of failed tests, security fixes and V-30 |
| [release-0.5.0.md](release-0.5.0.md) | 0.5.0 release notes: profile defaults for builds and test plans, report grouping by named test run |
| [release-0.6.0.md](release-0.6.0.md) | 0.6.0 release notes: Test Case report redesign, `-IncludeDetail`, the repository review, V-31 and V-32 |
| [release-0.6.5.md](release-0.6.5.md) | 0.6.5 release notes: the changelog, the pull request check, workflow lint and the release handoff; no product change |
| [release-0.7.0.md](release-0.7.0.md) | 0.7.0 release notes: concurrent requests, the Open bugs view, attachments of every test run, the failed-test report rework and V-33 |
| [release-0.7.5.md](release-0.7.5.md) | 0.7.5 release notes: audit fixes, French spacing, help and guide corrections and tooling hardening |

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
| `V-34` to `V-36` | Server 2020 behavior that `tests/Live/Probes.Live.ps1` probes for the next version: result lists with details, compressed responses and the attachments of sub-results, under [Known limitations](../release-0.8.0.md#known-limitations) in the 0.8.0 notes |
| `S0-n` to `S5-n` | An [acceptance criterion](ado-toolkit-spec.md#22-delivery-slices-and-acceptance-criteria), §22; also a test tag |
| `F01` to `F16` | A finding in [plans/server-2020-audit-fixes.md](plans/server-2020-audit-fixes.md) |
