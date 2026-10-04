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
        # summary of a passing run. They come in the order below, whichever step ends first.
        $buildArguments = @('build', 'AdoToolkit.slnx', '--configuration', 'Release', '--no-restore', '--nologo')
        & $dotnet.Source @buildArguments
        $buildExit = $LASTEXITCODE
        if ($buildExit -ne 0) { exit 1 }
        Write-Output 'check: build succeeded (Release)'

        $buildProperties = Read-ProjectXml -Path (Resolve-RepositoryPath 'Directory.Build.props')
        $declared = $buildProperties.SelectSingleNode('/Project/PropertyGroup/VersionPrefix')
        $version = if ($null -ne $declared) { $declared.InnerText } else { '' }
        if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VersionPrefix must be a three-part module version.' }
        $staging = Resolve-RepositoryPath "artifacts/verify/AdoToolkit/$version"

        . (Join-Path $PSScriptRoot 'lib/test-results.ps1')
        . (Join-Path $PSScriptRoot 'lib/processes.ps1')
        $steps = [System.Collections.Generic.List[object]]::new()
        $coreRuns = @()
        try {
            if (-not $SkipTests) {
                # The Core runs need only the build, so they run beside packaging and product Pester.
                # en-US runs every test. fr-CA leaves out the classes marked [Trait("Culture",
                # "Invariant")], whose assertions the process culture cannot change; tests/AGENTS.md.
                foreach ($run in @(
                        [pscustomobject]@{ Culture = 'en-US'; Filter = $null }
                        [pscustomobject]@{ Culture = 'fr-CA'; Filter = 'Culture!=Invariant' })) {
                    # A successful runner exit alone can hide skipped or undiscovered tests.
                    # Use a fresh folder so previous successful results cannot satisfy this run.
                    $results = Resolve-RepositoryPath ('artifacts/verify/core-' + [guid]::NewGuid().ToString('N'))
                    [void] [System.IO.Directory]::CreateDirectory($results)
                    $testArguments = @('test', 'AdoToolkit.slnx', '--configuration', 'Release', '--no-build', '--no-restore', '--nologo',
                        '--logger', 'trx', '--results-directory', $results)
                    if ($null -ne $run.Filter) { $testArguments += @('--filter', $run.Filter) }
                    $handle = Start-AdoGateProcess -FilePath $dotnet.Source -ArgumentList $testArguments -Environment @{
                        ADOTOOLKIT_TEST_CULTURE = $run.Culture
                        ADOTOOLKIT_UPDATE_GOLDEN = $null
                    }
                    $steps.Add($handle)
                    $coreRuns += [pscustomobject]@{ Culture = $run.Culture; Results = $results; Handle = $handle }
                }
            }

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

            $pesterRun = $null
            if (-not $SkipTests) {
                # Each product test file runs, with the staged module, in a process of its own beside
                # the Core runs. Nothing a cmdlet under test prints, "What if:" text included, reaches
                # this output.
                $pesterArguments = @{
                    TestPath = @(Resolve-RepositoryPath 'tests/AdoToolkit.PowerShell.Tests')
                    PesterManifest = $pester[0].Path
                    TestExtension = '.Pester.ps1'
                    Environment = @{
                        ADOTOOLKIT_MODULE_MANIFEST = $manifest
                        # A developer's default profile would connect implicitly; tests never read real configuration.
                        ADOTOOLKIT_CONFIG_PATH = Resolve-RepositoryPath ('artifacts/verify/config-' + [guid]::NewGuid().ToString('N') + '/config.json')
                    }
                }
                $pesterRun = Invoke-PesterProcess @pesterArguments
            }

            # Every step has ended. Each reports in the order above, so that a failure is never
            # hidden behind an earlier step that was only incomplete.
            $failed = $false
            $incomplete = $false
            foreach ($run in $coreRuns) {
                $completed = Wait-AdoGateProcess -Handle $run.Handle
                Write-Output $completed.Lines
                if ($completed.ExitCode -ne 0) { $failed = $true }
                $reports = @(Get-ChildItem -LiteralPath $run.Results -File -Filter '*.trx')
                if ($reports.Count -eq 0) { Write-Output "Core tests ($($run.Culture)): the result report was not written."; $incomplete = $true }
                foreach ($report in $reports) {
                    $outcome = Get-AdoTestOutcome -Document (Read-ProjectXml -Path $report.FullName)
                    Write-Output "check: Core tests ($($run.Culture)): $($outcome.Summary)"
                    if ($outcome.ExitCode -eq 1) { $failed = $true }
                    elseif ($outcome.ExitCode -eq 2) { $incomplete = $true }
                }
            }
            Write-Output "check: package staged and inspected (AdoToolkit $version): exact layout, help for every command"

            if ($null -ne $pesterRun) {
                Write-Output ('check: product Pester: {0} passed, {1} failed, {2} skipped, {3} discovered' -f
                    $pesterRun.PassedCount, $pesterRun.FailedCount, $pesterRun.SkippedCount, $pesterRun.TotalCount)
                if ($pesterRun.Result -eq 'Failed') {
                    foreach ($container in @($pesterRun.Containers)) {
                        foreach ($message in @($container.Messages)) { Write-Output "$($container.Item): $message" }
                    }
                    foreach ($test in @($pesterRun.Failed | Select-Object -First 20)) {
                        $reason = @("$($test.Message)" -split "`r?`n" | Where-Object { $_ } | Select-Object -First 2) -join ' '
                        Write-Output "$($test.ExpandedPath): $reason"
                    }
                    $failed = $true
                }
                elseif ($pesterRun.TotalCount -eq 0 -or $pesterRun.Incomplete -or
                    ($pesterRun.PassedCount + $pesterRun.FailedCount) -ne $pesterRun.TotalCount) { $incomplete = $true }
            }
            if ($failed) { exit 1 }
            if ($incomplete) { exit 2 }
        }
        finally {
            # A packaging failure or an error ends the gate, and the Core runs stop with it.
            foreach ($step in $steps) { Stop-AdoGateProcess -Handle $step }
            # Each run reads its report, then gives its folder back: a gate that left them behind
            # would fill artifacts/verify with the TRX of every run ever made in this checkout.
            foreach ($run in $coreRuns) {
                if (Test-Path -LiteralPath $run.Results) { Remove-Item -LiteralPath $run.Results -Recurse -Force }
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
