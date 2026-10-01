# Documentation

| Path | Reader | Rule |
| --- | --- | --- |
| `README.md`, `docs/guides/` | End users | Keep the explanation. Fix facts, links and examples; do not trim for agents |
| `docs/commands/` | End users, as `Get-Help` | Shipped content; see `docs/commands/AGENTS.md` |
| `docs/release-<version>.md` | End users and the developer | Only the current and the previous version; the `release` skill writes them |
| `docs/schemas/testcase.v1.schema.json` | Consumers of the JSON report | Public contract; `JsonSchemaValidationTests` validates reports against it |
| `docs/tooling.md` | Developers and agents | Reference for `tools/`: terse, tables, exact commands |
| `docs/archive/` | History | Never edit a file. Only `docs/archive/README.md` and `docs/archive/plans/README.md` change |

## Rules

- A code change that alters behavior updates, in the same change, every guide and help
  topic that describes it.
- Examples use synthetic data: hosts ending in `.test`, invented names and IDs.
- Every relative link, heading anchor and repository path must resolve; the `documentation`
  stage of `verify` checks them in every Markdown file except archived files and test
  fixtures. Release notes may name a path that has since gone.
- State a fact once and link to it. The guides link to the help topics under
  `docs/commands/en-US/`.
- Server behavior that is not confirmed at work is marked with its `V-nn` item.

## Archiving

1. Move the file into `docs/archive/` with a filesystem move. Do not edit it.
2. Add a row to `docs/archive/README.md`.
3. Repoint every link and path that named the old location.
4. Keep every citation form used in code resolvable through `docs/archive/README.md`.
