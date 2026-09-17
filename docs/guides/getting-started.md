# Getting started

Install AdoToolkit, save a connection profile and check that you can reach your
Azure DevOps Server 2020 collection.

## Before you begin

- Use Windows with PowerShell 7.6. Check with `$PSVersionTable.PSVersion` in a `pwsh`
  window. Windows PowerShell 5.1 (`powershell.exe`) can't load AdoToolkit, and its
  prompt looks the same, so make sure you're in `pwsh`.
- You don't need administrator rights, the .NET SDK or any other module to use a
  release.

## Install the module

### From a release (recommended)

1. From the [releases page](https://github.com/JosephGibson/Ado_tools/releases),
   download `AdoToolkit-<version>.zip`, `AdoToolkit-<version>.zip.sha256` and
   `Install-AdoToolkit.ps1` into one folder.
2. Close every PowerShell window that has AdoToolkit loaded. A loaded module can't
   be replaced until its process ends.
3. In PowerShell 7, from that folder, run:

   ```powershell
   Unblock-File .\Install-AdoToolkit.ps1
   .\Install-AdoToolkit.ps1 -Path .\AdoToolkit-0.1.1.zip
   ```

   The script checks the zip against the `.sha256` file and checks that it contains
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
every module file.

To install without the script, check the hash yourself, then unblock the zip before
extracting it so that no extracted file carries the download mark:

```powershell
(Get-FileHash .\AdoToolkit-0.1.1.zip -Algorithm SHA256).Hash   # compare with the .sha256 file
Unblock-File .\AdoToolkit-0.1.1.zip
Expand-Archive .\AdoToolkit-0.1.1.zip `
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
& .\artifacts\release\Install-AdoToolkit.ps1 -Path .\artifacts\release\AdoToolkit-0.1.1.zip
```

The package script lists every missing prerequisite before it starts. Restore uses
nuget.org only, through the repository `nuget.config`, so machine-wide package feeds
and their credentials are not involved.

A signed installation is still available when you have a code-signing certificate:
sign the staged package with `Set-AdoToolkitPackageSignature.ps1`, then install it with
`Install-AdoToolkitPackage.ps1 -ExpectedThumbprint <thumbprint>`. Both scripts
support `-WhatIf`.

Messages and help follow `$PSUICulture`: French cultures get French text, and all
other cultures get English.

## Save a profile

A profile stores a collection URL, an optional default project and a request
timeout in the local [configuration file](configuration.md). It never stores
credentials: AdoToolkit uses your Windows identity.

```powershell
Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/tfs/DefaultCollection' `
    -DefaultProject 'Web' -DefaultProfile
Get-AdoProfile
```

The collection URL must be an absolute collection-level URL, such as
`https://server/tfs/DefaultCollection` or `https://server/DefaultCollection`. Put the
project in `-DefaultProject`, not in the URL. A URL with a web page or API route is
rejected, and so are Azure DevOps Services hosts. A URL that ends with a project name
can't be recognized until the server is contacted: `Test-AdoConnection` then reports
the corrected `Connect-Ado` command. You can use HTTP, but it produces a warning
because it exposes the Windows authentication exchange.

To change a profile, run `Set-AdoProfile` again with only the values you want to
change. To delete one, use `Remove-AdoProfile -Name work`.

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

When the check fails, the error or the result's `Hint` points to likely causes, such
as the Windows identity in use, the collection URL or the proxy. For example, after
connecting to `https://server/DefaultCollection/Web` by mistake, the hint is:

```text
The collection URL ends with the project name. Connect with: Connect-Ado -CollectionUrl 'https://server/DefaultCollection' -Project 'Web'
```

- Each runspace has its own connection. `Get-AdoConnection` shows it, and
  `Disconnect-Ado` clears it and its caches.
- Cmdlets that contact the server accept `-Connection` to use another connection
  object. Project-scoped cmdlets also accept `-Project`.
- Objects remember their collection. Piping one into a cmdlet that uses a different
  collection produces a `ConnectionMismatch` error instead of querying the wrong
  server.
- AdoToolkit only reads from Azure DevOps. It never creates, changes or queues
  anything.

## Next steps

- [Work items and WIQL](work-items.md)
- [Test Case reports and bulk export](test-case-reports.md)
- [Pipeline failure triage](pipeline-triage.md)

Full help: [Connect-Ado](../commands/en-US/Connect-Ado.md),
[Set-AdoProfile](../commands/en-US/Set-AdoProfile.md),
[Test-AdoConnection](../commands/en-US/Test-AdoConnection.md), or
`Get-Help <cmdlet> -Full`.
