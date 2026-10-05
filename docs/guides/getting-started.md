# Getting started

Install AdoToolkit, save a connection profile and check that you can reach your
Azure DevOps Server 2020 collection.

## Before you begin

- The portable release supports Windows x64 and includes PowerShell and its .NET
  runtime. You don't need to install PowerShell first.
- The module-only release requires your own PowerShell 7.6 installation. Windows
  PowerShell 5.1 (`powershell.exe`) can't load AdoToolkit.
- You don't need administrator rights, the .NET SDK or any other module to use a
  release.

## Install the module

### Portable release (recommended for Windows x64)

1. From the [releases page](https://github.com/JosephGibson/Ado_tools/releases),
   download `AdoToolkit-<version>-win-x64.zip`.
2. Right-click the ZIP in Explorer, choose **Properties**, select **Unblock** if it
   appears, and click **Apply**. Do this before extracting it.
3. Extract the entire ZIP to a folder you can write to.
4. Double-click **`Start-AdoToolkit.cmd`**. The bundled PowerShell opens with
   AdoToolkit loaded. Run `Get-Command -Module AdoToolkit` to list its commands,
   then [save a profile](#save-a-profile).

Always use this launcher for the portable copy. It loads the adjacent module,
without installing it in your user module folder or changing `PATH`. Relative
report paths start in the extracted folder. Startup needs no internet access;
commands that query Azure DevOps still need access to your server.

To update from AdoToolkit 0.11.0 on, run `Update-AdoToolkit` in the console. It
installs the newest release in a new folder, `AdoToolkit-<version>-win-x64`, next to
the old one, which it does not change. Close the console, start `Start-AdoToolkit.cmd`
in the new folder, and delete the old folder once its console is closed and you have
moved out any reports saved in it. The command needs HTTPS access to GitHub; whether the
proxy at work allows it is not confirmed (V-38). From 0.10.5 or older, or when the command
cannot reach GitHub, extract the new release into a new folder and use its launcher. Saved
profiles remain in your Windows user profile. The bundled PowerShell version is tested
and pinned for each release; it does not update itself. PowerShell fixes are delivered
in updated AdoToolkit portable releases.

The `.zip.sha256` asset and release notes provide the bundle's checksum. To check
it before extraction, run this in Windows PowerShell or PowerShell 7 and compare
the result with the published checksum:

```powershell
(Get-FileHash .\AdoToolkit-0.11.0-win-x64.zip -Algorithm SHA256).Hash
```

The launcher uses `RemoteSigned` for its process only. It does not change your
saved execution policy or override Group Policy. AdoToolkit is unsigned, so an
organization that requires signed code needs a signed build. If a downloaded
script is blocked, unblock the original ZIP in Properties and extract it again.

### Module-only release (existing PowerShell 7.6)

1. From the [releases page](https://github.com/JosephGibson/Ado_tools/releases),
   download `AdoToolkit-<version>.zip`, `AdoToolkit-<version>.zip.sha256` and
   `Install-AdoToolkit.ps1` into one folder.
2. Close every PowerShell window that has AdoToolkit loaded. A loaded module can't
   be replaced until its process ends.
3. In PowerShell 7, from that folder, run:

   ```powershell
   Unblock-File .\Install-AdoToolkit.ps1
   .\Install-AdoToolkit.ps1 -Path .\AdoToolkit-0.11.0.zip
   ```

   The script checks the ZIP against the `.sha256` file and checks that it contains
   exactly the module files. It then installs them to
   `Documents\PowerShell\Modules\AdoToolkit\<version>`. An existing copy of the same
   version is replaced only after the new copy is complete. `-WhatIf` shows the
   target without writing anything.
4. Open a new PowerShell window, load the module and list its cmdlets:

   ```powershell
   Import-Module AdoToolkit
   Get-Command -Module AdoToolkit
   ```

`Unblock-File` is needed because Windows marks downloaded files, and PowerShell 7's
default `RemoteSigned` policy refuses to run marked scripts that aren't signed. The
installed module files carry no mark, so `Import-Module` works without
`-ExecutionPolicy Bypass`. Releases are not code-signed; the checksum protects the
download instead. If your organization enforces `AllSigned` through Group Policy,
unsigned releases can't be loaded, and you need a signed build. For a signed release,
add `-ExpectedThumbprint <certificate thumbprint>` to require a valid signature on
the manifest, the format file and every toolkit assembly.

To update a module installed this way, from AdoToolkit 0.11.0 on, run
`Update-AdoToolkit`. It installs the newest release in its own version folder beside
the running one, after checking it against both of its checksums, and then asks you to
open a new PowerShell window: a window that has loaded AdoToolkit keeps that version
until it closes. It updates only a copy under a folder of `PSModulePath`. It checks no
code signature and installs GitHub's unsigned release: where signed code is required,
update with the signed installation under [From source](#from-source) instead. Whether
the proxy at work lets it reach
GitHub is not confirmed (V-38). From 0.10.5 or older, or when the command cannot reach
GitHub, install the new release with the steps above.

To install without the script, check the hash yourself, then unblock the ZIP before
extracting it so that no extracted file carries the download mark:

```powershell
(Get-FileHash .\AdoToolkit-0.11.0.zip -Algorithm SHA256).Hash   # compare with the .sha256 file
Unblock-File .\AdoToolkit-0.11.0.zip
Expand-Archive .\AdoToolkit-0.11.0.zip `
    -DestinationPath (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'PowerShell\Modules')
```

### From source

Building needs the .NET SDK selected by `global.json` and PlatyPS 1.x for PowerShell 7.
A per-user .NET install works without administrator rights; see
[Developer tooling](../tooling.md#prerequisites). From the repository root, in
PowerShell 7:

```powershell
& .\tools\package\Publish-AdoToolkitPackage.ps1    # restores, builds and stages the package
& .\tools\package\New-AdoToolkitRelease.ps1        # writes artifacts\release
& .\artifacts\release\Install-AdoToolkit.ps1 -Path .\artifacts\release\AdoToolkit-0.11.0.zip
```

The package script lists every missing prerequisite before it starts. Restore uses
nuget.org only, through the repository `nuget.config`, so machine-wide package feeds
and their credentials are not involved.

A signed installation is still available when you have a code-signing certificate:
sign the staged package with `tools/package/Set-AdoToolkitPackageSignature.ps1`, then
install it with `tools/package/Install-AdoToolkitPackage.ps1`. Both scripts take
`-PackagePath` and the certificate thumbprint, and support `-WhatIf`.

Messages and help follow `$PSUICulture`: French cultures get French text, and all
other cultures get English.

## Save a profile

A profile stores a collection URL, an optional default project and a request
timeout in the local [configuration file](configuration.md). It never stores
credentials: AdoToolkit uses your Windows identity. It can also store a default
branch, build definition, test plan and test suite, so that you don't have to pass
`-Branch`, `-Definition`, `-PlanId` and `-SuiteId` to every command; see
[Profiles](configuration.md#profiles).

```powershell
Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/tfs/DefaultCollection' `
    -DefaultProject 'Web' -DefaultProfile
Get-AdoProfile
```

The collection URL must be an absolute collection-level URL, such as
`https://ado.example.test/tfs/DefaultCollection` or
`https://ado.example.test/DefaultCollection`. Put the
project in `-DefaultProject`, not in the URL. A URL with a web page or API route is
rejected, and so are Azure DevOps Services hosts. A URL that ends with a project name
can't be recognized until the server is contacted: `Test-AdoConnection` then reports
the corrected `Connect-Ado` command. You can use HTTP, but it produces a warning
because it exposes the Windows authentication exchange.

To change a profile, run `Set-AdoProfile` again with only the values you want to
change; `-DefaultProject ''` removes the default project. To delete a profile, use
`Remove-AdoProfile -Name work`.

## Connect

`Connect-Ado` selects the connection for the current PowerShell runspace. It doesn't
contact the server. With a default profile you can skip it: the first command that
needs a connection connects with the default profile in the same way, and says so
with `-Verbose`.

```powershell
Connect-Ado                                  # default profile
Connect-Ado -Profile work -Project 'Mobile'  # named profile, different project
Test-AdoConnection                           # calls the server and reports the result
Get-AdoProject -Name 'W*'
```

Project names complete from the runspace cache after `Get-AdoProject` has listed them;
profile names complete from the local configuration. You can start either kind of name
with single or double quotes. Completion inserts a quoted, pasteable argument and sends
no server request. `Get-AdoProfile -Name` takes a wildcard pattern, so there the inserted
name has its `[`, `]`, `*`, `?` and backtick characters escaped and matches that profile
only.

When the check fails, the error or the result's `Hint` points to likely causes, such
as the Windows identity in use, the collection URL or the proxy. For example, after
connecting to `https://ado.example.test/DefaultCollection/Web` by mistake, the hint is:

```text
The collection URL ends with the project name. Connect with: Connect-Ado -CollectionUrl 'https://ado.example.test/DefaultCollection' -Project 'Web'
```

- Each runspace has its own connection. `Get-AdoConnection` shows it, and
  `Disconnect-Ado` clears it and its caches.
- Cmdlets that contact the server accept `-Connection` to use another connection
  object. Project-scoped cmdlets also accept `-Project`.
- Objects remember their collection. Piping one into a cmdlet that uses a different
  collection produces an `AdoConnectionMismatch` error instead of querying the wrong
  server.
- Pipe the objects that AdoToolkit commands return. A hand-made object that lacks a
  required property is refused with an error for that input, and the rest of the input
  is still processed.
- AdoToolkit only reads from Azure DevOps. It never creates, changes or queues
  anything there. `Update-AdoToolkit` is the one command that contacts anything else:
  GitHub, for AdoToolkit's own releases.

## Next steps

- [Work items and WIQL](work-items.md)
- [Test Case reports and bulk export](test-case-reports.md)
- [Pipeline failure triage](pipeline-triage.md)
- [Export a report from a build ID](build-report.md)

Full help: [Connect-Ado](../commands/en-US/Connect-Ado.md),
[Set-AdoProfile](../commands/en-US/Set-AdoProfile.md),
[Get-AdoProfile](../commands/en-US/Get-AdoProfile.md),
[Test-AdoConnection](../commands/en-US/Test-AdoConnection.md),
[Get-AdoProject](../commands/en-US/Get-AdoProject.md),
[Get-AdoConnection](../commands/en-US/Get-AdoConnection.md),
[Disconnect-Ado](../commands/en-US/Disconnect-Ado.md),
[Remove-AdoProfile](../commands/en-US/Remove-AdoProfile.md),
[Update-AdoToolkit](../commands/en-US/Update-AdoToolkit.md), or
`Get-Help <cmdlet> -Full`.
