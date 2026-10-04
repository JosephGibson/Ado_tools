# Plans

Plans of work in progress. Each phase of a plan lists the files it touches and the phases it
depends on, so phases with disjoint files can run as parallel worktree sessions; see
`docs/tooling.md`, under Worktrees.

A plan carries a `## Critique` section: each finding of its critique pass with the verdict
that closed it, applied, rejected or owed. The `critique-plan` skill runs the pass and
writes the section, and a plan presented without one is unfinished.

A finished plan moves to `docs/archive/plans/` through the Archiving procedure of
`docs/AGENTS.md`, and the [index of archived plans](../archive/plans/README.md) lists it.
