# AdoToolkit 0.6.5 release notes

Prepared on 2026-10-01 from the uncommitted changes on the branch `0.6.5`, which starts at
the 0.6.0 release (tag `v0.6.0`). 0.6.5 changes how a release is prepared, checked and
published. It changes no product code: nothing under `src/` or `tests/` differs from
0.6.0, and the module differs by its version number only. This is an offline review. No
Azure DevOps Server connection or `tests/Live/*.Live.ps1` run was attempted, so nothing
below is confirmed live. The areas the README lists as confirmed are still connections,
projects, builds and test runs.

Neither workflow, the setup action nor the Dependabot configuration has run on GitHub.
This release is their first run.

## What changed

0.6.5 adds no cmdlet and no parameter. The configuration file format, the JSON report
schema, the help in both cultures and the seven-file package layout are unchanged.

### Releases

| Area | 0.6.5 behavior |
| --- | --- |
| Changelog | `CHANGELOG.md` is new, at the repository root, in the style of Keep a Changelog: one short section per version that says what changes for a user and links the notes. It starts with this release and points to the earlier notes. The README links it |
| GitHub release text | Opens with `## Changes` and the `CHANGELOG.md` section of the version, above the installation instructions. A relative link in the section becomes a link to that file at the tag. A version whose section is missing or empty is not published |
| Release procedure | The `release` skill releases the branch that is checked out, writes the changelog section after the notes, and ends with two commands for the developer. The first stages and commits every change, creates the annotated tag and pushes the branch and the tag together. The second prints and opens GitHub's compare page against `main`, with the title and body of the pull request filled in. It gives no commands when validation fails, the head is detached, the branch is `main` or the tag exists. Nothing it writes names an agent or a tool as author |
| Manual run | `.github/workflows/release.yml` can be started by hand with an existing tag as input, for a tag whose run did not start or failed for a reason outside the tagged files. `docs/tooling.md` lists the recovery for each cause of a run that does not publish |
| Pull request check | `.github/workflows/verify.yml` is new: it runs `verify` on a pull request to `main`, with a token that can only read |
| Setup action | `.github/actions/setup/action.yml` is new: a composite action that installs the .NET SDK, PowerShell 7.6.6, actionlint 1.7.12 and the exact build modules. Each download is checked against a pinned SHA-256. Both workflows use it, so each pin is declared once |
| Action updates | `.github/dependabot.yml` is new: a weekly check of the actions pinned in the workflows and in the setup action |

### Developer tooling and instructions

| Area | Change |
| --- | --- |
| `verify` | Seventh stage, `workflow-lint`: actionlint checks the YAML, the schema and the expressions of every workflow file. Without actionlint the stage is unavailable and `verify` exits `2` |
| Prerequisites | actionlint is required. `diagnose` and `deps` list it, and `bootstrap -Install` installs it with winget |
| Changelog gate | A tooling test requires, in `CHANGELOG.md`, the section of `VersionPrefix` with a dated heading, at least one entry and the link to the notes. It runs the publish step of the workflow on the file, so both read the section the same way |
| `documentation` stage | `CHANGELOG.md` is checked like the notes of a release: its links must resolve, its paths are not checked |
| Instructions | `docs/tooling.md` describes the stage, the prerequisite, the two commands, the setup action, the pull request check and the recovery from a run that does not publish. `tools/package/AGENTS.md` and `docs/AGENTS.md` carry the new rules, and the packaging and documentation rules of Claude also load for `.github/` and `CHANGELOG.md` |
| Release notes | The 0.5.0 notes moved to `docs/archive/` |

### Visible changes for 0.6.0 users

- The module is the 0.6.0 module with another version number. Existing configuration files
  need no change, and no script that worked with 0.6.0 needs one.
- Public contract: unchanged. No cmdlet, parameter, output type, configuration key or
  schema differs.
- The page of a GitHub release starts with the changes of that version.
- The file names of the assets carry 0.6.5. Installation is as before, with the installer
  that ships with the zip.
- For developers: `verify` has a seventh stage and needs actionlint. Until it is
  installed, `verify` exits `2`. A terminal that was open during the installation may not
  find it.
- For developers: a new `VersionPrefix` without its section in `CHANGELOG.md` fails
  `verify`.
- For developers: a pull request to `main` runs `verify` on GitHub.

## Bug fixes

None. 0.6.5 corrects no defect of the module or of the tooling.

The new behavior was tested before it existed. The test of the changelog section in the
release text, and the three cases of a missing section, were run against the 0.6.0
workflow and failed, then passed against the changed one. The changelog gate was run on
this tree after the version was raised and before the section was written, and failed
with `CHANGELOG.md needs one heading '## 0.6.5 - <yyyy-MM-dd>'`.

### Structural changes

| Change | Reason |
| --- | --- |
| Setup action | The pull request check needs the toolchain of the release. Two copies of the installation steps and of each pin would drift |
| `RELEASE_TAG` | The steps of the release job read the tag from this variable. In a manual run the ref of the run is a branch |
| `workflow-lint` runs in-process | It reports each finding of actionlint as a failure and a missing actionlint as unavailable, like the lint of the PowerShell files |
| One winget list in `bootstrap` | ripgrep and actionlint install through the same code |

### Existing tests changed

