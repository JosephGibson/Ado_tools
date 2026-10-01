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
| `V-31`, `V-32` | The requests of `Export-AdoTestCase -IncludeDetail`, under [Known limitations](../release-0.6.0.md#known-limitations) in the 0.6.0 notes |
| `S0-n` to `S5-n` | An [acceptance criterion](ado-toolkit-spec.md#22-delivery-slices-and-acceptance-criteria), §22; also a test tag |
| `F01` to `F16` | A finding in [plans/server-2020-audit-fixes.md](plans/server-2020-audit-fixes.md) |
