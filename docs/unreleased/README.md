# Change fragments

Each change that the next release must describe leaves one JSON file here, written when the
change is made: by the `fix-bug` skill for a fix, by the fix mode of the `repo-review` workflow
for each finding it fixes, and by feature or tooling work for each change a user or the release
notes should hear about. `tools/package/Start-AdoToolkitRelease.ps1` assembles them into the
`CHANGELOG.md` section and the notes of the release, and
`tools/package/Complete-AdoToolkitRelease.ps1` deletes them once the release is finished. One
file per change keeps parallel worktree sessions from editing the same file.

## Name

`<yyyyMMdd-HHmmss>-<slug>.json`, in lower case, for example `20261005-143000-retry-body-read.json`.
The time orders the fragments: the changelog entries of a group, and the `R-1`, `R-2`, … rows of
Bug fixes, follow the file names.

## Format

```json
{
  "kind": "fixed",
  "changelog": "One line that says what a user sees.",
  "visible": "Optional: the observable change, for Visible changes.",
  "contract": "Optional: a public contract change, for Public contract changes.",
  "finding": "What was wrong, its cause and the fix.",
  "regressionTest": "RetryPolicyTests.BodyReadErrorsAreRetriedOnlyWhenTheConnectionWasLost",
  "files": ["src/AdoToolkit.Core/Http/RetryPolicy.cs"],
  "testsChanged": "Optional: an existing test that was edited, and why.",
  "preFix": {
    "failure": "Assert.Throws() Failure: No exception was thrown",
    "totals": "Failed 1, passed 2, total 3",
    "condition": "Optional: when it fails, such as under a console code page"
  }
}
```

| Property | Rule |
| --- | --- |
| `kind` | `added`, `changed`, `deprecated`, `removed`, `fixed` or `security`: the group of the changelog entry. `internal`: a change of the tools or the procedures, listed in the notes only |
| `changelog` | Required. One line. For `internal`, the line goes to Internal changes in the notes |
| `visible`, `contract` | Optional. One line each, for those two sections of the notes |
| `finding` | Required for `fixed`. A fragment with a finding is a row of Bug fixes, and names its `files` |
| `regressionTest` | The test that fails without the fix. Required when `preFix` records a failure |
| `files` | The repository paths of the change, with forward slashes; optional without a finding, never empty |
| `testsChanged` | Optional. One line, for Existing tests changed |
| `preFix` | The failure the regression test showed before the fix, with the run's totals. A finding that names a file outside `docs/` that is not Markdown needs it, or `{ "none": "<reason>" }` when no test can show the defect, or `{ "notReproduced": "<reason>" }` when the failure was not observed. The notes then say so: evidence that was not observed is never claimed |

Every text is one line and plain Markdown. An unknown property, a missing required one or a
`files` path that leaves the repository makes the preparation refuse the fragment, and
`tools/tests/Release.Tests.ps1` checks every fragment here in `verify`.

## Lifecycle

1. A change writes its fragment in the same edit as the change.
2. The release preparation journals the fragments it assembles, with their SHA-256, in
   `artifacts/release/release-prep.json`. All three release scripts refuse a fragment added or
   changed after that: keep it out of this folder until the release is finished, and it goes
   into the next one.
3. Finishing the release deletes the journaled fragments, so the release commit holds none.
