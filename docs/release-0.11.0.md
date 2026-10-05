# AdoToolkit 0.11.0 release notes

Prepared on 2026-10-05 from the branch `0.11.0`, which starts at `main` after the pull
request of 0.10.5 was squash-merged as `0e48355`; the files there are those of 0.10.5. The
reviewed changes are the working tree on top of it: the branch has no commit of its own.
0.11.0 adds `Update-AdoToolkit`, the 23rd cmdlet. It reads the newest release on GitHub,
checks the download against both SHA-256 checksums that GitHub gives for it, and installs it
beside the running copy, which it never changes: a new version folder for a module installed
with `Install-AdoToolkit.ps1`, a new sibling folder for a portable copy. The user then opens a
new PowerShell window. A new cmdlet makes this a minor version. The design, its spikes, the
developer's decisions and the critique are in the
[0.11.0 plan](archive/plans/0.11.0-update-cmdlet.md). This is an offline review. No Azure
DevOps Server connection and no `tests/Live/*.Live.ps1` run was used, and no test reached
GitHub: every update test runs against a handler that takes the place of the network. The
one request to GitHub during the release read the published v0.10.5 release through the API,
to confirm the asset digests that the updater requires (see
[Known limitations](#known-limitations)). Whether the work proxy lets the command reach
GitHub is V-38, owed to the [Work-PC Live checks](#work-pc-live-checks). The areas the README
lists as confirmed are still connections, projects, builds and test runs.

## What changed

0.11.0 adds one cmdlet, `Update-AdoToolkit`, with its output type
`AdoToolkit.Core.Update.AdoToolkitUpdate`, two public enums, four public Core types that the
cmdlet uses, a table view, one progress phase and 50 keys in the string catalog. It adds no
error ID: its failures reuse the existing exception types. It sends no new request to Azure
DevOps; its own requests go to GitHub. No other cmdlet, parameter, report, report golden,
configuration setting, CSV column or schema changed, and the package still holds the same
seven files. The [0.10.5 notes](release-0.10.5.md) describe the last fixes, and the
[0.10.0 notes](archive/release-0.10.0.md) the last feature.

### Update-AdoToolkit

| Area | 0.11.0 behavior |
| --- | --- |
| Parameters | `-WhatIf` and `-Confirm` only. There is no version choice, downgrade, target path, pruning of old versions or `-ExpectedThumbprint` |
| What it updates | Detected from the module's own folder before any request. **Module**: `<root>\AdoToolkit\<version>`, where `<root>` is on `PSModulePath`, compared as `Install-AdoToolkit.ps1` compares it: full paths without a trailing separator, ignoring case. **Portable**: `<folder>\module` beside `Start-AdoToolkit.cmd` and `runtime\pwsh.exe`. Anything else, a source build or a staged copy under `artifacts/` included, is refused with `AdoConfiguration` and the address of the README's installation steps, and sends no request |
| The release | `https://api.github.com/repos/JosephGibson/Ado_tools/releases/latest`, which never names a draft or a prerelease. The JSON may hold 1 MiB; `draft` and `prerelease` must be false. The tag must match `^v(0\|[1-9][0-9]{0,8})\.(0\|[1-9][0-9]{0,8})\.(0\|[1-9][0-9]{0,8})\z`: ASCII digits, no leading zero, no trailing newline. Every folder name and URL is built from the parsed numbers; the API's download URLs and asset IDs are never used |
| Nothing to do | A release that is not newer than the running version is `UpToDate` after that one request: nothing is downloaded or written. In module mode, a valid folder of the new version is `AlreadyInstalled` with no download. In portable mode, an existing target folder is refused with `UpdateTargetExists` before any download |
| Verification | The zip and its `.sha256` asset must both be listed once, each with a `sha256:` digest and a size from 1 byte to its cap (32 MiB for the module zip, 256 MiB for the portable zip), checked before any download. The checksum file must match its own digest. The zip is hashed as it arrives, its length counted against the listed size, and its hash must equal both the API digest and the checksum file before the zip is opened |
| The archive | The installer's rules, in its order: safe entry names, one version folder equal to the tag, the exact seven files, 64 MiB per entry, 64 entries; then the manifest's `ModuleVersion` and `RootModule`. The updater adds an identity check of the three DLLs, read from metadata and never loaded: each carries its file name and `ModuleVersion` plus `.0`. A module that needs a newer PowerShell than the one running is refused with `AdoConfiguration`. A portable zip is checked against the layout that `New-AdoPortableArchive` builds: the top level, the seven module files, the ten runtime files, `bundle.json` of the tag's version and `win-x64`, no link, duplicate or colliding name, at most 5,000 entries and 1 GiB, and the free space before extraction |
| Installation | Module: staged beside the versions in `.update-<32hex>`, then moved to `<root>\AdoToolkit\<version>`. An incomplete folder of that version is moved aside first and restored if the move fails, as the installer does. Portable: staged beside the running folder, then moved to `AdoToolkit-<version>-win-x64`, never over an existing folder. The running copy is never written. Up to the move onto the target, a failure rolls back and leaves the folders as they were; after it, the new version is installed and a cleanup failure is a warning. Ctrl+C is not observed during the commit, so it cannot leave only a backup behind |
| Two updates at once | One lock per module root, or per portable destination, so two portable folders updating to the same release share one. A second update fails at once with `UpdateBusy` and downloads nothing. A target that appears during the update is `AlreadyInstalled` in module mode when valid, and refused in portable mode. The commit move makes up to five attempts over about 3 s when a file is held, as an antivirus scan holds one |
| Network | Only `api.github.com`, `github.com` and `release-assets.githubusercontent.com`, over HTTPS on the default port. The command follows at most five redirects itself, each checked against the three hosts; any other hop is `AdoRedirect`. It uses the system proxy, PAC files included; only the proxy receives the user's Windows credentials. GitHub receives no credentials, no cookie and no `Authorization` header. It sends `User-Agent: AdoToolkit/<running version>`. Time limits: 60 s for the API and the checksum file, 5 min for the module zip, 30 min for the portable zip, 60 s without data. No request is retried |
| After an update | The information stream, tagged `PSHOST` so it shows by default, names the new version, its folder and the version that was running, and says to close the window and open a new one; for a portable copy, to start `Start-AdoToolkit.cmd` in the new folder and to delete the old folder once its window is closed and its reports are moved out. The command never imports the new version and never opens a window |
| `-WhatIf` | Reads the release, one request, and names the folder. When there is something to install it writes nothing and returns no object; `UpToDate` and `AlreadyInstalled` return their object as without it |
| Progress and Verbose | A progress bar in KB while the zip downloads. `-Verbose` gives each request's host and path, never its signed query string, with its status and duration, each redirect, and the line that says both checksums matched |
| Output | `AdoToolkitUpdate`: `Status` (`UpToDate`, `Installed`, `AlreadyInstalled`), `InstallMode` (`Module`, `Portable`), `CurrentVersion`, `LatestVersion`, `Path` (the folder to use next), `PreviousPath` (the running copy's folder once a newer version is installed) and `ReleaseUri`, the release page built from the version. The default table shows `Status`, `CurrentVersion`, `LatestVersion` and `Path` |

### Failures

Each error ID is an existing one, derived from the exception type as before. As for every
cmdlet, only configuration, authentication and authorization errors end the pipeline.

| Failure | Error ID | Category |
| --- | --- | --- |
| Not an installed layout, or a module root off `PSModulePath`; the install path passes through a link; the new version needs a newer PowerShell | `AdoConfiguration` | `InvalidArgument` |
| The proxy refuses the Windows sign-in, as a tunnel error or a 407 response | `AdoAuthentication` | `AuthenticationError` |
| GitHub answers 403 without a rate-limit header | `AdoAuthorization` | `PermissionDenied` |
| 403 or 429 with `x-ratelimit-remaining: 0` or `Retry-After`; the message gives the reset as a local time when GitHub gives one | `AdoThrottled` | `ConnectionError` |
| No release, or an asset missing from it | `AdoNotFound` | `ObjectNotFound` |
| A 5xx status | `AdoServer` | `ConnectionError` |
| DNS, connect, connect timeout or TLS failure, a proxy that refuses the tunnel, a connection lost mid-body, any other status | `AdoRequest` | `InvalidOperation` |
| A redirect to another host, to plain `http`, without `Location`, or more than five | `AdoRedirect` | `InvalidOperation` |
| The total time of a request, or 60 s without data | `AdoTimeout` | `OperationTimeout` |
| Release JSON, tag, digest, size, checksum, archive entry, layout, assembly identity or manifest that is not what the release says | `AdoResponseFormat` | `InvalidResult` |
| The portable target exists, another update holds the lock, the target is in use, the disk is full, a folder cannot be read, any other write failure | `AdoFileOutput` | `WriteError` |

Remote text in a message, a tag or an entry name, is cut to printable characters. Ctrl+C
stops the command as for any cmdlet, and staging and the lock are released.

### Decisions this version takes

| Decision | 0.11.0 |
| --- | --- |
| DD-011, BCL-only runtime, and the toolkit as a client of the Azure DevOps server only | One command now contacts three GitHub hosts, with BCL types only. It shares nothing with the Azure DevOps side: `src/AdoToolkit.Core/Update/` uses no part of `Http/`, `Connections/` or `Configuration/`, no credential provider, profile or session state, and only the cmdlet and `ModuleManifestReader` use it. `UpdateIsolationTests` checks both directions. No other command calls GitHub |
| §5.1, redirects are errors | The updater follows redirects itself, within the three hosts. It is the one exception, and `src/AGENTS.md` names it |
| DD-015, signed packages, superseded by unsigned releases | Releases stay unsigned. Two SHA-256 values from GitHub, over TLS, catch a download damaged or altered in transit or in a cache. They do not authenticate the publisher: whoever can publish a release in the repository can ship code that the updater installs. There is no `-ExpectedThumbprint` |

### Code

| File | Change |
| --- | --- |
| `src/AdoToolkit.PowerShell/Commands/UpdateAdoToolkitCommand.cs` | New cmdlet: detection and the release read in `RunWorker`, `ShouldProcess`, the install, then the message and the object. `TestTransport` is the test seam, a static handler that is null in production and that only the Pester tests set, through reflection |
| `src/AdoToolkit.PowerShell/Commands/Infrastructure/ModuleManifestReader.cs` | Reads a manifest as `Import-PowerShellDataFile` does, with `Parser.ParseFile` and `SafeGetValue`, for Core, which cannot reference `System.Management.Automation` |
| `src/AdoToolkit.Core/Update/AdoToolkitUpdate.cs`, `src/AdoToolkit.Core/Update/AdoToolkitUpdateStatus.cs`, `src/AdoToolkit.Core/Update/AdoToolkitInstallMode.cs` | The output type and its two enums |
| `src/AdoToolkit.Core/Update/ToolkitUpdater.cs`, `src/AdoToolkit.Core/Update/ToolkitUpdatePlan.cs`, `src/AdoToolkit.Core/Update/ToolkitInstallContext.cs`, `src/AdoToolkit.Core/Update/ModuleManifestFacts.cs` | The public engine: `PrepareAsync` detects, reads the release and decides without writing; `InstallAsync` downloads, verifies and installs |
| `src/AdoToolkit.Core/Update/ToolkitInstallation.cs` | Detection of the two layouts, and the target folder names |
| `src/AdoToolkit.Core/Update/UpdateHttp.cs`, `src/AdoToolkit.Core/Update/GitHubClient.cs` | The owner, repository, hosts, URLs, limits and handler; requests, manual redirects, body checks, timers on a `TimeProvider`, and the translation of each failure |
| `src/AdoToolkit.Core/Update/ReleaseReader.cs`, `src/AdoToolkit.Core/Update/ChecksumFile.cs` | The release JSON and the strict tag; the `.sha256` file, read as the installer reads it |
| `src/AdoToolkit.Core/Update/ModuleArchive.cs`, `src/AdoToolkit.Core/Update/PortableArchive.cs` | The archive rules and extraction, and the check of an existing version folder. `ModuleArchive.Layout` and `PortableArchive.RuntimeFiles` repeat the seven package files and the ten runtime files of the packaging scripts |
| `src/AdoToolkit.Core/Update/UpdateFolderCommit.cs`, `src/AdoToolkit.Core/Update/UpdateCommitStep.cs`, `src/AdoToolkit.Core/Update/UpdateGuards.cs` | The lock, staging, the commit, backup and rollback, leftovers, with a fault hook per step; links, deletion that never follows a link, the disk-full and sharing-violation codes, the free-space check and printable remote text |
| `src/AdoToolkit.Core/Diagnostics/AdoProgressPhase.cs` | `ReleaseDownload` |
| `src/AdoToolkit.PowerShell/AdoToolkit.psd1`, `src/AdoToolkit.PowerShell/AdoToolkit.Format.ps1xml` | The cmdlet is exported; the table view of `AdoToolkitUpdate` |

### Strings

50 keys in `src/AdoToolkit.Core/Resources/Strings.resx` and
`src/AdoToolkit.Core/Resources/Strings.fr.resx`, each an `AdoMessage` member: five information
messages, the `ShouldProcess` action, two progress texts, three Verbose lines, one warning and
38 error messages, among them `UpdateRedirectLimit` and `UpdateFolderUnreadable`, which the
implementation added to the plan's list. The French entries were written by the
`fr-translator` subagent and pass `FrenchTerminologyTests`.

### Documentation

| Area | Change |
| --- | --- |
| `docs/commands/en-US/Update-AdoToolkit.md`, `docs/commands/fr-CA/Update-AdoToolkit.md` | New topics: the two layouts and the refusal of any other, both checksums, the three hosts and the proxy, the new window, three examples, the output, the error IDs, GitHub's limit and V-38 |
| `README.md` | A paragraph on this version, a Features row, the update steps under Installation for 0.11.0 and later, and the sentence that AdoToolkit only reads from Azure DevOps, which names the one command that also contacts GitHub; the version and the links to these notes |
| `docs/guides/getting-started.md` | The update steps of a portable copy and of a module, the signed installation as the way to update where signed code is required, the GitHub exception in the troubleshooting list, a help link, and the zip names of 0.11.0 |
| `docs/guides/build-report.md`, `docs/guides/configuration.md` | "AdoToolkit never changes anything in Azure DevOps", and "every request to Azure DevOps accepts gzip and deflate": the updater asks for neither |
| `tools/package/portable/README.txt`, `.github/workflows/release.yml` | The portable bundle's `README.txt` and the text of each GitHub release name `Update-AdoToolkit` for 0.11.0 and later, and the manual steps once from older versions |
| Archive | The 0.10.0 notes moved to `docs/archive/release-0.10.0.md` and the finished 0.11.0 plan to `docs/archive/plans/0.11.0-update-cmdlet.md`, both unedited. The archive indexes list them, the V-38 row of `docs/archive/README.md` resolves to [Known limitations](#known-limitations) below, and the changelog and the 0.10.5 notes point to the new place of the 0.10.0 notes |

### The development tooling and the live checks

| File | Change |
| --- | --- |
| `tools/package/Package.Common.ps1`, `tools/package/Publish-AdoToolkitPackage.ps1`, `tools/package/portable/Start-AdoToolkit.ps1` | The cmdlet count, 22 to 23: `Assert-AdoPackage`, the help sources and compiled help of the publisher, and the launcher's smoke check |
| `tests/Live/Update.Live.ps1`, `tests/Live/Live.Common.ps1` | New check V-38, described under [Work-PC Live checks](#work-pc-live-checks), and its pure helpers `Get-AdoLiveUpdateGate`, `ConvertTo-AdoLiveRelabeledManifest`, `Read-AdoLiveUpdateOutput`, `Get-AdoLiveUpdateVerdict` and `Get-AdoLiveProxyNote` |
| `docs/tooling.md` | The Live checks row of `tests/Live/Update.Live.ps1`, and the one check that needs no profile |
| `AGENTS.md`, `src/AGENTS.md`, `tests/AGENTS.md`, `tools/package/AGENTS.md`, `tests/Live/AGENTS.md` | 23 cmdlets; the updater as the one exception to the request, redirect and file-writing rules, and `TestTransport` as the one mutable static member; the GitHub hosts and `tests/AdoToolkit.PowerShell.Tests/Support/FakeGitHub.ps1` in the tests; the C# copies of the package and runtime lists and the tests that keep them equal; what V-38 may write |
| `.claude/agents/area-reviewer.md`, `.claude/workflows/repo-review.js` | `Update` joins the Core infrastructure review area |

### Tests

| Suite | 0.10.5 | 0.11.0 |
| --- | --- | --- |
| Core, en-US | 1565 executed, 632 test methods | 1808 executed, 723 methods: 236 cases in the eight classes of `tests/AdoToolkit.Core.Tests/Update/`, 4 in `UpdateIsolationTests`, and 3 rows of `FrenchTerminologyTests` for the new French topic |
| Core, fr-CA | 1423 executed | 1662 executed: the same, without `UpdateIsolationTests`, the one new class marked culture-invariant |
| Product Pester | 181 tests, 144 `It` blocks | 233 tests, 160 `It`: 49 in `tests/AdoToolkit.PowerShell.Tests/Update.Pester.ps1`, 2 in `tests/AdoToolkit.PowerShell.Tests/UpdatePortable.Pester.ps1`, 1 in `tests/AdoToolkit.PowerShell.Tests/UpdateLayout.Pester.ps1` |
| Tooling Pester | 331 tests, 227 `It` blocks | 351 tests, 233 `It`: the V-38 tests of `tools/tests/LiveAudit.Tests.ps1` |
| Fixtures | 189 | 189: the update tests build their zips, their release JSON and their assemblies in the test |

The Core classes are `ReleaseReaderTests` (43 cases), `ChecksumFileTests` (15),
`GitHubClientTests` (30), `InstallationDetectionTests` (16), `ModuleArchiveTests` (50),
`PortableArchiveTests` (39), `UpdateFolderCommitTests` (23) and `ToolkitUpdaterTests` (20).
Their DLLs are assemblies emitted at the wanted name and version with
`PersistedAssemblyBuilder`, and the tests of time limits and rate-limit resets run on
`ManualTimeProvider`.
The Pester files install, through the test seam, the module zip and the portable zip that
`New-AdoReleaseArchive` builds from the staged package, the portable one over a synthetic
runtime; run a table of hostile and valid zips through both `Install-AdoToolkit.ps1` and the
cmdlet and require the same verdict, apart from six cases where the cmdlet is stricter by
design; read `$layout` of the installer and the runtime list of
`tools/package/Portable.Common.ps1` with the PowerShell parser and compare them with the C#
copies; and check that a staged copy under `artifacts/verify` is refused with no request.

### Existing tests changed

None was edited to make a failing test pass.

| Test | Change | Reason |
| --- | --- | --- |
| `tests/AdoToolkit.PowerShell.Tests/Module.Pester.ps1` | 23 commands, `Update-AdoToolkit` in the sorted list, and its `-WhatIf` | The new cmdlet |
| `tests/AdoToolkit.PowerShell.Tests/Help.Pester.ps1` | Twenty-three complete topics under each culture | The new topic |
| `tools/tests/Package.Tests.ps1`, `tools/tests/Portable.Tests.ps1` | 23 exported names and help commands in the synthetic packages | The count |
| `tools/tests/ReleaseWorkflow.Tests.ps1` | The release text names ``run `Update-AdoToolkit` `` twice, once per installation | The update steps |
| `tests/AdoToolkit.Core.Tests/Http/ManualTimeProvider.cs` | A settable local time zone, the system's by default | The reset time of a rate limit, formatted as a local time |

No golden changed and no acceptance tag changed.

### Visible changes for 0.10.5 users

- `Update-AdoToolkit` is new. From 0.11.0 on it updates a module installed with
  `Install-AdoToolkit.ps1` or a portable copy; 0.11.0 itself is installed by hand, as before.
- That command contacts GitHub: `api.github.com`, `github.com` and
  `release-assets.githubusercontent.com`, through the system proxy. No other command does.
- After an update, the window that ran it keeps the old version, and `Import-Module AdoToolkit`
  fails there with "Assembly with same name is already loaded" until a new window is opened.
- A module root then holds two version folders; the command removes no old version. A portable
  update leaves the old folder for the user to delete.
- The update steps in the README, the getting-started guide, the portable `README.txt` and the
  release text, and the portable launcher's check of 23 cmdlets.
- Configuration files, the reports, the CSV columns, the JSON report schema and the
  installation layout are unchanged, and the file names of the assets carry 0.11.0.

### Public contract changes

All additive; nothing was removed or renamed.

| Item | Change |
| --- | --- |
| `Update-AdoToolkit` | New cmdlet, with `-WhatIf` and `-Confirm` and no other parameter |
| `AdoToolkit.Core.Update.AdoToolkitUpdate` | New output type: `Status`, `InstallMode`, `CurrentVersion`, `LatestVersion`, `Path`, `PreviousPath`, `ReleaseUri` |
| `AdoToolkit.Core.Update.AdoToolkitUpdateStatus`, `AdoToolkit.Core.Update.AdoToolkitInstallMode` | New public enums: `UpToDate`, `Installed`, `AlreadyInstalled`; `Module`, `Portable` |
| `AdoToolkit.Core.Diagnostics.AdoProgressPhase.ReleaseDownload` | New member of a public enum |
| `ToolkitUpdater`, `ToolkitUpdatePlan`, `ToolkitInstallContext`, `ModuleManifestFacts` in `AdoToolkit.Core.Update` | New public Core types, public because the PowerShell assembly uses them; not meant for scripts |

## Bug fixes

None. 0.11.0 corrects no defect of a cmdlet, a report, the installer or the configuration
file, so section 3 of the `release` skill had nothing to prove: there is no regression test to
fail without a fix, because there is no fix. The two guide sentences reworded for the updater
were true of 0.10.5 and are listed under [Documentation](#documentation).

### Findings not fixed

| Finding | Reason |
| --- | --- |
| On a signed installation, the command installs GitHub's unsigned release beside it | Open for the developer: whether the updater should refuse when the running copy is signed. Where only signed code may load, the new version, which `Import-Module AdoToolkit` prefers, would then fail to load in a new window; removing its folder, or `Import-Module AdoToolkit -RequiredVersion <signed version>`, returns to the signed one. The help and the getting-started guide say to update a signed installation with its own steps instead |
| Old versions accumulate | A decision: no pruning. A module root keeps every version folder, as with the installer, and a portable update leaves the old folder, whose `runtime\pwsh.exe` may still be running |
| `Update-AdoToolkit` and `Install-AdoToolkit.ps1` started at once | As racy as two installers today, apart from one difference: the updater never deletes the installer's `.staging-*` folders, so it cannot break an install in progress. Each takes the target with a move that never replaces |
| The updater is stricter than the installer in six cases | A version folder with a leading zero or a Unicode digit, a checksum file whose zip name differs only in case, a DLL that is not an assembly or carries another version, and more than 64 entries are refused by the updater and accepted by the installer. Only a malformed release has them; the Pester table lists them apart, so that the two can drift only on purpose |
| The findings of 0.10.5 and earlier | Unchanged; see the [0.10.5 notes](release-0.10.5.md#findings-not-fixed), which link those of every earlier version |

### Known limitations

| Limitation | Notes |
| --- | --- |
| V-38: the work proxy | Not confirmed: whether the work network's proxy lets the command reach `api.github.com`, `github.com` and `release-assets.githubusercontent.com`, and whether it accepts the Windows sign-in for them. `tests/Live/Update.Live.ps1` settles it. Until then, and wherever it fails, the manual installation is the way to update |
| The asset digests | The updater needs the `sha256:` digest that GitHub reports for each release asset. The API's description of the published v0.10.5 release, read on 2026-10-05, gives one for each of its five assets and is neither a draft nor a prerelease. A release without them fails with `AdoResponseFormat`, `UpdateDigestMissing` |
| No publisher authentication | The checksums show that the download is the file GitHub holds, not who put it there (DD-015 above). This is the trust of a download by hand from the release page, with both checks done for the user |
| The window that ran the update | A PowerShell process cannot load a newer copy of a binary module that it has loaded (PowerShell/PowerShell#13575): `Import-Module`, `-Force`, `-RequiredVersion` and `Remove-Module` then `Import-Module` all fail, as the plan's spike showed. Open a new window |
| GitHub's limit | 60 requests an hour without sign-in for each network address, which a company's users share behind one proxy address. Each run uses one API request; the downloads go to `github.com`. The limit gives `AdoThrottled` with the time it resets |
| No retry | A failure asks the user to run the command again. At 30 minutes, the 110 MB portable zip needs about 61 KB/s on average |
| 0.10.5 and older | They have no `Update-AdoToolkit`: install 0.11.0 by hand once |
| Limits carried over | The [0.10.5](release-0.10.5.md#known-limitations) known limitations, which link the earlier ones, still apply, with V-01, V-02, V-03, V-27, V-28 and V-31 to V-37 |

## Work-PC Live checks

Install the 0.11.0 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `tests/Live/Update.Live.ps1`, with no input variable, once 0.11.0 is the newest version installed in the user's module folder | V-38: `PASS V-38`. It copies the install into a temporary folder as 0.0.1 and updates that copy in a child process, a real download of the 0.9 MB module zip through the system proxy. The `NOTE V-38 PROXY_API`, `PROXY_SITE` and `PROXY_DOWNLOAD` lines say `DIRECT` or `PROXIED` for each host. `INCONCLUSIVE` comes from a rate limit, a missing release, nothing to install, a configuration refusal, a local file error, or an install that is missing, older than 0.11.0 or invalid. `FAIL V-38 <error ID>`, such as `AdoAuthentication` or `AdoRequest`, means the proxy refused: keep the manual installation and report the ID |
| 2 | In a new window, `Update-AdoToolkit -Verbose` with the installed 0.11.0, then the same in the portable 0.11.0 console | `Status` `UpToDate` with its message and one request to `api.github.com` in each layout. A Documents folder that OneDrive or folder redirection moves should be accepted; `AdoConfiguration` there is a finding to report with its message key, not its path |
| 3 | The checks of the [0.10.5 notes](release-0.10.5.md#work-pc-live-checks), with the 0.11.0 package | V-37 first, then the smoke and probe checks, the console tables, the Excel check and the earlier checks they list. All still pending |

## Local validation and developer handoff

Run on 2026-10-05 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0 with no warning, the product gate running beside the in-process stages. All seven stages pass: 76 PowerShell files linted, 351 tooling Pester tests, 147 configuration files, 90 Markdown files with 302 links and 316 path references, 4 hook helpers, 5 skills and 3 subagents in the layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.11.0 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with no warning, with the exact module pins in `tools/BuildModules.psd1` — Pester 5.9.1, PSScriptAnalyzer 1.25.0 and Microsoft.PowerShell.PlatyPS 1.0.3 — and the same counts. The variable was set for that process only and was never set in the shell |
| Core tests in the final gate | 1808 passed under en-US and 1662 under fr-CA, with no failures or skips: 243 and 239 more than 0.10.5, as [Tests](#tests) lists |
| Product Pester tests against the staged module | 233 passed, none skipped, for 181 in 0.10.5. The tooling tests are 351, for 331 |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.11.0` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.11.0` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/`. The archive's SHA-256 equals the published `hashes.sha256` and the setup action's `POWERSHELL_SHA256` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.11.0-win-x64.zip -ModuleVersion 0.11.0 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.11.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.11.0/`. The portable ZIP has the same seven under `module/`, both launchers, `README.txt` and `bundle.json` (module 0.11.0, PowerShell 7.6.6, win-x64, PowerShell SHA-256 `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860`, equal to the published hash and to the pin of the setup action), 669 entries in all. Each `.sha256` file holds the hash of its ZIP, and the installer is byte-identical to its source |
| `tools/package/Install-AdoToolkit.ps1 -Path ./artifacts/release/AdoToolkit-0.11.0.zip -WhatIf` | Exit 0: the checksum and the layout are accepted and the target is named, `Documents\PowerShell\Modules\AdoToolkit\0.11.0`. Nothing was written, and no module was installed |
| `pwsh -NoProfile -File .\tools\dev.ps1 diagnose` | `ok`, with every required tool present and both recommended tools present: ripgrep 15.2.0 and `gh` 2.102.0. The handoff below therefore opens the pull request with `gh` |
| The release on GitHub | `gh api repos/JosephGibson/Ado_tools/releases/latest`, read-only: v0.10.5, not a draft or a prerelease, with a `sha256:` digest on each of its five assets |

No test failed in either gate run, so nothing is recorded as a flake. A first run, made before the
changelog had its 0.11.0 section and while the 0.10.0 notes were being moved, failed the
changelog test of `tools/tests/ReleaseWorkflow.Tests.ps1` and the documentation stage, as
expected at that point; the product check passed in it with the counts above.

No Live script ran: V-38, V-37, the Excel check and every earlier check are the developer's.
Not run: an actual installation or update outside the tests, the workflows, which only GitHub
runs, a `repo-review` run, and the squash merge, which happens on GitHub after these notes.
The implementation session ran the `docs-sync` and `area-reviewer` subagents and fixed every
finding they confirmed; the release did not run them again.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.11.0-win-x64.zip` | 110483724 | `d09def7b778d62ee58fec77f01c7cec1cc31b2c74d652471ade728bf93992f2a` |
| `AdoToolkit-0.11.0.zip` | 876708 | `6ede091da73f3e28e5bfb76cf34004cad2cdbb0c39751cac05ea82a4af663a15` |
| `Install-AdoToolkit.ps1` | 13567 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag `v0.11.0` exists. The branch `0.11.0`
tracks `origin/0.11.0`, which holds no commit of its own; every change is uncommitted. The
developer owns publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.11.0`
   and pushes the branch and the tag together, which publishes the release at once, and then
   opens the pull request with its final title, falling back to the prefilled compare page if
   `gh pr create` cannot.
2. Run the second command. It waits for the Verify check of the pull request and opens the
   page in a browser once that check passes.
3. Merge with Squash and merge and keep the commit title that GitHub proposes: the title of
   the pull request, then its number.
4. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. Check also that the API lists a `sha256:` digest for
   each asset, which `Update-AdoToolkit` requires. If the run does not publish,
   `docs/tooling.md` lists the recovery for each cause.
5. Install 0.11.0 on the work PC by hand, run V-38 first and then the other work-PC checks
   above, and record any inconclusive coverage.
6. Once the pull request has closed, start the next branch from the updated `main` with the
   command line the handoff gives: it fetches the merge, creates the branch without tracking
   and sets its upstream.
