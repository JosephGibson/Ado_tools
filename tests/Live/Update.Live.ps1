[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$InformationPreference = 'SilentlyContinue'

# Opt-in work-machine check of V-38: whether the work network lets Update-AdoToolkit reach
# api.github.com, github.com and release-assets.githubusercontent.com. It copies the newest installed
# release into a temporary folder as version 0.0.1 and updates that copy in a child process: a real
# download of the module zip through the system proxy, by the module's own client. It prints codes
# only, never a URL, a path or a message, and deletes the folder. It needs no input variable.
. (Join-Path $PSScriptRoot 'Live.Common.ps1')
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('AdoToolkit-V38-' + [guid]::NewGuid().ToString('N'))
$settled = $false
try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    $root = Get-AdoModuleRoot
    $newest = $null
    if (Test-Path -LiteralPath $root -PathType Container) {
        $newest = Get-ChildItem -LiteralPath $root -Directory |
            Where-Object { $_.Name -cmatch '^[0-9]+\.[0-9]+\.[0-9]+$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'AdoToolkit.psd1') -PathType Leaf) } |
            Sort-Object -Property { [version] $_.Name } -Descending | Select-Object -First 1
    }
    # The version is read before the package check, which counts the commands of this checkout.
    $gate = Get-AdoLiveUpdateGate -Installed $(if ($null -ne $newest) { [version] $newest.Name })
    if ($gate) {
        Write-Output $gate
        $settled = $true
        exit 2
    }
    try { $null = Assert-AdoPackage -PackagePath $newest.FullName }
    catch {
        Write-Output 'INCONCLUSIVE V-38 INSTALLED_MODULE_INVALID'
        $settled = $true
        exit 2
    }
    $modules = Join-Path $temporary 'Modules'
    $copy = Join-Path $modules 'AdoToolkit/0.0.1'
    [void] [IO.Directory]::CreateDirectory($copy)
    Get-ChildItem -LiteralPath $newest.FullName -Force | Copy-Item -Destination $copy -Recurse -Force
    $manifest = Join-Path $copy 'AdoToolkit.psd1'
    [IO.File]::WriteAllText($manifest, (ConvertTo-AdoLiveRelabeledManifest -Text ([IO.File]::ReadAllText($manifest)) -Version '0.0.1'),
        [Text.UTF8Encoding]::new($false))
    # A process of its own: this one may already hold the installed assemblies. The child, whose
    # client makes the requests, also says whether the system proxy carries each host.
    $quote = { param([string] $Text) "'" + [System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($Text) + "'" }
    $child = @"
`$ErrorActionPreference = 'Stop'
`$ProgressPreference = 'SilentlyContinue'
function Get-AdoLiveProxyNote {
$(${function:Get-AdoLiveProxyNote})
}
foreach (`$pair in @(@('API', 'https://api.github.com/'), @('SITE', 'https://github.com/'), @('DOWNLOAD', 'https://release-assets.githubusercontent.com/'))) {
    `$destination = [uri] `$pair[1]
    'PROXY ' + `$pair[0] + ' ' + (Get-AdoLiveProxyNote -Proxy ([System.Net.Http.HttpClient]::DefaultProxy.GetProxy(`$destination)) -Destination `$destination)
}
`$env:PSModulePath = $(& $quote $modules) + [IO.Path]::PathSeparator + `$env:PSModulePath
try {
    Import-Module -Name $(& $quote $manifest)
    `$result = Update-AdoToolkit -ErrorAction Stop -Confirm:`$false 6>`$null
    'STATUS ' + `$result.Status
    'FILES ' + @(Get-ChildItem -LiteralPath `$result.Path -Recurse -File).Count
}
catch { 'ERROR ' + (`$_.FullyQualifiedErrorId -split ',')[0] }
"@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($child))
    $childLines = @(& (Join-Path $PSHOME 'pwsh.exe') -NoProfile -NonInteractive -EncodedCommand $encoded 2>$null)

    $outcome = Read-AdoLiveUpdateOutput -Line $childLines
    foreach ($name in $outcome.Proxy.Keys) { Write-Output ('NOTE V-38 PROXY_' + $name + ' ' + $outcome.Proxy[$name]) }
    $verdict = Get-AdoLiveUpdateVerdict -Status $outcome.Status -Files $outcome.Files -ErrorId $outcome.ErrorId
    Write-Output $verdict
    $settled = $true
    if ($verdict.StartsWith('PASS ', [StringComparison]::Ordinal)) { exit 0 }
    if ($verdict.StartsWith('FAIL ', [StringComparison]::Ordinal)) { exit 1 }
    exit 2
}
catch {
    # A failure after the verdict was printed does not print a second one.
    if (-not $settled) { Write-Output 'FAIL V-38 CHECK_FAILED' }
    exit 1
}
finally {
    # An antivirus scan may still hold the files that the child extracted a moment ago.
    for ($attempt = 1; $attempt -le 5 -and (Test-Path -LiteralPath $temporary); $attempt++) {
        try { Remove-AdoPackageDirectory -Path $temporary -Root ([IO.Path]::GetTempPath()) }
        catch { Start-Sleep -Milliseconds 600 }
    }
    if (Test-Path -LiteralPath $temporary) { Write-Output 'NOTE V-38 TEMPORARY_FOLDER_LEFT' }
}
