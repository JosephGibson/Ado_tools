BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
}

AfterAll { $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig }

# PowerShell builds a parameter's type from any property bag, so [pscustomobject]@{ Id = 44 }
# binds as a toolkit object whose required members are null. Such an input is reported for that
# input, names its type, and never reaches the server.
Describe 'Hand-made pipeline input' {
    BeforeEach {
        $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '/config.json')
        $outputDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($outputDirectory)
        $server = Start-FakeAdoServer
        Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
    }
    AfterEach {
        Disconnect-Ado
        Stop-FakeAdoServer -Server $server
    }

    It '<Command> reports an incomplete <Type> as a per-input error' -TestCases @(
        @{ Command = 'Get-AdoBuild'; Type = 'AdoBuildDefinition'; Class = 'GetAdoBuildCommand' }
        @{ Command = 'Get-AdoTestSuite'; Type = 'AdoTestPlan'; Class = 'GetAdoTestSuiteCommand' }
        @{ Command = 'Get-AdoBuildFailure'; Type = 'AdoBuild'; Class = 'GetAdoBuildFailureCommand' }
        @{ Command = 'Get-AdoBuildTimeline'; Type = 'AdoBuild'; Class = 'GetAdoBuildTimelineCommand' }
        @{ Command = 'Get-AdoTestRun'; Type = 'AdoBuild'; Class = 'GetAdoTestRunCommand' }
        @{ Command = 'Get-AdoBuildTestFailure'; Type = 'AdoBuild'; Class = 'GetAdoBuildTestFailureCommand' }
    ) {
        param($Command, $Type, $Class)
        $failures = $null
        $output = @([pscustomobject]@{ Id = 44 }, [pscustomobject]@{ Id = 45 } |
            & $Command -ErrorAction SilentlyContinue -ErrorVariable failures -WarningAction SilentlyContinue)
        $output.Count | Should -Be 0
        # One error per input: the first incomplete object does not stop the pipeline.
        @($failures).Count | Should -Be 2
        foreach ($failure in $failures) {
            $failure.FullyQualifiedErrorId | Should -Be "AdoRequest,AdoToolkit.$Class"
            [string] $failure | Should -Match "\b$Type\b"
        }
        $server.Requests.Count | Should -Be 0
    }

    It 'Get-AdoTestCase reports an incomplete suite' {
        { Get-AdoTestCase -Suite ([pscustomobject]@{ Id = 44; PlanId = 1 }) -ErrorAction Stop } |
            Should -Throw -ErrorId 'AdoRequest,AdoToolkit.GetAdoTestCaseCommand' -ExpectedMessage '*AdoTestSuite*'
        $server.Requests.Count | Should -Be 0
    }

    It 'Export-AdoTestCase reports an incomplete test case and writes nothing' {
        { [pscustomobject]@{ Id = 44 } | Export-AdoTestCase -Path $outputDirectory -ErrorAction Stop } |
            Should -Throw -ErrorId 'AdoRequest,AdoToolkit.ExportAdoTestCaseCommand' -ExpectedMessage '*AdoTestCase*'
        @(Get-ChildItem -LiteralPath $outputDirectory -Force).Count | Should -Be 0
    }

    It 'Export-AdoBuildTestFailure reports an incomplete failure set and writes nothing' {
        { [pscustomobject]@{ FailedCount = 1 } | Export-AdoBuildTestFailure -Path $outputDirectory -ErrorAction Stop } |
            Should -Throw -ErrorId 'AdoRequest,AdoToolkit.ExportAdoBuildTestFailureCommand' -ExpectedMessage '*AdoBuildTestFailureSet*'
        @(Get-ChildItem -LiteralPath $outputDirectory -Force).Count | Should -Be 0
        $server.Requests.Count | Should -Be 0
    }

    It 'Remove-AdoProfile reports a profile without a name and removes nothing' {
        Set-AdoProfile -Name kept -CollectionUrl 'https://ado.example.test/Collection' | Out-Null
        { [pscustomobject]@{ CollectionUri = 'https://ado.example.test/Collection' } | Remove-AdoProfile -Confirm:$false -ErrorAction Stop } |
            Should -Throw -ErrorId 'AdoRequest,AdoToolkit.RemoveAdoProfileCommand' -ExpectedMessage '*AdoProfile*'
        @(Get-AdoProfile).Name | Should -Be @('kept')
    }

    It 'a connection without a usable <Member> is a configuration error' -TestCases @(
        @{ Member = 'CollectionUri'; Bag = @{ DefaultProject = 'Équipe Web' } }
        @{ Member = 'RequestTimeoutSeconds'; Bag = @{ CollectionUri = 'http://127.0.0.1:9/Collection'; RequestTimeoutSeconds = 0 } }
    ) {
        param($Member, $Bag)
        { [pscustomobject] $Bag | Test-AdoConnection -ErrorAction Stop } |
            Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.TestAdoConnectionCommand' -ExpectedMessage "*$Member*"
        { Get-AdoProject -Connection ([pscustomobject] $Bag) -ErrorAction Stop } |
            Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoProjectCommand' -ExpectedMessage "*$Member*"
        $server.Requests.Count | Should -Be 0
    }

    It 'complete hand-made objects are still accepted' {
        $definition = [AdoToolkit.Core.Builds.AdoBuildDefinition]@{ Id = 44; Name = 'Piped'; TeamProject = 'Équipe Web'
            WebUrl = 'https://ado.example.test/ignored'; CollectionUri = (Get-AdoConnection).CollectionUri }
        @($definition | Get-AdoBuild).Count | Should -Be 0
        $server.Requests.ToArray()[0].Line | Should -Match 'builds\?definitions=44&'
    }
}