| Test | Change |
| --- | --- |
| `ReleaseWorkflow.Tests.ps1` | `Get-ReleaseStep` also reads a step of the setup action, and the tests of the PowerShell download and of its pin run the step of the action. The tests of the publish step supply a changelog and run as a manual run does, with a branch as the ref and the tag in `RELEASE_TAG`. The test of the action pins covers the workflow and the action |
| `Documentation.Tests.ps1` | "checks links but not paths in a dated document…" also covers `CHANGELOG.md`, and shows that a file of that name in another folder is not exempt |

### Findings not fixed

| Finding | Reason |
| --- | --- |
| The workflows, the setup action and the Dependabot configuration are untested on GitHub | Only a run shows it, and this release is that run. Locally, actionlint accepts both workflows and the tooling tests run their steps with mocked downloads |
| actionlint does not read the setup action or the Dependabot configuration | It lints workflow files. The steps of the action are covered by the tooling tests; the Dependabot file by one test of its directories |
| Dependabot does not watch the PowerShell and actionlint pins | They are downloads, not actions. They are updated by hand, each with its SHA-256 |
| The actionlint version is not pinned locally | winget installs the current version. The workflows install 1.7.12 |
| No rule on `main` requires the pull request check | A repository setting, declined by the developer for now |
| The release is published before the pull request is reviewed | Accepted by the developer. A draft release was declined |
| The findings of 0.6.0 | Unchanged; see its [findings not fixed](archive/release-0.6.0.md#findings-not-fixed) |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Manual run | It takes the workflow file from the branch it is started on and every other file from the tag, so it works for a tag that holds the setup action: `v0.6.5` and later |
| Pull request page | The second command opens the page; the pull request exists once it is created there. The filled-in title and body rely on GitHub's `quick_pull`, `title` and `body` query parameters, and the page was not opened in a browser during this preparation |
| Remote address | The second command converts the `https` and `ssh` forms of a GitHub address. An address with a port is not handled |
| Links in the release text | Only an inline link without a title is pointed at the tagged file |
| Limits carried over | The 0.6.0 [known limitations](archive/release-0.6.0.md#known-limitations) still apply, with V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.6.5 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. The module code is that of 0.6.0, so every check of 0.6.0 is
still pending and decides the same assumptions.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.6.5, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.6.5 with seven files in its folder |
| 2 | Checks 2 to 9 of the [0.6.0 notes](archive/release-0.6.0.md#work-pc-live-checks), with the 0.6.5 package | As listed there: V-31 and V-32, V-02, V-01, V-03, the `-Open` warning, custom fields, Markdown line breaks, and the 0.5.0 checks with V-30. All still pending |

## Local validation and developer handoff

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0. All seven stages pass without a warning: 60 PowerShell files linted, 254 tooling Pester tests, 147 configuration files, 82 Markdown files, the hook and skill layout, 2 workflow files linted with actionlint, and the product check |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1` |
| Core tests in the final gate | 1209 passed under en-US and 1209 under fr-CA, with no failures or skips. None is new: no product code changed |
| Product Pester tests against the staged module | 145, all passed. None is new |
| Tooling Pester tests | 254, all passed, in ten files; 0.6.0 had 221 in eight. `VerifyWorkflow.Tests.ps1` (11) and `WorkflowLint.Tests.ps1` (8) are new, and `ReleaseWorkflow.Tests.ps1` has 14 more: the changelog section in the release text and its gate, the manual run, and the actionlint download |
| actionlint 1.7.12, installed with winget through `bootstrap -Install` | No finding in either workflow. In a copy with a broken indentation, the stage reported the YAML error with its file, line and column |
| The publish step on the real `CHANGELOG.md`, with `gh` replaced, as part of the tooling tests | The release text opens with the 0.6.5 section, and its link names `docs/release-0.6.5.md` at the tag `v0.6.5` |
| The address of the second command, built without Git from six forms of a GitHub remote address | Each gives the `https` address of the repository, without credentials; the title and the body read back unchanged from the query |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.6.5` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.6.5` with seven files |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.6.5-win-x64.zip -ModuleVersion 0.6.5 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.6.5, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.6.5/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt` and `bundle.json` (module 0.6.5, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the pin), 669 files in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |

Not run: the installer against the 0.6.5 ZIP, and the workflows, which only GitHub runs.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.6.5-win-x64.zip` | 110328439 | `9d411cf65cf23a3699c3dc280bf4adf745779686b834a0047606256da80e3662` |
| `AdoToolkit-0.6.5.zip` | 721525 | `9c15816907d69db64951695a9baa8583bde17cfb6e14a8eaf39905800f5c57a4` |
| `Install-AdoToolkit.ps1` | 12880 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes.

No Live script was run, and no tag or GitHub release exists for 0.6.5. Every change is
uncommitted on the branch `0.6.5`, which has no upstream yet. The developer owns the rest
of the release:

1. Run the first command of the handoff. It commits every change, creates the tag
   `v0.6.5` and pushes the branch and the tag together, which publishes the release.
2. Run the second command and create the pull request on the page it opens. Merge it when
   the Verify check passes.
3. Watch the release workflow, which runs the setup action for the first time, and check
   its five uploaded assets, version and checksums. CI builds its own assets, so compare
   each checksum with CI's own `.sha256` files; archive metadata can differ from a local
   build. If the run does not publish, `docs/tooling.md` lists the recovery for each cause.
4. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
