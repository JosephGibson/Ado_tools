---
name: release
description: Prepare an AdoToolkit version bump, release notes, changelog and validated local packages, then give the developer two commands that publish the release and open its pull request. Use only for a requested release.
---

# Prepare a release

`<new>` is the version to release. Run no Git writes and publish nothing: the developer runs
the handoff commands, which exist only once the release check has passed. The developer is the
author: add no agent or tool attribution or co-author trailer to commits, tags, pull requests,
notes, changelog or release text.

Three scripts in `tools/package/` do the mechanical work, each printing one JSON document; see
Release scripts in `docs/tooling.md`. Never bump a version, move notes or write the Validation
section by hand.

1. **Fragments.** Run `pwsh -NoProfile -File .\tools\package\Start-AdoToolkitRelease.ps1 -Version <new> -WhatIf`
   and compare its `Fragments` with `git diff --stat <Base>` and `git status --short`. Write
   the fragment of a change that has none now, in the format of `docs/unreleased/README.md`: a
   fix whose failure was not recorded when it was made gets
   `{ "notReproduced": "the failure was not recorded when the fix was made" }`. Once the
   preparation has run, every script refuses a new fragment.
2. **Prepare.** Run the same command without `-WhatIf`. On exit `1`, report its `Error` and
   stop; after a fix, run it again: it resumes. It bumps `VersionPrefix` and the documents that
   repeat it, archives the older notes, writes the `CHANGELOG.md` section and
   `docs/release-<new>.md` from the fragments, and lists the `release:todo` markers it left.
   Report its `Warnings`, such as a GitHub release that `gh` could not look up.
3. **Check.** At once, start `pwsh -NoProfile -File .\tools\package\Test-AdoToolkitRelease.ps1`
   in the background. From then until step 7 has run, edit nothing but `CHANGELOG.md`,
   `docs/release-<new>.md` and `docs/archive/README.md`: the check hashes the rest of the tree
   before its gate, and the finish compares the tree with that hash. Any other edit means
   running the check again.
4. **Orientation.** Read the JSON of step 2, the diff of the changed product files, and the
   "For the release notes" section of a plan for `<new>`, if there is one. Review each
   `Leftovers` line in context; history keeps old versions on purpose. Do not read earlier
   notes for their structure: the template sets it.
5. **Prose.** Replace every `release:todo` marker:
   - in `CHANGELOG.md`, a bullet no fragment supplied, under its Keep a Changelog group. Tests,
     tooling and internal details stay in the notes; with no user-visible change, one entry says
     so;
   - in the notes, the summary line, the opening sentences, What changed, the new rows of
     Findings not fixed and Known limitations, or the marker row deleted, and the Work-PC Live
     checks. Complete Visible changes and Public contract changes from the diff;
   - in `docs/archive/README.md`, the one-line summary of notes written before the template.
6. **Wait** for the check. Exit `0` passes. Exit `2` passes only when the `assets` step alone
   is incomplete, because `artifacts/runtime-download/` lacks the pinned runtime and the
   portable asset was not built: fetch nothing without authorization. Otherwise report the
   failed or incomplete step, its detail and the command, and stop. A test that fails once and
   passes when the check runs again, and whose subject nothing in the release touches, is a
   flake: record the command, the failure and the passing repeat in the opening of the notes,
   add a row to Findings not fixed saying why the release does not cause it, and go on. A
   failure that repeats stops the release.
7. **Finish.** Run `pwsh -NoProfile -File .\tools\package\Complete-AdoToolkitRelease.ps1 -Summary '<summary>'`.
   The summary follows `git log --oneline`: one line, no quote, and no ` (#<n>)`, which the
   squash merge appends. The script refuses while a marker remains, and when the tree changed
   after the check started; then run the check again. It writes `artifacts/release/handoff.md`.
8. **Handoff.** Relay `handoff.md` as written, each command in its own code block, with the gate
   result, the assets, the public contract changes and the unfixed findings. Above the commands,
   list the `WorkingTree` paths that do not belong to the release: command 1 stages everything.
   Say that command 1 publishes the release the moment it pushes the tag, before any review,
   and that from 0.11.0 on, users who run `Update-AdoToolkit` receive it at once.

## Versions and notes

- `<old>` and `<older>` are the current and the previous version. `VersionPrefix` in
  `Directory.Build.props` is the one declaration of the version.
- Only the notes of the current and the previous version stay under `docs/`; older notes move
  to `docs/archive/` by the Archiving procedure of `docs/AGENTS.md`.
- The notes say only what was observed: a fix whose failure was not reproduced says so.

## gh and tokens

The check records whether `gh` is present. Command 1 needs no `gh`: it falls back to the
compare page by itself when `gh` is missing or `gh pr create` is refused. Without `gh` the
handoff has no command 2, and the developer watches the Verify check on the pull request page;
a `gh` installed after the check counts only once the check runs again. winget puts a tool on
the `PATH` of new terminals only, so `missing` just after an install means "open a new
terminal". Creating a pull request needs a token allowed to write one: a fine-grained PAT
without pull-request write reads the checks and still refuses `createPullRequest`, and
`gh pr create --dry-run` exits `0` without exercising that permission. When the create was
refused, the release is already published, so `gh pr create` alone is enough once the token
may write a pull request.
