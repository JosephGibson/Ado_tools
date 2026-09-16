@{
    RootModule = 'AdoToolkit.PowerShell.dll'
    RequiredAssemblies = @('AdoToolkit.Core.dll')
    # Replaced from Directory.Build.props during staging and packaging.
    ModuleVersion = '@VERSION@'
    GUID = '04c927e5-7b80-453e-914c-98d17650d28c'
    Author = 'AdoToolkit contributors'
    PowerShellVersion = '7.6'
    CompatiblePSEditions = @('Core')
    FormatsToProcess = @('AdoToolkit.Format.ps1xml')
    CmdletsToExport = @('Connect-Ado', 'Disconnect-Ado', 'Get-AdoConnection', 'Test-AdoConnection',
        'Get-AdoProfile', 'Set-AdoProfile', 'Remove-AdoProfile', 'Get-AdoProject', 'Get-AdoWorkItem', 'Get-AdoTestCase', 'Export-AdoTestCase',
        'Get-AdoTestPlan', 'Get-AdoTestSuite', 'Invoke-AdoWiql',
        'Get-AdoBuildDefinition', 'Get-AdoBuild', 'Get-AdoBuildTimeline', 'Get-AdoBuildFailure', 'Save-AdoBuildLog',
        'Get-AdoTestRun', 'Get-AdoBuildTestFailure', 'Export-AdoBuildTestFailure')
    FunctionsToExport = @()
    AliasesToExport = @()
    VariablesToExport = @()
}
