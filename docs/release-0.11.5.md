# AdoToolkit 0.11.5 release notes

> change fragments written with each change, three scripts that prepare, check and finish a release, the slim release skill and an isolated lint; no product change

Prepared on 2026-10-05 from the branch `0.11.5`, which starts at `3e7f24f`, the commit of AdoToolkit 0.11.0 on `main`. 0.11.5 changes only the tools and the procedures that develop and release AdoToolkit, as the [0.11.5 plan](plans/0.11.5-release-speedup.md) describes; nothing in it needs the work server, and the live checks of 0.11.0 are still pending. It is the first release prepared with the new scripts: under 4 minutes from the request to the finishing script, for a target of 9.

## What changed

### Releases

A release is three scripts in `tools/package/` and the prose they leave to the agent, as
[Release scripts](tooling.md#release-scripts) describes. The `release` skill names the
scripts and keeps none of their steps.

| Step | Up to 0.11.0 | From 0.11.5 |
| --- | --- | --- |
| Release text | Written at release time from the diff | Written with each change as a fragment in `docs/unreleased/`; `Start-AdoToolkitRelease.ps1` assembles the `CHANGELOG.md` section and the notes |
| Version bump, archive of the older notes | By hand, following the skill | `Start-AdoToolkitRelease.ps1`, which resumes after a failure |
| Gate | The whole of `verify` four to seven times | Once, split between `Test-AdoToolkitRelease.ps1` and `Complete-AdoToolkitRelease.ps1`, with a hash of the tree proving that only the changelog, the notes and the archive index changed in between |
| Evidence that a fix fixes | Released source restored to run the regression test again | The failure the regression test showed before the fix, recorded in its fragment |
| Validation section | Written by hand, with link counts, durations, per-file tables and local checksums | Written by `Complete-AdoToolkitRelease.ps1` from the check's results |
| Handoff commands | Filled in by hand from the skill | Written to `artifacts/release/handoff.md` |

### Fixes

The `fix-bug` skill writes the fragment of a fix, with the failure its regression test showed
before the fix or the reason there is none. The fix mode of the `repo-review` workflow asks each
fixer for that fragment and writes it in its gate step.

### Lint

`powershell-lint` runs PSScriptAnalyzer in a pwsh process of its own, without the Windows
PowerShell module folders or any other folder on the module path of `verify`. Inside a full
`verify` held to four processors on the development PC, the stage took 22.3 s where it took
33.6 s, as the [Lint timings](plans/0.11.5-release-speedup.md#lint-timings) of the plan record.

### README

The README gives the current version only, and `verify` fails when its version line does not
name `VersionPrefix`.

### Internal changes

- `README.md` carries the current version and no history of earlier versions, and `tools/tests/Release.Tests.ps1` requires its one version line to name `VersionPrefix` and no link to release notes, so a bump that misses the README fails `verify`.
- Change fragments in `docs/unreleased/` carry the release text of each change, written with the change, and three scripts in `tools/package/` prepare, check and finish a release: the version bump, the archive of the older notes, the changelog section and the notes assembled from the fragments, the gate and the packaging under the release pins, and the handoff commands.
- A fix writes its change fragment when it is made: the `fix-bug` skill records the failure its regression test showed before the fix, and the fix mode of the `repo-review` workflow requires a fragment, with that failure or the reason there is none, from each fixer and writes it in its gate step. The release no longer restores released source to prove a fix.
- The `release` skill shrinks to the three release scripts and the prose they leave to the agent: one gate, split between the check and the finishing script, where each release used to run four to seven, and notes without link counts, durations, per-file tables or local checksums.
- The `powershell-lint` stage runs PSScriptAnalyzer in a pwsh process of its own whose module path holds only PowerShell's own modules, to which PowerShell adds back the user's and the shared PowerShell 7 folders: the Windows PowerShell folders and every other folder on the module path of `verify` can no longer slow the analysis or shadow a command, and the findings come back whole through a file.

### Visible changes for 0.11.0 users

- The README gives the current version only; the changelog says what each version changed and links its release notes.

### Public contract changes

None.

## Bug fixes

None: no change fragment of this version records a fix.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| A release is public before its review | The first handoff command publishes the release the moment it pushes the tag, before the pull request is reviewed, and from 0.11.0 on `Update-AdoToolkit` installs it for every user who runs it. Changing that changes the publication model, which is left to a later version: [Considered and not taken](plans/0.11.5-release-speedup.md#considered-and-not-taken) in the plan lists draft-then-publish and releasing from `main` |
| `powershell-lint` on the GitHub runner | Not yet measured: it took 76 to 144 s there before this version, for a target under 30 s. The pull request run of 0.11.5 measures it; if it is still above 30 s, step 3 of [D-6](plans/0.11.5-release-speedup.md#d-6-a-faster-ci-lint) in the plan, running lint beside the tooling tests, is owed |
| The findings of 0.11.0, 0.10.5, 0.10.0, 0.9.15, 0.9.10, 0.9.5, 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.11.0](release-0.11.0.md#findings-not-fixed), [0.10.5](archive/release-0.10.5.md#findings-not-fixed), [0.10.0](archive/release-0.10.0.md#findings-not-fixed), [0.9.15](archive/release-0.9.15.md#findings-not-fixed), [0.9.10](archive/release-0.9.10.md#findings-not-fixed), [0.9.5](archive/release-0.9.5.md#findings-not-fixed), [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Lint isolation stops at PowerShell's own folders | PSScriptAnalyzer resolves commands in runspaces of its own, and PowerShell 7 puts the user's and the shared module folders first in each of them, whatever the module path says. A module there can still shadow a command of a build module for the analysis |
| A fragment written during a release | Once the preparation has journaled the fragments, every release script refuses a new or changed one: it waits outside `docs/unreleased/` until the release is finished, and goes into the next one |
| Limits carried over | The [0.11.0](release-0.11.0.md#known-limitations), [0.10.5](archive/release-0.10.5.md#known-limitations), [0.10.0](archive/release-0.10.0.md#known-limitations), [0.9.15](archive/release-0.9.15.md#known-limitations), [0.9.10](archive/release-0.9.10.md#known-limitations), [0.9.5](archive/release-0.9.5.md#known-limitations), [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply |

## Work-PC Live checks

0.11.5 owes no new `V-nn` check: its module is that of 0.11.0. It is the first release that a
0.11.0 installation can update to, so the work PC can now see a real update. The rules of the
[0.11.0 notes](release-0.11.0.md#work-pc-live-checks) apply.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | In a new window with 0.11.0 installed by hand, `Update-AdoToolkit -Verbose`, then the same in the portable 0.11.0 console | V-38 in use: `Status` `Installed`, `LatestVersion` 0.11.5 and `Path` naming the new folder, a `0.11.5` version folder beside the 0.11.0 one for the module and a new folder next to the old one for the portable copy. In a new window, or from the new launcher, the loaded module is 0.11.5. An error is a finding to report by its error ID |
| 2 | `tests/Live/Update.Live.ps1`, once 0.11.5 is the newest version installed | V-38: `PASS V-38`, as check 1 of the 0.11.0 notes describes |
| 3 | The other checks of the 0.11.0 notes, with 0.11.5 | All still pending |

## Validation

<!-- release-check:begin -->

| Check | Result |
| --- | --- |
| `verify`, every stage but `documentation`, with `ADOTOOLKIT_RELEASE_BUILD=1` | Passed: `powershell-lint`, `powershell-test`, `configuration`, `tooling-layout`, `workflow-lint` and `project-check`, with Microsoft.PowerShell.PlatyPS 1.0.3, Pester 5.9.1 and PSScriptAnalyzer 1.25.0 |
| Tests that passed | tooling Pester 421, Core tests (en-US) 1808, Core tests (fr-CA) 1662 and product Pester 233 |
| The `documentation` stage and the changelog and README tests | Passed on these notes, before the handoff |
| Version and package | MSBuild gives 0.11.5; `AdoToolkit/0.11.5` packaged from the verified build |
| Release assets | `AdoToolkit-0.11.5-win-x64.zip` 110,483,761 bytes, `AdoToolkit-0.11.5-win-x64.zip.sha256` 96 bytes, `AdoToolkit-0.11.5.zip` 876,745 bytes, `AdoToolkit-0.11.5.zip.sha256` 88 bytes and `Install-AdoToolkit.ps1` 13,567 bytes |
| Portable launcher | passed: AdoToolkit 0.11.5, PowerShell 7.6.6, Windows x64 |
| `Install-AdoToolkit.ps1 -WhatIf` | Accepted the checksum and the layout of `AdoToolkit-0.11.5.zip` |
| Content | Every file the gate checked is unchanged, apart from the changelog, these notes and the archive index |

CI builds its own assets: compare the published files with the `.sha256` files of the release.

<!-- release-check:end -->