# Archive

These are the planning and design records from AdoToolkit's first development phase.
They are kept unchanged for reference and are no longer maintained. For current
documentation, start with the [README](../../README.md).

| Document | Contents |
| --- | --- |
| [ado-toolkit-spec.md](ado-toolkit-spec.md) | Technical specification v1.0: architecture, behavior, security, testing, packaging, design decisions, open questions and the verification ledger |
| [plans/](plans/README.md) | Delivery roadmap, implementation plans and offline evidence records |
| [design.md](design.md) | Design rationale for the developer tooling in `tools/` |

## Citations in the code

Comments in `src/`, `tests/` and `tools/` cite the specification. All of these
identifiers refer to [ado-toolkit-spec.md](ado-toolkit-spec.md):

| Citation | Meaning |
| --- | --- |
| `§n`, `§n.m` | A numbered section |
| `DD-0nn` | A [design decision](ado-toolkit-spec.md#23-design-decisions) |
| `Q-nn` | An [open question](ado-toolkit-spec.md#24-open-questions-for-the-next-pass) and its answer |
| `V-nn` | A [verification ledger](ado-toolkit-spec.md#25-verification-ledger) entry; cmdlet help notes use these too |
| `S0-n` to `S5-n` | An [acceptance criterion](ado-toolkit-spec.md#22-delivery-slices-and-acceptance-criteria), also used as test tags |

The archived files were moved here without edits. Some relative links inside them
point outside `docs/archive/` and no longer resolve.
