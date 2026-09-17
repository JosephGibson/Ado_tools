# Getting started

Install AdoToolkit, save a connection profile and check that you can reach your
Azure DevOps Server 2020 collection.

## Before you begin

- Meet the [requirements](../../README.md#requirements) on the machine that will use
  the module. Build the module on that machine; don't copy binaries from another one.
- Restore NuGet packages once, as described in
  [Build and verify](../../README.md#build-and-verify).
- Have a code-signing certificate with its private key in `Cert:\CurrentUser\My`.
  The install script accepts only a signed package.

## Install the module

Run these steps in PowerShell 7.6 from the repository root.

1. Build and stage the package. This builds a Release configuration and compiles
   the English and French help:

   ```powershell
   & .\tools\package\Publish-AdoToolkitPackage.ps1       # prints the package version
   $package = (Resolve-Path .\artifacts\AdoToolkit\0.1.0).Path
   ```

2. Sign every script, manifest and toolkit assembly in the package. Signing must be
   the last change to the package:

   ```powershell
   $thumbprint = '<40-character certificate thumbprint>'
   & .\tools\package\Set-AdoToolkitPackageSignature.ps1 -PackagePath $package `
       -CertificateThumbprint $thumbprint -TimestampServer 'https://timestamp.example.test'
   ```

   Use `-NoTimestamp` if you have no timestamp server. Without a timestamp, the
   signatures stop being valid when the certificate expires.

3. Install the package. The script checks the package layout and every signature
   against the expected certificate. It then installs the module to
   `Documents\PowerShell\Modules\AdoToolkit\<version>` and replaces any existing
   copy of the same version:

   ```powershell
   & .\tools\package\Install-AdoToolkitPackage.ps1 -PackagePath $package -ExpectedThumbprint $thumbprint
   ```

   Both the signing and install scripts support `-WhatIf`.

4. Load the module and list its cmdlets:

   ```powershell
   Import-Module AdoToolkit
   Get-Command -Module AdoToolkit
   ```

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

The collection URL must be an absolute collection-level URL. A URL that points to a
project, a web page or an API route is rejected. Azure DevOps Services hosts are
rejected too. You can use HTTP, but it produces a warning because it exposes the
Windows authentication exchange.

To change a profile, run `Set-AdoProfile` again with only the values you want to
change. To delete one, use `Remove-AdoProfile -Name work`.

## Connect

`Connect-Ado` selects the connection for the current PowerShell runspace. It doesn't
contact the server.

```powershell
Connect-Ado                                  # default profile
Connect-Ado -Profile work -Project 'Mobile'  # named profile, different project
Test-AdoConnection                           # calls the server and reports the result
Get-AdoProject -Name 'W*'
```

When the check fails, the error or the result's guidance points to likely causes,
such as the Windows identity in use, the collection URL or the proxy.

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
