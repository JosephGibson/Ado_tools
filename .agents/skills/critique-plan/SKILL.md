---
name: critique-plan
description: Final step of planning. Send a plan to the critique skill, triage its findings against this repository, and record them in the plan. Use before presenting a plan or saving one under docs/plans.
---

# Critique a plan

The critic is the developer's personal `critique` skill: an OpenAI model through the Codex
CLI, so the second opinion comes from another model family. Never write the critique
yourself, not even when the command fails, and never fabricate a finding or a verdict.

1. Choose the artifact. A saved plan is the file under `docs/plans/`; a plan held in the
   session, including a plan mode plan, goes in through a quoted heredoc. The critic reads
   one plan at a time. Over 50,000 characters it stops with exit `2`: ask the developer to
   confirm the size before `--force`, or critique one phase at a time.
2. Run it from the repository root, in the background, writing the raw critique to the
   session scratchpad and never into the repository:

   ```bash
   bash ~/.claude/skills/critique/critique --raw --repo --out "$OUT" docs/plans/<name>.md
   ```

   `--repo` lets the critic read this tree, read-only, which is what grounds a finding in
   `path:line`; it resolves the root with `git rev-parse`, which the Git boundary allows.
   Drop it only for a plan that names no repository file, and keep the default effort
   unless the developer asks for a faster pass.
3. Triage every finding before changing the plan. A recorded decision (`§n`, `DD-0nn`,
   `Q-nn`, `V-nn`, `S0-n`, `Fnn`, which `docs/archive/README.md` resolves), a pinned golden
   under `tests/Fixtures/Reports/` or the public contract names what a finding would
   change; none of them refutes it, because the contract changes when the change is the fix
   and a golden records bytes, not correctness. Reject a finding only on evidence that the
   behavior it questions is still intended, and cite that evidence. Otherwise take the
   finding on and name the deliberate change: the decision to revisit, the goldens to
   regenerate through `update-goldens`, or the contract change with its release note. A
   finding about unconfirmed server behavior is owed to a `V-nn` check in `tests/Live/`,
   which only the developer runs; the critic cannot settle it.
4. Record the verdicts in the plan, in a `## Critique` section: the critic and the date,
   then one line per finding with its tag and one of `applied` and the change made,
   `rejected` and the evidence, or `owed` and the check that would settle it. A plan mode
   plan carries the section in the text that ExitPlanMode presents, and
   `tools/guard-plan.ps1` blocks one that carries none.
5. Report the findings and verdicts to the developer, and end the reply with the footer
   line of the run, which names the saved raw critique.
6. When the critic cannot run, record `Critique: skipped` and the reason, and say the pass
   is owed. The reason is a missing `codex`, a usage limit, or a Codex session, where the
   critic would be the same model family as the author of the plan.

The critic only surfaces concerns: applying one is this session's work. Critique a plan
again only after it changes in a way the recorded findings do not cover.
