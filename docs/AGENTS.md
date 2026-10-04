# Documentation

| Path | Reader | Rule |
| --- | --- | --- |
| `README.md`, `docs/guides/` | End users | Keep the explanation. Fix facts, links and examples; do not trim for agents |
| `docs/commands/` | End users, as `Get-Help` | Shipped content; see `docs/commands/AGENTS.md` |
| `docs/release-<version>.md` | End users and the developer | Only the current and the previous version; the `release` skill writes them |
| `CHANGELOG.md` | End users, also as the opening of each GitHub release | One short section per version that links its release notes. The `release` skill writes it, and `verify` requires it for the current version |
| `docs/schemas/testcase.v1.schema.json` | Consumers of the JSON report | Public contract; `JsonSchemaValidationTests` validates reports against it |
| `docs/tooling.md` | Developers and agents | Reference for `tools/` and the agent setup: terse, tables, exact commands |
| `docs/plans/` | The developer and agents | Each phase of a plan lists the files it touches and the phases it depends on, so phases with disjoint files can run as parallel worktree sessions |
| `docs/archive/` | History | Never edit a file. Only `docs/archive/README.md` and `docs/archive/plans/README.md` change |

## Rules

- A code change that alters behavior updates, in the same change, every guide and help
  topic that describes it.
- Examples use synthetic data: hosts ending in `.test`, invented names and IDs.
- The changelog and release notes may name a path that has since gone; their links must
  still resolve.
- Guides link to help topics under `docs/commands/en-US/`.
- Server behavior that is not confirmed at work is marked with its `V-nn` item.
- An error is named by its error ID, such as `AdoConnectionMismatch`, or by its category,
  such as `ObjectNotFound`.

## Archiving

1. Move the file into `docs/archive/` with a filesystem move.
2. Add a row to `docs/archive/README.md`.
3. Repoint every link and path that named the old location.
4. Keep every citation form used in code resolvable through `docs/archive/README.md`.
