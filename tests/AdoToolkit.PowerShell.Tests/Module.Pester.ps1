BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $manifest = $env:ADOTOOLKIT_MODULE_MANIFEST
}

Describe 'Staged binary module' -Tag 'S0-1' {
    It 'imports the staged manifest in the fresh Pester process' {
        $manifest | Should -Not -BeNullOrEmpty
        Test-Path -LiteralPath $manifest -PathType Leaf | Should -BeTrue
        $module = Import-Module -Name $manifest -PassThru -ErrorAction Stop
        $module.Name | Should -Be 'AdoToolkit'
        $module.RootModule | Should -Be 'AdoToolkit.PowerShell.dll'
    }

    It 'changes no session preferences on import' -Tag 'S0-6' {
        $names = @('ErrorActionPreference', 'ProgressPreference', 'VerbosePreference', 'WarningPreference', 'DebugPreference', 'InformationPreference', 'ConfirmPreference', 'WhatIfPreference')
        $before = @{}
        foreach ($name in $names) { $before[$name] = Get-Variable -Name $name -ValueOnly }
        Import-Module -Name $manifest -Force
        foreach ($name in $names) { Get-Variable -Name $name -ValueOnly | Should -Be $before[$name] }
    }

    It 'exports exactly the implemented cmdlets with output type metadata' {
        $commands = @(Get-Command -Module AdoToolkit)
        $commands.Count | Should -Be 22
        $commands.Name | Sort-Object | Should -Be @('Connect-Ado','Disconnect-Ado','Export-AdoBuildTestFailure','Export-AdoTestCase','Get-AdoBuild','Get-AdoBuildDefinition','Get-AdoBuildFailure','Get-AdoBuildTestFailure','Get-AdoBuildTimeline','Get-AdoConnection','Get-AdoProfile','Get-AdoProject','Get-AdoTestCase','Get-AdoTestPlan','Get-AdoTestRun','Get-AdoTestSuite','Get-AdoWorkItem','Invoke-AdoWiql','Remove-AdoProfile','Save-AdoBuildLog','Set-AdoProfile','Test-AdoConnection')
        foreach ($command in $commands) { $command.OutputType.Count | Should -BeGreaterThan 0 }
        (Get-Command Connect-Ado).ParameterSets.Name | Should -Contain 'ByUrl'
        (Get-Command Connect-Ado).ParameterSets.Name | Should -Contain 'ByProfile'
        (Get-Command Set-AdoProfile).Parameters.Keys | Should -Contain 'WhatIf'
        (Get-Command Remove-AdoProfile).Parameters.Keys | Should -Contain 'WhatIf'
        (Get-Command Export-AdoBuildTestFailure).Parameters.Keys | Should -Contain 'WhatIf'
    }
}
