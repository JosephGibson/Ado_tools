# AdoToolkit 0.7.10 release notes

Prepared on 2026-10-02 from the uncommitted changes on the branch `0.7.10`, which starts at
`main` after the pull request of 0.7.5 was squash-merged as `23e5f4a`; the files there are
those of the tag `v0.7.5`. 0.7.10 changes how a pull request is merged and records it in the
release procedure. It changes no product code: nothing under `src/` or `tests/` differs from
0.7.5, and the module differs by its version number only. This is an offline review. No
Azure DevOps Server connection or `tests/Live/*.Live.ps1` run was attempted, so nothing
below is confirmed live. The areas the README lists as confirmed are still connections,
projects, builds and test runs.

## What changed

0.7.10 adds no cmdlet, no parameter and no configuration key. The configuration file format,
the JSON report schema, the help in both cultures and the seven-file package layout are
unchanged. The [0.7.5 notes](release-0.7.5.md) still describe the module.

### Releases

| Area | 0.7.10 behavior |
| --- | --- |
| Merge of a pull request | A pull request that passes the check is merged with Squash and merge, under the commit title that GitHub proposes: the title of the pull request, then its number, as in `AdoToolkit 0.7.5: … (#3)`. `docs/tooling.md` says so under Pull request check |
| Release procedure | In the `release` skill, the commit message leaves out the ` (#<n>)` that ends a subject on `main`, because the squash merge appends it. The developer steps name the squash merge and the title to keep, and a last step starts the next branch from the updated `main` |
| Tag | Unchanged: the first command of the handoff tags the commit of the branch and pushes both, which publishes at once. After a squash merge the tag stays on that commit, which `main` does not contain; see Known limitations |
| Installer | The examples of `tools/package/Install-AdoToolkit.ps1` name the 0.7.10 zip. The script is otherwise that of 0.7.5 |

### Documentation

| Area | Change |
| --- | --- |
| README | The version, the paragraph on this version and the link to these notes |
| Guides | `docs/guides/getting-started.md` names the 0.7.10 files |
| Release notes | The 0.7.0 notes moved to `docs/archive/`, unedited. The index of the archive lists them and resolves `V-33` there; the 0.7.5 notes and the changelog link to the new place |

### Visible changes for 0.7.5 users

- The module is the 0.7.5 module with another version number. Existing configuration files
  need no change, and no script that worked with 0.7.5 needs one.
- Public contract: unchanged. No cmdlet, parameter, output type, configuration key or
  schema differs.
- The file names of the assets carry 0.7.10. Installation is as before, with the installer
  that ships with the zip.
- For developers: merge a pull request with Squash and merge, keep the commit title that
  GitHub proposes, and start the next branch from `main`.

## Bug fixes

None. 0.7.10 corrects no defect of the module or of the tooling.

### Existing tests changed

None. No test, fixture or golden was added, changed or removed.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| Nothing enforces the merge method | It is a setting of the repository on GitHub, which this release does not change: merge commits and rebase merges stay possible until the developer turns them off there |
| The body of the squashed commit is not prescribed | GitHub proposes it. For a pull request with several commits it is the list of their subjects, as for 0.7.5; for one commit it is the body of that commit, which the release commit does not have. The repository setting that defaults the message to the pull request title leaves it empty in both cases |
| The release is published before the pull request is reviewed | As since 0.6.5, accepted by the developer |
| The findings of 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.7.5](release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| The tag after a squash merge | `v0.7.10` names the commit of the branch, not the squashed commit on `main`. Their files are the same when nothing else reached `main` in between, as `git diff v0.7.5 23e5f4a` shows for 0.7.5 by printing nothing. A command that looks for tags in the history of `main`, such as `git describe`, does not find the tag |
| Limits carried over | The [0.7.5](release-0.7.5.md#known-limitations), [0.7.0](archive/release-0.7.0.md#known-limitations), [0.6.5](archive/release-0.6.5.md#known-limitations) and [0.6.0](archive/release-0.6.0.md#known-limitations) known limitations still apply, with V-33, V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.7.10 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. The module code is that of 0.7.5, so every check of 0.7.5 is
still pending and decides the same assumptions.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.7.10, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.7.10 with seven files in its folder |
| 2 | Checks 2 to 7 of the [0.7.5 notes](release-0.7.5.md#work-pc-live-checks), with the 0.7.10 package | As listed there: V-33, the bug link and **Expand all** in the browser, the two Live scripts, completion, a request with the PC off the network, and the 0.6.0 checks with V-31, V-32, V-02, V-01 and V-03. All still pending |

## Local validation and developer handoff

Run on 2026-10-02 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0. All seven stages pass without a warning: 61 PowerShell files linted, 276 tooling Pester tests, 147 configuration files, 82 Markdown files with 226 links and 203 path references, the hook and skill layout, 2 workflow files linted with actionlint, and the product check |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1`. The previous state of the variable was restored afterwards |
| Core tests in the final gate | 1392 passed under en-US and 1392 under fr-CA, with no failures or skips. None is new: no product code changed |
| Product Pester tests against the staged module | 160 passed, none skipped. None is new |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.7.10` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.7.10` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.7.10-win-x64.zip -ModuleVersion 0.7.10 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.7.10, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.7.10/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt` and `bundle.json` (module 0.7.10, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the pin), 669 files in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |

No browser opened a report, and no Live script ran. Not run: the installer against the
0.7.10 ZIP, the workflows, which only GitHub runs, and the squash merge itself, which
happens on GitHub after these notes.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.7.10-win-x64.zip` | 110356597 | `28e4840218957ad7c3100e1e63bfcb91a375e8c36f52257fa89def84b8e8090c` |
| `AdoToolkit-0.7.10.zip` | 749696 | `46df2982d611f50ee54099e8d4700885e3079b2ab38ccaec0ad7f7265ec61f1b` |
| `Install-AdoToolkit.ps1` | 13035 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag or GitHub release exists for 0.7.10. Every
change is uncommitted on the branch `0.7.10`, which has no upstream yet. The developer owns
publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.7.10`
   and pushes the branch and the tag together, which publishes the release at once.
2. Run the second command and create the pull request on the page it opens, keeping its
   title.
3. When the Verify check passes, merge with Squash and merge and keep the commit title that
   GitHub proposes: the title of the pull request, then its number.
4. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. If the run does not publish, `docs/tooling.md`
   lists the recovery for each cause.
5. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
6. Start the next branch from the updated `main`.
