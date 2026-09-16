[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string] $PackagePath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string] $ExpectedThumbprint
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Package.Common.ps1')
$package = Resolve-AdoPackagePath -Path $PackagePath
$version = Assert-AdoPackage -PackagePath $package
Assert-AdoPackageSignature -PackagePath $package -ExpectedThumbprint $ExpectedThumbprint
$root = Resolve-AdoPackagePath -Path (Get-AdoModuleRoot)
$destination = Resolve-AdoPackagePath -Path (Join-Path $root $version) -Root $root
if ($PSCmdlet.ShouldProcess($destination, 'Install signed toolkit package')) {
    [void] [System.IO.Directory]::CreateDirectory($root)
    $staging = Resolve-AdoPackagePath -Path (Join-Path $root ('.staging-' + [guid]::NewGuid().ToString('N'))) -Root $root
    try {
        [void] [System.IO.Directory]::CreateDirectory($staging)
        foreach ($file in Get-AdoPackageFile -PackagePath $package) {
            $relative = [System.IO.Path]::GetRelativePath($package, $file.FullName)
            $target = Resolve-AdoPackagePath -Path (Join-Path $staging $relative) -Root $staging
            [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $target))
            [System.IO.File]::Copy($file.FullName, $target)
        }
        if ((Assert-AdoPackage -PackagePath $staging) -ne $version) { throw 'Copied package version changed.' }
        Assert-AdoPackageSignature -PackagePath $staging -ExpectedThumbprint $ExpectedThumbprint
        Move-AdoPackageDirectory -Staging $staging -Destination $destination -Root $root
        Write-Output "Installed AdoToolkit $version"
    }
    finally { Remove-AdoPackageDirectory -Path $staging -Root $root }
}

