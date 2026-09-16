[CmdletBinding()]
param([switch] $SkipTests)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Resolve-RepositoryPath {
    param([Parameter(Mandatory = $true)][string] $RelativePath)

    $resolved = [System.IO.Path]::GetFullPath((Join-Path $repository $RelativePath))
    $prefix = $repository + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Path must stay inside the repository.'
    }
    $ancestor = $resolved
    while ($ancestor.Length -gt $repository.Length) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw 'Repository paths must not traverse links.'
            }
        }
        $ancestor = Split-Path -Parent $ancestor
    }
    return $resolved
}

function Read-ProjectXml {
    param([Parameter(Mandatory = $true)][string] $Path)

    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        return ,$document
    }
    finally { $reader.Dispose() }
}

try {
    $dotnet = Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue
    if (-not $dotnet) { Write-Output 'dotnet missing: authorized setup step required'; exit 2 }

    $solution = Read-ProjectXml -Path (Resolve-RepositoryPath 'AdoToolkit.slnx')
    $projects = @($solution.SelectNodes('//Project'))
    if ($projects.Count -eq 0) { Write-Output 'No projects declared in AdoToolkit.slnx'; exit 2 }
    foreach ($project in $projects) {
        $projectPath = Resolve-RepositoryPath $project.GetAttribute('Path')
        $assets = Join-Path (Split-Path -Parent $projectPath) 'obj/project.assets.json'
        if (-not (Test-Path -LiteralPath $assets -PathType Leaf)) {
            Write-Output 'restore required: authorized setup step'
            exit 2
        }
    }
    $pester = @(Get-Module -ListAvailable -Name Pester | Where-Object { $_.Version.Major -eq 5 })
    if ($pester.Count -eq 0) { Write-Output 'Pester 5.x missing: authorized setup step required'; exit 2 }
    if (Test-Path -LiteralPath (Join-Path $repository 'docs/commands') -PathType Container) {
        $platy = @(Get-Module -ListAvailable -Name Microsoft.PowerShell.PlatyPS | Where-Object { $_.Version.Major -eq 1 })
        if ($platy.Count -eq 0) { Write-Output 'PlatyPS 1.x missing: authorized setup step required'; exit 2 }
    }

    Push-Location -LiteralPath $repository
    try {
        $buildArguments = @('build', 'AdoToolkit.slnx', '--configuration', 'Release', '--no-restore', '--nologo')
        & $dotnet.Source @buildArguments
        $buildExit = $LASTEXITCODE
        if ($buildExit -ne 0) { exit 1 }

        if (-not $SkipTests) {
            $previousCulture = $env:ADOTOOLKIT_TEST_CULTURE
            $previousGolden = $env:ADOTOOLKIT_UPDATE_GOLDEN
            try {
                $env:ADOTOOLKIT_UPDATE_GOLDEN = $null
                foreach ($culture in @('en-US', 'fr-CA')) {
                    $env:ADOTOOLKIT_TEST_CULTURE = $culture
                    Write-Output "Core tests: $culture"
                    $testArguments = @('test', 'AdoToolkit.slnx', '--configuration', 'Release', '--no-build', '--no-restore', '--nologo')
                    & $dotnet.Source @testArguments
                    $testExit = $LASTEXITCODE
                    if ($testExit -ne 0) { exit 1 }
                }
            }
            finally {
                $env:ADOTOOLKIT_TEST_CULTURE = $previousCulture
                $env:ADOTOOLKIT_UPDATE_GOLDEN = $previousGolden
            }
        }

        $buildProperties = Read-ProjectXml -Path (Resolve-RepositoryPath 'Directory.Build.props')
        $version = $buildProperties.SelectSingleNode('/Project/PropertyGroup/VersionPrefix').InnerText
        if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VersionPrefix must be a three-part module version.' }
        $staging = Resolve-RepositoryPath "artifacts/verify/AdoToolkit/$version"
        $publisher = Resolve-RepositoryPath 'tools/package/Publish-AdoToolkitPackage.ps1'
        if (Test-Path -LiteralPath $publisher -PathType Leaf) {
            $publishArguments = @('-NoProfile', '-File', $publisher, '-NoBuild', '-OutputRoot', (Resolve-RepositoryPath 'artifacts/verify'))
            & (Join-Path $PSHOME 'pwsh.exe') @publishArguments
        }
        else {
            $publishArguments = @('publish', 'src/AdoToolkit.PowerShell', '--configuration', 'Release', '--no-build', '--no-restore', '--nologo', '-o', $staging)
            & $dotnet.Source @publishArguments
        }
        $publishExit = $LASTEXITCODE
        if ($publishExit -ne 0) { exit 1 }

        $manifest = Join-Path $staging 'AdoToolkit.psd1'
        $temporary = Join-Path $staging ([guid]::NewGuid().ToString('N') + '.psd1')
        try {
            $text = [System.IO.File]::ReadAllText($manifest).Replace('@VERSION@', $version)
            [System.IO.File]::WriteAllText($temporary, $text, [System.Text.UTF8Encoding]::new($false))
            $data = Import-PowerShellDataFile -LiteralPath $temporary
            if ($data.ModuleVersion -ne $version) { throw 'Staged manifest version mismatch.' }
            [System.IO.File]::Move($temporary, $manifest, $true)
        }
        finally {
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        }

        # An allowlist inspects every published file, including transitive runtime assets.
        $allowed = @('AdoToolkit.psd1', 'AdoToolkit.Format.ps1xml', 'AdoToolkit.PowerShell.dll',
            'AdoToolkit.Core.dll', 'fr/AdoToolkit.PowerShell.resources.dll',
            'fr/AdoToolkit.Core.resources.dll')
        foreach ($file in Get-ChildItem -LiteralPath $staging -File -Recurse) {
            $relative = [System.IO.Path]::GetRelativePath($staging, $file.FullName).Replace('\', '/')
            $isHelp = $relative -match '^(en-US|fr|fr-CA|fr-FR)/AdoToolkit\.PowerShell\.dll-Help\.xml$'
            if ($relative -notin $allowed -and -not $isHelp) { throw "Unexpected published asset: $relative" }
        }
        foreach ($required in @('AdoToolkit.Core.dll', 'AdoToolkit.PowerShell.dll', 'fr/AdoToolkit.Core.resources.dll', 'fr/AdoToolkit.PowerShell.resources.dll', 'en-US/AdoToolkit.PowerShell.dll-Help.xml', 'fr/AdoToolkit.PowerShell.dll-Help.xml')) {
            if (-not (Test-Path -LiteralPath (Join-Path $staging $required) -PathType Leaf)) { throw "Missing published asset: $required" }
        }
        Write-Output 'Package inspection passed: toolkit assemblies only; no host dependencies or private runtime.'

        if (-not $SkipTests) {
            $previousManifest = $env:ADOTOOLKIT_MODULE_MANIFEST
            try {
                $env:ADOTOOLKIT_MODULE_MANIFEST = $manifest
                $child = @'
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Import-Module Pester -MinimumVersion 5.0 -MaximumVersion 5.999.999 -ErrorAction Stop
$ProgressPreference = 'SilentlyContinue'
$configuration = New-PesterConfiguration
$configuration.Run.Path = Join-Path (Get-Location).Path 'tests/AdoToolkit.PowerShell.Tests'
$configuration.Run.TestExtension = '.Pester.ps1'
$configuration.Run.PassThru = $true
$configuration.Output.Verbosity = 'None'
$result = Invoke-Pester -Configuration $configuration
Write-Output ('Product Pester: {0} passed, {1} failed, {2} skipped, {3} discovered' -f $result.PassedCount, $result.FailedCount, $result.SkippedCount, $result.TotalCount)
if ($result.FailedCount -gt 0 -or $result.FailedContainersCount -gt 0 -or $result.FailedBlocksCount -gt 0) {
    $result.Tests | Where-Object Result -eq 'Failed' | ForEach-Object { Write-Output $_.ExpandedPath; Write-Output $_.ErrorRecord }
    exit 1
}
if ($result.TotalCount -eq 0 -or $result.SkippedCount -gt 0 -or $result.NotRunCount -gt 0) { exit 2 }
exit 0
'@
                $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($child))
                $pesterArguments = @('-NoProfile', '-EncodedCommand', $encoded)
                & (Join-Path $PSHOME 'pwsh.exe') @pesterArguments
                $pesterExit = $LASTEXITCODE
                if ($pesterExit -ne 0) { exit $pesterExit }
            }
            finally { $env:ADOTOOLKIT_MODULE_MANIFEST = $previousManifest }
        }
    }
    finally { Pop-Location }
    # The template marks -SkipTests incomplete; this exit reports the checks actually run.
    exit 0
}
catch {
    Write-Output $_.Exception.Message
    exit 1
}
