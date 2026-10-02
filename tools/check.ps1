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
    # Get-Command lists every dotnet on PATH; the first one is the one a command line would run.
    $dotnet = Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
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
    . (Join-Path $PSScriptRoot 'lib/dependencies.ps1')
    $pester = @(Get-BuildModule -Name Pester)
    if ($pester.Count -eq 0) { Write-Output 'Pester 5.x missing: authorized setup step required'; exit 2 }
    # Packaging compiles the command help in docs/commands.
    $platy = @(Get-BuildModule -Name Microsoft.PowerShell.PlatyPS)
    if ($platy.Count -eq 0) { Write-Output 'PlatyPS 1.x missing: authorized setup step required'; exit 2 }

    Push-Location -LiteralPath $repository
    try {
        # Lines that start with "check: " are this gate's results; dev.ps1 reports them as the
        # summary of a passing run.
        $buildArguments = @('build', 'AdoToolkit.slnx', '--configuration', 'Release', '--no-restore', '--nologo')
        & $dotnet.Source @buildArguments
        $buildExit = $LASTEXITCODE
        if ($buildExit -ne 0) { exit 1 }
        Write-Output 'check: build succeeded (Release)'

        . (Join-Path $PSScriptRoot 'lib/test-results.ps1')
        if (-not $SkipTests) {
            $previousCulture = $env:ADOTOOLKIT_TEST_CULTURE
            $previousGolden = $env:ADOTOOLKIT_UPDATE_GOLDEN
            try {
                $env:ADOTOOLKIT_UPDATE_GOLDEN = $null
                foreach ($culture in @('en-US', 'fr-CA')) {
                    $env:ADOTOOLKIT_TEST_CULTURE = $culture
                    # A successful runner exit alone can hide skipped or undiscovered tests.
                    # Use a fresh folder so previous successful results cannot satisfy this run.
                    $results = Resolve-RepositoryPath ('artifacts/verify/core-' + [guid]::NewGuid().ToString('N'))
                    [void] [System.IO.Directory]::CreateDirectory($results)
                    $testArguments = @('test', 'AdoToolkit.slnx', '--configuration', 'Release', '--no-build', '--no-restore', '--nologo',
                        '--logger', 'trx', '--results-directory', $results)
                    & $dotnet.Source @testArguments
                    $testExit = $LASTEXITCODE
                    if ($testExit -ne 0) { exit 1 }
                    $reports = @(Get-ChildItem -LiteralPath $results -File -Filter '*.trx')
                    if ($reports.Count -eq 0) { Write-Output "Core tests ($culture): the result report was not written."; exit 2 }
                    foreach ($report in $reports) {
                        $outcome = Get-AdoTestOutcome -Document (Read-ProjectXml -Path $report.FullName)
                        Write-Output "check: Core tests ($culture): $($outcome.Summary)"
                        if ($outcome.ExitCode -ne 0) { exit $outcome.ExitCode }
                    }
                }
            }
            finally {
                $env:ADOTOOLKIT_TEST_CULTURE = $previousCulture
                $env:ADOTOOLKIT_UPDATE_GOLDEN = $previousGolden
            }
        }

        $buildProperties = Read-ProjectXml -Path (Resolve-RepositoryPath 'Directory.Build.props')
        $declared = $buildProperties.SelectSingleNode('/Project/PropertyGroup/VersionPrefix')
        $version = if ($null -ne $declared) { $declared.InnerText } else { '' }
        if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VersionPrefix must be a three-part module version.' }
        $staging = Resolve-RepositoryPath "artifacts/verify/AdoToolkit/$version"
        $publishArguments = @('-NoProfile', '-File', (Resolve-RepositoryPath 'tools/package/Publish-AdoToolkitPackage.ps1'),
            '-NoBuild', '-OutputRoot', (Resolve-RepositoryPath 'artifacts/verify'))
        & (Join-Path $PSHOME 'pwsh.exe') @publishArguments
        $publishExit = $LASTEXITCODE
        if ($publishExit -ne 0) { exit 1 }

        # Assert-AdoPackage holds the one package layout: the exact files, help that describes
        # every command, the manifest, and assemblies built for this version.
        . (Join-Path $PSScriptRoot 'package/Package.Common.ps1')
        if ((Assert-AdoPackage -PackagePath $staging) -ne $version) { throw 'Staged package version mismatch.' }
        $manifest = Join-Path $staging 'AdoToolkit.psd1'
        Write-Output "check: package staged and inspected (AdoToolkit $version): exact layout, help for every command"

        if (-not $SkipTests) {
            $previousManifest = $env:ADOTOOLKIT_MODULE_MANIFEST
            $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
            try {
                $env:ADOTOOLKIT_MODULE_MANIFEST = $manifest
                # A developer's default profile would connect implicitly; tests never read real configuration.
                $env:ADOTOOLKIT_CONFIG_PATH = Resolve-RepositoryPath ('artifacts/verify/config-' + [guid]::NewGuid().ToString('N') + '/config.json')
                # The child loads the staged module, which cannot be unloaded from this process.
                # It reports through a file: see Invoke-AdoReportingChild.
                $child = @'
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path (Get-Location).Path 'tools/lib/dependencies.ps1')
$pester = Get-BuildModule -Name Pester
if ($null -eq $pester) { throw 'Pester 5.x is required: authorized setup step.' }
Import-Module -Name $pester.Path -ErrorAction Stop
$ProgressPreference = 'SilentlyContinue'
$configuration = New-PesterConfiguration
$configuration.Run.Path = Join-Path (Get-Location).Path 'tests/AdoToolkit.PowerShell.Tests'
$configuration.Run.TestExtension = '.Pester.ps1'
$configuration.Run.PassThru = $true
$configuration.Output.Verbosity = 'None'
$result = Invoke-Pester -Configuration $configuration
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add(('check: product Pester: {0} passed, {1} failed, {2} skipped, {3} discovered' -f $result.PassedCount, $result.FailedCount, $result.SkippedCount, $result.TotalCount))
$failed = $result.FailedCount -gt 0 -or $result.FailedContainersCount -gt 0 -or $result.FailedBlocksCount -gt 0
if ($failed) {
    foreach ($container in @($result.Containers | Where-Object Result -eq 'Failed')) {
        foreach ($record in @($container.ErrorRecord)) { $lines.Add("$($container.Item): $($record.Exception.Message)") }
    }
    foreach ($test in @($result.Tests | Where-Object Result -eq 'Failed' | Select-Object -First 20)) {
        $reason = @("$($test.ErrorRecord | Select-Object -First 1)" -split "`r?`n" | Where-Object { $_ } | Select-Object -First 2) -join ' '
        $lines.Add("$($test.ExpandedPath): $reason")
    }
}
[System.IO.File]::WriteAllLines($env:ADOTOOLKIT_GATE_REPORT, $lines, [System.Text.UTF8Encoding]::new($false))
if ($failed) { exit 1 }
if ($result.TotalCount -eq 0 -or $result.SkippedCount -gt 0 -or $result.NotRunCount -gt 0) { exit 2 }
exit 0
'@
                $report = Resolve-RepositoryPath ('artifacts/verify/pester-' + [guid]::NewGuid().ToString('N') + '.txt')
                $pesterRun = Invoke-AdoReportingChild -Script $child -ReportPath $report
                Write-Output $pesterRun.Lines
                if ($pesterRun.ExitCode -ne 0) { exit $pesterRun.ExitCode }
            }
            finally {
                $env:ADOTOOLKIT_MODULE_MANIFEST = $previousManifest
                $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig
            }
        }
    }
    finally { Pop-Location }
    # dev.ps1 marks a -SkipTests run incomplete; this exit reports the checks actually run.
    exit 0
}
catch {
    Write-Output $_.Exception.Message
    exit 1
}
