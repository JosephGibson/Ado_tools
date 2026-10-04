BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot '../lib/test-results.ps1')
}

Describe 'Product test completeness' {
    It 'returns <Expected> for <Case> even if the runner exits successfully' -TestCases @(
        @{ Case = 'all passed'; Total = 3; Executed = 3; Passed = 3; Failed = 0; Errors = 0; Expected = 0 }
        @{ Case = 'a skipped test'; Total = 3; Executed = 2; Passed = 2; Failed = 0; Errors = 0; Expected = 2 }
        @{ Case = 'no discovered tests'; Total = 0; Executed = 0; Passed = 0; Failed = 0; Errors = 0; Expected = 2 }
        @{ Case = 'a failed test'; Total = 3; Executed = 3; Passed = 2; Failed = 1; Errors = 0; Expected = 1 }
        @{ Case = 'a test error'; Total = 3; Executed = 3; Passed = 2; Failed = 0; Errors = 1; Expected = 1 }
        @{ Case = 'an inconclusive test'; Total = 3; Executed = 3; Passed = 2; Failed = 0; Errors = 0; Expected = 2 }
    ) {
        param($Case, $Total, $Executed, $Passed, $Failed, $Errors, $Expected)
        $document = [xml] @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary><Counters total="$Total" executed="$Executed" passed="$Passed" failed="$Failed" error="$Errors" timeout="0" aborted="0" /></ResultSummary>
</TestRun>
"@
        (Get-AdoTestOutcome -Document $document).ExitCode | Should -Be $Expected
    }

    It 'reports incomplete when counters are absent or malformed' -TestCases @(
        @{ Text = '<TestRun />' }
        @{ Text = '<TestRun><ResultSummary><Counters total="3" /></ResultSummary></TestRun>' }
        @{ Text = '<TestRun><ResultSummary><Counters total="-1" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" /></ResultSummary></TestRun>' }
    ) {
        param($Text)
        (Get-AdoTestOutcome -Document ([xml] $Text)).ExitCode | Should -Be 2
    }
}

Describe 'Product gate steps' {
    It 'gives a step its own environment and literal arguments, and returns its exit code and lines' {
        $script = Join-Path $TestDrive 'step.ps1'
        Set-Content -LiteralPath $script -Value @'
foreach ($argument in $args) { [Console]::Out.WriteLine($argument) }
[Console]::Out.WriteLine("set=$env:ADOTOOLKIT_STEP_SET removed=$env:ADOTOOLKIT_STEP_REMOVED")
[Console]::Error.WriteLine('on stderr')
exit 3
'@
        $previous = $env:ADOTOOLKIT_STEP_REMOVED
        try {
            $env:ADOTOOLKIT_STEP_REMOVED = 'parent'
            $handle = Start-AdoGateProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -ArgumentList @('-NoProfile', '-File', $script, 'a b', 'c;d|e') -Environment @{
                ADOTOOLKIT_STEP_SET = 'child'
                ADOTOOLKIT_STEP_REMOVED = $null
            }
            $result = Wait-AdoGateProcess -Handle $handle
            Stop-AdoGateProcess -Handle $handle

            $result.ExitCode | Should -Be 3
            $result.Lines | Should -Be @('a b', 'c;d|e', 'set=child removed=', 'on stderr')
            $env:ADOTOOLKIT_STEP_REMOVED | Should -Be 'parent'
            $env:ADOTOOLKIT_STEP_SET | Should -BeNullOrEmpty
        }
        finally { $env:ADOTOOLKIT_STEP_REMOVED = $previous }
    }

    It 'stops a step that still runs' {
        $handle = Start-AdoGateProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 60')
        $id = $handle.Process.Id

        Stop-AdoGateProcess -Handle $handle

        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        while ($null -ne (Get-Process -Id $id -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
        Get-Process -Id $id -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
    }
}

Describe 'Product restore preflight' {
    It 'reports missing assets before build without restoring' {
        $fixture = Join-Path $TestDrive 'unrestored'
        [void] [System.IO.Directory]::CreateDirectory((Join-Path $fixture 'tools'))
        [void] [System.IO.Directory]::CreateDirectory((Join-Path $fixture 'src/Sample'))
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../check.ps1') -Destination (Join-Path $fixture 'tools/check.ps1')
        Set-Content -LiteralPath (Join-Path $fixture 'AdoToolkit.slnx') -Value '<Solution><Project Path="src/Sample/Sample.csproj" /></Solution>'
        Set-Content -LiteralPath (Join-Path $fixture 'src/Sample/Sample.csproj') -Value '<Project Sdk="Microsoft.NET.Sdk" />'
        $arguments = @('-NoProfile', '-File', (Join-Path $fixture 'tools/check.ps1'))
        $output = @(& (Join-Path $PSHOME 'pwsh.exe') @arguments)
        $code = $LASTEXITCODE
        $code | Should -Be 2
        $output | Should -Contain 'restore required: authorized setup step'
        Test-Path -LiteralPath (Join-Path $fixture 'src/Sample/obj') | Should -BeFalse
    }
}

Describe 'Product version' {
    It 'names a VersionPrefix that Directory.Build.props does not declare' {
        $fixture = Join-Path $TestDrive 'undeclared-version'
        foreach ($directory in @('tools/lib', 'src/Sample/obj', 'shim')) {
            [void] [System.IO.Directory]::CreateDirectory((Join-Path $fixture $directory))
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../check.ps1') -Destination (Join-Path $fixture 'tools/check.ps1')
        # Stand-ins for the build and the installed modules: the gate reaches its version check
        # without building, restoring or loading anything.
        Set-Content -LiteralPath (Join-Path $fixture 'shim/dotnet.cmd') -Value '@exit /b 0'
        Set-Content -LiteralPath (Join-Path $fixture 'tools/lib/dependencies.ps1') -Value "function Get-BuildModule { [pscustomobject]@{ Path = 'synthetic' } }"
        Set-Content -LiteralPath (Join-Path $fixture 'tools/lib/test-results.ps1') -Value ''
        Set-Content -LiteralPath (Join-Path $fixture 'AdoToolkit.slnx') -Value '<Solution><Project Path="src/Sample/Sample.csproj" /></Solution>'
        Set-Content -LiteralPath (Join-Path $fixture 'src/Sample/Sample.csproj') -Value '<Project Sdk="Microsoft.NET.Sdk" />'
        Set-Content -LiteralPath (Join-Path $fixture 'src/Sample/obj/project.assets.json') -Value '{}'
        Set-Content -LiteralPath (Join-Path $fixture 'Directory.Build.props') -Value '<Project><PropertyGroup /></Project>'
        $arguments = @('-NoProfile', '-File', (Join-Path $fixture 'tools/check.ps1'), '-SkipTests')
        $previousPath = $env:PATH
        try {
            $env:PATH = (Join-Path $fixture 'shim') + [System.IO.Path]::PathSeparator + $previousPath
            $output = @(& (Join-Path $PSHOME 'pwsh.exe') @arguments)
            $code = $LASTEXITCODE
        }
        finally { $env:PATH = $previousPath }
        $code | Should -Be 1
        $output | Should -Be @('check: build succeeded (Release)', 'VersionPrefix must be a three-part module version.')
    }
}
