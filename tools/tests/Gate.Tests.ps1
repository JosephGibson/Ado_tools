BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
}

# The real gate against stand-ins: a dotnet that writes test results, a package step, and one
# product Pester file. ADOTOOLKIT_FAKE_GATE names what the stand-ins do apart from passing.
Describe 'Product gate order' {
    BeforeAll {
        function New-GateFixture {
            param([Parameter(Mandatory = $true)][string] $Root)

            foreach ($directory in @('tools/lib', 'tools/package', 'src/Sample/obj', 'shim', 'tests/AdoToolkit.PowerShell.Tests')) {
                [void] [System.IO.Directory]::CreateDirectory((Join-Path $Root $directory))
            }
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../check.ps1') -Destination (Join-Path $Root 'tools/check.ps1')
            foreach ($library in @('test-results.ps1', 'processes.ps1')) {
                Copy-Item -LiteralPath (Join-Path $PSScriptRoot "../lib/$library") -Destination (Join-Path $Root "tools/lib/$library")
            }
            # The product Pester processes import the Pester that runs these tests.
            $pester = (Join-Path (Get-Module -Name Pester).ModuleBase 'Pester.psd1').Replace("'", "''")
            Set-Content -LiteralPath (Join-Path $Root 'tools/lib/dependencies.ps1') -Value @"
function Get-BuildModule { param([string] `$Name) [pscustomobject]@{ Path = if (`$Name -eq 'Pester') { '$pester' } else { 'synthetic' } } }
"@
            Set-Content -LiteralPath (Join-Path $Root 'tools/package/Publish-AdoToolkitPackage.ps1') -Value @'
param([switch] $NoBuild, [string] $OutputRoot)
$folder = Join-Path $OutputRoot 'AdoToolkit/1.2.3'
[void] [System.IO.Directory]::CreateDirectory($folder)
Set-Content -LiteralPath (Join-Path $folder 'AdoToolkit.psd1') -Value '@{}'
'@
            Set-Content -LiteralPath (Join-Path $Root 'tools/package/Package.Common.ps1') -Value "function Assert-AdoPackage { param([string] `$PackagePath) '1.2.3' }"
            Set-Content -LiteralPath (Join-Path $Root 'AdoToolkit.slnx') -Value '<Solution><Project Path="src/Sample/Sample.csproj" /></Solution>'
            Set-Content -LiteralPath (Join-Path $Root 'src/Sample/Sample.csproj') -Value '<Project Sdk="Microsoft.NET.Sdk" />'
            Set-Content -LiteralPath (Join-Path $Root 'src/Sample/obj/project.assets.json') -Value '{}'
            Set-Content -LiteralPath (Join-Path $Root 'Directory.Build.props') -Value '<Project><PropertyGroup><VersionPrefix>1.2.3</VersionPrefix></PropertyGroup></Project>'
            # A cmdlet run with -WhatIf prints straight to the host; once, such text reached the gate's
            # output, where a folder named "missing" made it look like a warning.
            Set-Content -LiteralPath (Join-Path $Root 'tests/AdoToolkit.PowerShell.Tests/Sample.Pester.ps1') -Value @'
Describe 'Sample' {
    It 'sees the staged manifest' {
        [Console]::Out.WriteLine('What if: Performing the operation "Export" on target "C:\temp\missing\Build-401-TestFailures.html".')
        if (@("$env:ADOTOOLKIT_FAKE_GATE" -split ' ') -contains 'pester') { 1 | Should -Be 2 }
        $env:ADOTOOLKIT_MODULE_MANIFEST | Should -BeLike '*AdoToolkit.psd1'
    }
}
'@
            Set-Content -LiteralPath (Join-Path $Root 'shim/dotnet.cmd') -Value '@"%PSHOME_PWSH%" -NoProfile -File "%~dp0dotnet.ps1" %*'
            # A test run counts 3 tests, or 2 under the fr-CA filter. A run that sees
            # ADOTOOLKIT_UPDATE_GOLDEN, or whose culture ADOTOOLKIT_FAKE_GATE names, fails a test; one
            # named as skip-<culture> leaves a test unexecuted; slow-<culture> ends after the others.
            Set-Content -LiteralPath (Join-Path $Root 'shim/dotnet.ps1') -Value @'
if ($args[0] -ne 'test') { exit 0 }
$results = $args[[array]::IndexOf($args, '--results-directory') + 1]
$filtered = [array]::IndexOf($args, '--filter') -ge 0 -and $args[[array]::IndexOf($args, '--filter') + 1] -eq 'Culture!=Invariant'
$total = if ($filtered) { 2 } else { 3 }
$fake = @("$env:ADOTOOLKIT_FAKE_GATE" -split ' ')
$failed = if ($env:ADOTOOLKIT_UPDATE_GOLDEN -or $fake -contains $env:ADOTOOLKIT_TEST_CULTURE) { 1 } else { 0 }
$executed = if ($fake -contains "skip-$env:ADOTOOLKIT_TEST_CULTURE") { $total - 1 } else { $total }
if ($fake -contains "slow-$env:ADOTOOLKIT_TEST_CULTURE") { Start-Sleep -Milliseconds 1500 }
$counters = '<Counters total="{0}" executed="{1}" passed="{2}" failed="{3}" error="0" timeout="0" aborted="0" />' -f $total, $executed, ($executed - $failed), $failed
Set-Content -LiteralPath (Join-Path $results 'run.trx') -Value ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary>' + $counters + '</ResultSummary></TestRun>')
exit $failed
'@
        }

        function Invoke-Gate {
            param([Parameter(Mandatory = $true)][string] $Root, [string] $Scenario)

            $saved = @{}
            foreach ($name in @('PATH', 'PSHOME_PWSH', 'ADOTOOLKIT_UPDATE_GOLDEN', 'ADOTOOLKIT_FAKE_GATE')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
            try {
                $env:PATH = (Join-Path $Root 'shim') + [System.IO.Path]::PathSeparator + $env:PATH
                $env:PSHOME_PWSH = Join-Path $PSHOME 'pwsh.exe'
                $env:ADOTOOLKIT_UPDATE_GOLDEN = '1'
                $env:ADOTOOLKIT_FAKE_GATE = $Scenario
                $output = @(& (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File (Join-Path $Root 'tools/check.ps1'))
                return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output; Results = @($output | Where-Object { $_ -like 'check: *' }) }
            }
            finally { foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) } }
        }
    }

    It 'reports the steps in a fixed order, whichever ends first, and filters the fr-CA run' {
        $root = Join-Path $TestDrive 'gate-order'
        New-GateFixture -Root $root

        $gate = Invoke-Gate -Root $root -Scenario 'slow-en-US'

        $gate.ExitCode | Should -Be 0
        $gate.Results | Should -Be @(
            'check: build succeeded (Release)'
            'check: Core tests (en-US): 3 passed, 0 failed, 3 executed, 3 discovered'
            'check: Core tests (fr-CA): 2 passed, 0 failed, 2 executed, 2 discovered'
            'check: package staged and inspected (AdoToolkit 1.2.3): exact layout, help for every command'
            'check: product Pester: 1 passed, 0 failed, 0 skipped, 1 discovered'
        )
        $gate.Output | Should -Not -Match 'What if'
    }

    It 'reports every step and fails when a Core run fails' {
        $root = Join-Path $TestDrive 'gate-failure'
        New-GateFixture -Root $root

        $gate = Invoke-Gate -Root $root -Scenario 'en-US'

        $gate.ExitCode | Should -Be 1
        $gate.Results | Should -Be @(
            'check: build succeeded (Release)'
            'check: Core tests (en-US): 2 passed, 1 failed, 3 executed, 3 discovered'
            'check: Core tests (fr-CA): 2 passed, 0 failed, 2 executed, 2 discovered'
            'check: package staged and inspected (AdoToolkit 1.2.3): exact layout, help for every command'
            'check: product Pester: 1 passed, 0 failed, 0 skipped, 1 discovered'
        )
    }

    It 'fails on a later failure even when an earlier step is only incomplete' {
        $root = Join-Path $TestDrive 'gate-incomplete-then-failure'
        New-GateFixture -Root $root

        $gate = Invoke-Gate -Root $root -Scenario 'skip-en-US pester'

        $gate.ExitCode | Should -Be 1
        $gate.Results[1] | Should -Be 'check: Core tests (en-US): 2 passed, 0 failed, 2 executed, 3 discovered'
        $gate.Results[-1] | Should -Be 'check: product Pester: 0 passed, 1 failed, 0 skipped, 1 discovered'
    }

    It 'is incomplete when a step is incomplete and none fails' {
        $root = Join-Path $TestDrive 'gate-incomplete'
        New-GateFixture -Root $root

        $gate = Invoke-Gate -Root $root -Scenario 'skip-fr-CA'

        $gate.ExitCode | Should -Be 2
        $gate.Results.Count | Should -Be 5
        $gate.Results[2] | Should -Be 'check: Core tests (fr-CA): 1 passed, 0 failed, 1 executed, 2 discovered'
    }

    It 'fails with the product Pester counts and the failing test' {
        $root = Join-Path $TestDrive 'gate-pester-failure'
        New-GateFixture -Root $root

        $gate = Invoke-Gate -Root $root -Scenario 'pester'

        $gate.ExitCode | Should -Be 1
        $gate.Results[-1] | Should -Be 'check: product Pester: 0 passed, 1 failed, 0 skipped, 1 discovered'
        $gate.Output[-1] | Should -BeLike 'Sample.sees the staged manifest: Expected 2, but got 1.*'
        $gate.Output | Should -Not -Match 'What if'
    }
}
