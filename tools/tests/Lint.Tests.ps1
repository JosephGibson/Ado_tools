Describe 'powershell-lint in a process of its own' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\dev.ps1')

        function New-TestFile {
            param([Parameter(Mandatory = $true)][string] $FilePath, [AllowEmptyString()][string] $Content = '')
            [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $FilePath))
            Set-Content -LiteralPath $FilePath -Value $Content -Encoding utf8
        }

        $analyzerInstalled = @(Get-BuildModule -Name PSScriptAnalyzer).Count -gt 0
        $platyInstalled = @(Get-BuildModule -Name Microsoft.PowerShell.PlatyPS).Count -gt 0
        $repository = Join-Path $TestDrive 'lint-sample'
        New-TestFile (Join-Path $repository 'alias.ps1') ("Set-StrictMode -Version 2.0`n" + 'gci')
        # A PlatyPS cmdlet without its mandatory parameters: the analyzer flags it only when it
        # resolves the command, so the build modules must stay within the analyzer's reach.
        New-TestFile (Join-Path $repository 'help.ps1') 'Export-MamlCommandHelp'
        # More findings than an output of 120 lines of 2,000 characters could carry.
        New-TestFile (Join-Path $repository 'many.ps1') ((1..130 | ForEach-Object { 'gci' }) -join "`n")
        # A module that shadows that cmdlet, on the module path of verify. Where the analyzer sees
        # it, the call resolves to this function, which has no mandatory parameter, and nothing is
        # reported.
        $shadow = Join-Path $TestDrive 'shadow'
        New-TestFile (Join-Path $shadow 'ShadowHelp/1.0.0/ShadowHelp.psm1') 'function Export-MamlCommandHelp { param([string] $Anything) $Anything }'
        New-TestFile (Join-Path $shadow 'ShadowHelp/1.0.0/ShadowHelp.psd1') "@{ RootModule = 'ShadowHelp.psm1'; ModuleVersion = '1.0.0'; GUID = '6f0b7c55-3c4e-4c43-9a8e-7e1a1c0a5b21'; FunctionsToExport = @('Export-MamlCommandHelp'); CmdletsToExport = @(); AliasesToExport = @() }"

        if ($analyzerInstalled) {
            $projectProfile = Get-ProjectProfile -Root $repository
            $plain = Get-PowerShellLintOutcome -ProjectProfile $projectProfile
            $modulePath = $env:PSModulePath
            try {
                $env:PSModulePath = $shadow + [System.IO.Path]::PathSeparator + $modulePath
                $shadowed = Get-PowerShellLintOutcome -ProjectProfile $projectProfile
            }
            finally { $env:PSModulePath = $modulePath }
        }
    }

    It 'reports each finding by path, line and rule, all of a large result, in the order of the files' {
        if (-not $analyzerInstalled) { Set-ItResult -Skipped -Because 'PSScriptAnalyzer is not installed' }
        $expected = @('alias.ps1:2 PSAvoidUsingCmdletAliases') +
            @(if ($platyInstalled) { 'help.ps1:1 PSUseCmdletCorrectly' }) +
            @(1..130 | ForEach-Object { "many.ps1:$_ PSAvoidUsingCmdletAliases" })
        $plain.Failures | Should -Be $expected
        $plain.Unavailable | Should -BeFalse
        # The child's findings travel as one JSON line far longer than 2,000 characters.
        (ConvertTo-Json -InputObject @($plain.Failures) -Compress).Length | Should -BeGreaterThan 2000
    }

    It 'validates a call to a command of a build module' {
        if (-not $analyzerInstalled -or -not $platyInstalled) { Set-ItResult -Skipped -Because 'PSScriptAnalyzer or PlatyPS is not installed' }
        $plain.Failures | Should -Contain 'help.ps1:1 PSUseCmdletCorrectly'
    }

    It 'gives the same findings when the module path of verify holds another module' {
        if (-not $analyzerInstalled -or -not $platyInstalled) { Set-ItResult -Skipped -Because 'PSScriptAnalyzer or PlatyPS is not installed' }
        $shadowed.Failures | Should -Contain 'help.ps1:1 PSUseCmdletCorrectly'
        $shadowed.Failures | Should -Be $plain.Failures
    }
}
