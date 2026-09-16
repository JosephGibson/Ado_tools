[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'Timestamp')]
param(
    [Parameter(Mandatory = $true)][string] $PackagePath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string] $CertificateThumbprint,
    [Parameter(Mandatory = $true, ParameterSetName = 'Timestamp')][uri] $TimestampServer,
    [Parameter(Mandatory = $true, ParameterSetName = 'NoTimestamp')][switch] $NoTimestamp
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Package.Common.ps1')
$package = Resolve-AdoPackagePath -Path $PackagePath
Assert-AdoPackage -PackagePath $package | Out-Null
if ($NoTimestamp) { Write-Warning 'Signatures without a timestamp may expire with the certificate.' }
elseif (-not $TimestampServer.IsAbsoluteUri -or $TimestampServer.Scheme -notin @('https', 'http')) {
    throw 'Timestamp server must be an absolute HTTP or HTTPS URI.'
}
if ($PSCmdlet.ShouldProcess($package, 'Sign toolkit package')) {
    $certificate = Get-Item -LiteralPath ("Cert:\CurrentUser\My\" + $CertificateThumbprint)
    if (-not $certificate.HasPrivateKey -or
        '1.3.6.1.5.5.7.3.3' -notin @($certificate.EnhancedKeyUsageList | ForEach-Object { [string] $_.ObjectId })) {
        throw 'A code-signing certificate with a private key is required.'
    }
    foreach ($file in Get-AdoSignableFile -PackagePath $package) {
        $arguments = @{ LiteralPath = $file.FullName; Certificate = $certificate; HashAlgorithm = 'SHA256'; ErrorAction = 'Stop' }
        if (-not $NoTimestamp) { $arguments.TimestampServer = $TimestampServer.AbsoluteUri }
        $signature = Set-AuthenticodeSignature @arguments
        if ($signature.Status -ne 'Valid') { throw 'Signing did not produce a valid signature.' }
    }
    Assert-AdoPackageSignature -PackagePath $package -ExpectedThumbprint $CertificateThumbprint
}
