# Separate from tools/tests/Verify.Tests.ps1 so that the child verify run, one of the longest
# tests, runs beside the rest of the tooling tests.
Describe 'tools/dev.ps1 called with &' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\dev.ps1')

        function New-TestFile {
            param(
                [Parameter(Mandatory = $true)][string] $Path,
                [AllowEmptyString()][string] $Content = ''
            )

            $parent = Split-Path -Parent $Path
            if (-not (Test-Path -LiteralPath $parent)) {
                [void] (New-Item -ItemType Directory -Path $parent -Force)
            }
            Set-Content -LiteralPath $Path -Value $Content -Encoding utf8
        }
    }

    It 'runs its in-process stages when called' {
        # Regression guard: a stage built with GetNewClosure is rebound to a dynamic
        # module whose scope chain excludes this script, so every in-process stage failed
        # with "term is not recognized" unless PowerShell had started dev.ps1 with -File.
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        # The regression concerns invocation scope, not the real product's prerequisites: an
        # isolated repository has a product check that passes. tools/tests/Verify.Tests.ps1 reads a
        # check that exits 2 as incomplete.
        $repository = Join-Path $TestDrive 'invocation'
        $devScript = Join-Path $repository 'tools/dev.ps1'
        New-TestFile $devScript (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../dev.ps1') -Raw)
        foreach ($library in @('discovery', 'dependencies', 'processes', 'validation', 'setup')) {
            New-TestFile (Join-Path $repository "tools/lib/$library.ps1") (Get-Content -LiteralPath (Join-Path $PSScriptRoot "../lib/$library.ps1") -Raw)
        }
        New-TestFile (Join-Path $repository 'tools/BuildModules.psd1') (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../BuildModules.psd1') -Raw)
        New-TestFile (Join-Path $repository 'sample.json') '{}'
        New-TestFile (Join-Path $repository 'PSScriptAnalyzerSettings.psd1') (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../PSScriptAnalyzerSettings.psd1') -Raw)
        New-TestFile (Join-Path $repository 'tools/check.ps1') ('[CmdletBinding()] param([switch] $SkipTests)' + [Environment]::NewLine + 'exit 0')
        $command = "& '" + $devScript.Replace("'", "''") + "' verify -SkipTests; exit " + '$LASTEXITCODE'
        $arguments = @('-NoProfile', '-Command', $command)
        $output = @(& $pwsh @arguments)
        $invocationExit = $LASTEXITCODE
        $document = $output -join [Environment]::NewLine | ConvertFrom-Json

        $invocationExit | Should -Be 2
        $document.Status | Should -Be 'incomplete'
        # A stage that could not call its function fails. Lint is unavailable, not failed, where no
        # analyzer is installed, so only a failure is ruled out.
        @($document.Stages | Where-Object { $_.Name -ne 'project-check' -and $_.Status -eq 'fail' }) | Should -BeNullOrEmpty
        @($document.Stages | Where-Object { $_.Name -in @('configuration', 'tooling-layout') -and $_.Status -ne 'pass' }) | Should -BeNullOrEmpty
        @($document.Stages | Where-Object { $_.Name -eq 'powershell-lint' }).Count | Should -Be 1
        ($document.Stages | Where-Object Name -eq 'project-check').Status | Should -Be 'pass'
        ($document.Stages | Where-Object Name -eq 'project-check').ExitCode | Should -Be 0
    }
}
