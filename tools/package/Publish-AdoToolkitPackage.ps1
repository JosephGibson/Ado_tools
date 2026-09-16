[CmdletBinding()]
param([switch] $NoBuild, [string] $OutputRoot)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Package.Common.ps1')
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $repository 'artifacts' }
$root = Resolve-AdoPackagePath -Path $OutputRoot -Root $repository
$project = Join-Path $repository 'src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj'
$staging = $null
$helpStaging = $null
try {
    Import-Module Microsoft.PowerShell.PlatyPS -MinimumVersion 1.0 -MaximumVersion 1.999.999 -ErrorAction Stop
    $arguments = @('msbuild', $project, '-getProperty:Version', '-nologo')
    $version = (& dotnet @arguments | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^\d+\.\d+\.\d+$') { throw 'MSBuild did not return a supported module version.' }
    if (-not $NoBuild) {
        $arguments = @('build', $project, '--configuration', 'Release', '--no-restore', '--nologo')
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw 'Package build failed.' }
    }
    $parent = Resolve-AdoPackagePath -Path (Join-Path $root 'AdoToolkit') -Root $repository
    [void] [System.IO.Directory]::CreateDirectory($parent)
    $destination = Resolve-AdoPackagePath -Path (Join-Path $parent $version) -Root $repository
    $staging = Resolve-AdoPackagePath -Path (Join-Path $parent ('.staging-' + [guid]::NewGuid().ToString('N'))) -Root $repository
    $helpStaging = Resolve-AdoPackagePath -Path (Join-Path $parent ('.help-' + [guid]::NewGuid().ToString('N'))) -Root $repository
    [void] [System.IO.Directory]::CreateDirectory($staging)
    $arguments = @('publish', $project, '--configuration', 'Release', '--no-build', '--no-restore', '--nologo', '--output', $staging)
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Package publish failed.' }
    $manifest = Join-Path $staging 'AdoToolkit.psd1'
    $temporary = Join-Path $staging ([guid]::NewGuid().ToString('N') + '.psd1')
    try {
        $text = [System.IO.File]::ReadAllText($manifest).Replace('@VERSION@', $version)
        [System.IO.File]::WriteAllText($temporary, $text, [System.Text.UTF8Encoding]::new($false))
        $data = Import-PowerShellDataFile -LiteralPath $temporary
        if ($data.ModuleVersion -ne $version) { throw 'Staged manifest version mismatch.' }
        [System.IO.File]::Move($temporary, $manifest, $true)
    }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
    foreach ($culture in @('en-US', 'fr-CA')) {
        $source = Resolve-AdoPackagePath -Path (Join-Path $repository "docs/commands/$culture") -Root $repository
        $documents = @(Get-AdoPackageFile -PackagePath $source | Where-Object Extension -eq '.md')
        if ($documents.Count -ne 22) { throw 'Expected twenty-two Markdown command help sources per culture.' }
        $help = @(Import-MarkdownCommandHelp -LiteralPath $documents.FullName)
        $exported = @(Export-MamlCommandHelp -CommandHelp $help -OutputFolder (Join-Path $helpStaging $culture) -Force)
        if ($exported.Count -ne 1) { throw 'Expected one compiled command help file per culture.' }
        $generated = Resolve-AdoPackagePath -Path $exported[0].FullName -Root $helpStaging
        $xml = Read-AdoPackageXml -Path $generated
        if ($xml.SelectNodes("//*[local-name()='command']").Count -ne 22) { throw 'Generated help is incomplete.' }
        $folder = if ($culture -eq 'fr-CA') { 'fr' } else { $culture }
        $helpTarget = Join-Path $staging $folder
        [void] [System.IO.Directory]::CreateDirectory($helpTarget)
        [System.IO.File]::Move($generated, (Join-Path $helpTarget 'AdoToolkit.PowerShell.dll-Help.xml'))
    }
    if ((Assert-AdoPackage -PackagePath $staging) -ne $version) { throw 'Package version mismatch.' }
    Move-AdoPackageDirectory -Staging $staging -Destination $destination -Root $repository
    Write-Output "Package ready: AdoToolkit/$version"
}
finally {
    if ($staging) { Remove-AdoPackageDirectory -Path $staging -Root $repository }
    if ($helpStaging) { Remove-AdoPackageDirectory -Path $helpStaging -Root $repository }
}
