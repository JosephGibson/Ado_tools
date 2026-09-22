BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
    function Get-Fixture {
        param([Parameter(Mandatory = $true)][string] $Name)
        Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Fixtures/Rest/$Name") -Raw -Encoding utf8
    }
    $plansBody = Get-Fixture 'testplans-first.json'
    $suitesFirst = Get-Fixture 'testsuites-first.json'
    $suiteTree = [System.Text.Json.Nodes.JsonNode]::Parse($suitesFirst)
    foreach ($suite in [System.Text.Json.Nodes.JsonNode]::Parse((Get-Fixture 'testsuites-last.json'))['value']) { $suiteTree['value'].Add($suite.DeepClone()) }
    $suitesBody = $suiteTree.ToJsonString()
    # Saves profile 'work' for the fake server as the default profile, so commands connect with it.
    function Set-DefaultsProfile {
        param([Parameter(Mandatory = $true)][string] $Uri, [hashtable] $Defaults = @{})
        Set-AdoProfile -Name work -CollectionUrl $Uri -DefaultProject 'Équipe Web' -DefaultProfile @Defaults -WarningAction SilentlyContinue | Out-Null
    }
}

AfterAll { $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig }

Describe 'Local profiles' -Tag 'S0-5' {
    BeforeEach {
        $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '/config.json')
        Disconnect-Ado
    }

    It 'does not create a configuration file for WhatIf' {
        Set-AdoProfile -Name sample -CollectionUrl 'https://ado.example.test/Collection' -DefaultProfile -WhatIf
        Test-Path -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH | Should -BeFalse
    }

    It 'preserves unspecified values and supports profile pipeline removal' {
        $savedProfile = Set-AdoProfile -Name sample -CollectionUrl 'https://ado.example.test/Collection///' -DefaultProject 'Équipe Web' -RequestTimeoutSeconds 123 -DefaultProfile
        $savedProfile.CollectionUri.AbsoluteUri | Should -Be 'https://ado.example.test/Collection'
        $updated = Set-AdoProfile -Name sample -RequestTimeoutSeconds 50
        $updated.DefaultProject | Should -Be 'Équipe Web'
        $updated.RequestTimeoutSeconds | Should -Be 50
        $connection = Connect-Ado
        $connection.DefaultProject | Should -Be 'Équipe Web'
        $before = [System.IO.File]::ReadAllBytes($env:ADOTOOLKIT_CONFIG_PATH)
        Get-AdoProfile -Name sample | Remove-AdoProfile -WhatIf
        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($env:ADOTOOLKIT_CONFIG_PATH)) | Should -Be ([Convert]::ToBase64String($before))
        Get-AdoProfile -Name sample | Remove-AdoProfile -Confirm:$false
        @(Get-AdoProfile).Count | Should -Be 0
        (Get-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Raw | ConvertFrom-Json).defaultProfile | Should -BeNullOrEmpty
    }

    It 'completes a profile name with a typographic apostrophe as one pasteable argument' {
        $name = "Équipe d$([char]0x2019)Alice"
        Set-AdoProfile -Name $name -CollectionUrl 'https://ado.example.test/Collection' | Out-Null
        $line = 'Connect-Ado -Profile É'
        $text = @([System.Management.Automation.CommandCompletion]::CompleteInput($line, $line.Length, $null).CompletionMatches.CompletionText)
        $text.Count | Should -Be 1
        $tokens = $null
        $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseInput("Connect-Ado -Profile $($text[0])", [ref] $tokens, [ref] $errors)
        $errors.Count | Should -Be 0
        $ast.EndBlock.Statements[0].PipelineElements[0].CommandElements[2].Value | Should -Be $name
    }

    It 'rejects a blank profile name as a parameter error without writing' {
        { Set-AdoProfile -Name ' ' -CollectionUrl 'https://ado.example.test/Collection' } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.SetAdoProfileCommand'
        { Remove-AdoProfile -Name ' ' -Confirm:$false } | Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.RemoveAdoProfileCommand'
        Test-Path -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH | Should -BeFalse
    }

    It 'reports a repeated property in the configuration file as a configuration error' {
        [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $env:ADOTOOLKIT_CONFIG_PATH))
        [System.IO.File]::WriteAllText($env:ADOTOOLKIT_CONFIG_PATH, '{"defaultProfile":"a","defaultProfile":"b"}')
        { Get-AdoProfile -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoProfileCommand'
        { Connect-Ado -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.ConnectAdoCommand'
    }

    It 'rejects a request timeout above one day without writing' {
        { Set-AdoProfile -Name sample -CollectionUrl 'https://ado.example.test/Collection' -RequestTimeoutSeconds 86401 } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.SetAdoProfileCommand'
        Test-Path -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH | Should -BeFalse
    }

    It 'refuses writes to a newer schema without modifying it' {
        [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $env:ADOTOOLKIT_CONFIG_PATH))
        Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Value '{"schemaVersion":2}'
        $before = Get-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Raw
        { Set-AdoProfile -Name sample -CollectionUrl 'https://ado.example.test/Collection' -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.SetAdoProfileCommand'
        Get-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Raw | Should -Be $before
    }

    It 'resolves explicit URL, named profile, and default profile' {
        Set-AdoProfile -Name sample -CollectionUrl 'https://ado.example.test/Collection' -DefaultProfile | Out-Null
        (Connect-Ado).CollectionUri.AbsoluteUri | Should -Be 'https://ado.example.test/Collection'
        (Connect-Ado -Profile sample -Project 'Override').DefaultProject | Should -Be 'Override'
        (Connect-Ado -CollectionUrl 'https://second.example.test/Other').CollectionUri.AbsoluteUri | Should -Be 'https://second.example.test/Other'
        Disconnect-Ado
        @(Get-AdoConnection).Count | Should -Be 0
    }
}

Describe 'Profile defaults for builds and test plans' {
    BeforeEach {
        $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '/config.json')
        Disconnect-Ado
    }
    AfterEach { Disconnect-Ado }

    It 'sets, shows and clears each default without touching the other settings' {
        $saved = Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/Collection' -DefaultBranch develop `
            -DefaultBuildDefinition Test_Plan -DefaultTestPlanId 812 -DefaultTestSuiteId 813
        $saved.DefaultBranch | Should -Be 'develop'
        $saved.DefaultBuildDefinition.Name | Should -Be 'Test_Plan'
        $saved.DefaultTestPlanId | Should -Be 812
        $saved.DefaultTestSuiteId | Should -Be 813
        $shown = Get-AdoProfile -Name work | Out-String
        $shown | Should -Match 'DefaultBranch\s+: develop'
        $shown | Should -Match 'DefaultBuildDefinition\s+: Test_Plan'
        $shown | Should -Match 'DefaultTestPlanId\s+: 812'
        $shown | Should -Match 'DefaultTestSuiteId\s+: 813'
        (Get-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Raw | ConvertFrom-Json).profiles.work.defaultBuildDefinition | Should -BeOfType ([string])

        $updated = Set-AdoProfile -Name work -DefaultBuildDefinition 42 -RequestTimeoutSeconds 50
        $updated.DefaultBuildDefinition.Id | Should -Be 42
        $updated.DefaultBranch | Should -Be 'develop'
        $updated.DefaultTestPlanId | Should -Be 812
        (Get-AdoProfile -Name work | Out-String) | Should -Match 'DefaultBuildDefinition\s+: 42'
        (Get-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Raw | ConvertFrom-Json).profiles.work.defaultBuildDefinition | Should -BeOfType ([long])

        $before = [System.IO.File]::ReadAllText($env:ADOTOOLKIT_CONFIG_PATH)
        { Set-AdoProfile -Name work -DefaultTestPlanId 0 } | Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.SetAdoProfileCommand'
        { Set-AdoProfile -Name work -DefaultTestSuiteId -5 } | Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.SetAdoProfileCommand'
        { Set-AdoProfile -Name work -DefaultBuildDefinition 0 -ErrorAction Stop } | Should -Throw -ErrorId 'AdoRequest,AdoToolkit.SetAdoProfileCommand'
        [System.IO.File]::ReadAllText($env:ADOTOOLKIT_CONFIG_PATH) | Should -Be $before

        $cleared = Set-AdoProfile -Name work -DefaultBranch '' -DefaultBuildDefinition $null -DefaultTestPlanId $null -DefaultTestSuiteId $null
        $cleared.DefaultBranch | Should -BeNullOrEmpty
        $cleared.DefaultBuildDefinition | Should -BeNullOrEmpty
        $cleared.DefaultTestPlanId | Should -BeNullOrEmpty
        $cleared.DefaultTestSuiteId | Should -BeNullOrEmpty
        $cleared.RequestTimeoutSeconds | Should -Be 50
        (Get-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Raw | ConvertFrom-Json).profiles.work.PSObject.Properties.Name |
            Should -Be @('collectionUrl', 'defaultProject', 'authentication', 'requestTimeoutSeconds')
    }

    It 'copies the defaults to connections made from the profile only' {
        Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/Collection' -DefaultBranch develop -DefaultBuildDefinition Test_Plan `
            -DefaultTestPlanId 812 -DefaultTestSuiteId 813 | Out-Null
        $connection = Connect-Ado -Profile work -Project 'Autre projet'
        $connection.DefaultProject | Should -Be 'Autre projet'
        $connection.DefaultBranch | Should -Be 'develop'
        $connection.DefaultBuildDefinition.Name | Should -Be 'Test_Plan'
        $connection.DefaultTestPlanId | Should -Be 812
        $connection.DefaultTestSuiteId | Should -Be 813
        $byUrl = Connect-Ado -CollectionUrl 'https://ado.example.test/Collection'
        $byUrl.DefaultBranch | Should -BeNullOrEmpty
        $byUrl.DefaultBuildDefinition | Should -BeNullOrEmpty
        $byUrl.DefaultTestPlanId | Should -BeNullOrEmpty
        $byUrl.DefaultTestSuiteId | Should -BeNullOrEmpty
    }

    It 'reports an invalid default in the file as a configuration error' {
        [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $env:ADOTOOLKIT_CONFIG_PATH))
        [System.IO.File]::WriteAllText($env:ADOTOOLKIT_CONFIG_PATH,
            '{"defaultProfile":"work","profiles":{"work":{"collectionUrl":"https://ado.example.test/Collection","defaultTestPlanId":0}}}')
        { Get-AdoProfile -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoProfileCommand'
        { Get-AdoTestSuite -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestSuiteCommand'
    }

    It 'Get-AdoBuild uses the profile definition and branch unless parameters are given' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = '{"value":[{"id":77,"name":"Test_Plan","path":"\\","revision":1,"queueStatus":"enabled"}]}' }
        )
        try {
            Set-DefaultsProfile -Uri $server.Uri -Defaults @{ DefaultBranch = 'develop'; DefaultBuildDefinition = 'Test_Plan' }
            @(Get-AdoBuild).Count | Should -Be 0
            @(Get-AdoBuild -Definition 42 -Branch main).Count | Should -Be 0
            @(Get-AdoBuild 43).Count | Should -Be 0
            $definition = [AdoToolkit.Core.Builds.AdoBuildDefinition]@{ Id = 44; Name = 'Piped'; TeamProject = 'Équipe Web'
                WebUrl = 'https://ado.example.test/ignored'; CollectionUri = (Get-AdoConnection).CollectionUri }
            @($definition | Get-AdoBuild -Branch 'refs/heads/release').Count | Should -Be 0
            @($definition | Get-AdoBuild).Count | Should -Be 0
            $lines = @($server.Requests.ToArray().Line)
            $lines.Count | Should -Be 6
            $lines[0] | Should -Match '/_apis/build/definitions\?'
            $lines[1] | Should -Match 'builds\?definitions=77&branchName=refs%2Fheads%2Fdevelop&'
            $lines[2] | Should -Match 'builds\?definitions=42&branchName=refs%2Fheads%2Fmain&'
            $lines[3] | Should -Match 'builds\?definitions=43&branchName=refs%2Fheads%2Fdevelop&'
            $lines[4] | Should -Match 'builds\?definitions=44&branchName=refs%2Fheads%2Frelease&'
            $lines[5] | Should -Match 'builds\?definitions=44&branchName=refs%2Fheads%2Fdevelop&'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoBuild without profile defaults has no branch filter and still requires -Definition' {
        $server = Start-FakeAdoServer
        try {
            Set-DefaultsProfile -Uri $server.Uri
            @(Get-AdoBuild -Definition 42).Count | Should -Be 0
            $server.Requests.ToArray()[0].Line | Should -Match 'builds\?definitions=42&statusFilter='
            { Get-AdoBuild -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoBuildCommand' -ExpectedMessage '*-Definition,*'
            $server.Requests.Count | Should -Be 1
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoBuildTestFailure selects the latest build of the profile definition and branch unless parameters are given' {
        $server = Start-FakeAdoServer
        try {
            Set-DefaultsProfile -Uri $server.Uri -Defaults @{ DefaultBranch = 'develop'; DefaultBuildDefinition = 42 }
            $options = @{ WarningAction = 'SilentlyContinue'; ErrorAction = 'Stop' }
            { Get-AdoBuildTestFailure @options } | Should -Throw -ErrorId 'AdoNotFound,AdoToolkit.GetAdoBuildTestFailureCommand'
            { Get-AdoBuildTestFailure -Definition 43 -Branch main @options } | Should -Throw -ErrorId 'AdoNotFound,AdoToolkit.GetAdoBuildTestFailureCommand'
            { Get-AdoBuildTestFailure -Result Failed @options } | Should -Throw -ErrorId 'AdoNotFound,AdoToolkit.GetAdoBuildTestFailureCommand'
            # A build ID and a piped build still select their own parameter sets. The fake server's
            # empty list is not a build, so the build ID read is a response format error.
            { Get-AdoBuildTestFailure 401 @options } | Should -Throw -ErrorId 'AdoResponseFormat,AdoToolkit.GetAdoBuildTestFailureCommand'
            $foreign = [AdoToolkit.Core.Builds.AdoBuild]@{ Id = 401; BuildNumber = 'Foreign'
                Definition = [AdoToolkit.Core.Builds.AdoBuildDefinitionRef]@{ Id = 42; Name = 'Foreign' }; TeamProject = 'Équipe Web'
                WebUrl = 'https://other.example.test/ignored'; CollectionUri = 'https://other.example.test/Collection' }
            { $foreign | Get-AdoBuildTestFailure @options } | Should -Throw -ErrorId 'AdoConnectionMismatch,AdoToolkit.GetAdoBuildTestFailureCommand'
            $lines = @($server.Requests.ToArray().Line)
            $lines.Count | Should -Be 4
            $lines[0] | Should -Match 'builds\?definitions=42&branchName=refs%2Fheads%2Fdevelop&statusFilter=completed&'
            $lines[1] | Should -Match 'builds\?definitions=43&branchName=refs%2Fheads%2Fmain&statusFilter=completed&'
            $lines[2] | Should -Match 'builds\?definitions=42&branchName=refs%2Fheads%2Fdevelop&statusFilter=completed&resultFilter=failed&'
            $lines[3] | Should -Match '/_apis/build/builds/401\?'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoBuildTestFailure without profile defaults has no branch filter and still requires a build or definition' {
        $server = Start-FakeAdoServer
        try {
            Set-DefaultsProfile -Uri $server.Uri
            $options = @{ WarningAction = 'SilentlyContinue'; ErrorAction = 'Stop' }
            { Get-AdoBuildTestFailure -Definition 42 @options } | Should -Throw -ErrorId 'AdoNotFound,AdoToolkit.GetAdoBuildTestFailureCommand'
            $server.Requests.ToArray()[0].Line | Should -Match 'builds\?definitions=42&statusFilter=completed&'
            { Get-AdoBuildTestFailure @options } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoBuildTestFailureCommand' -ExpectedMessage '*-Definition,*'
            $server.Requests.Count | Should -Be 1
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoTestCase reads the profile plan and suite unless parameters are given' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = $plansBody }, @{ Body = $suitesBody }, @{ Body = '{"value":[]}' },
            @{ Body = $plansBody }, @{ Body = $suitesBody }, @{ Body = '{"value":[]}' },
            @{ Body = $plansBody }, @{ Body = $suitesBody }, @{ Body = '{"value":[]}' }
        )
        try {
            Set-DefaultsProfile -Uri $server.Uri -Defaults @{ DefaultTestPlanId = 812; DefaultTestSuiteId = 814 }
            @(Get-AdoTestCase -WarningAction SilentlyContinue).Count | Should -Be 0
            @(Get-AdoTestCase -SuiteId 816 -WarningAction SilentlyContinue).Count | Should -Be 0
            Set-DefaultsProfile -Uri $server.Uri -Defaults @{ DefaultTestPlanId = 820; DefaultTestSuiteId = 999 }
            Disconnect-Ado
            @(Get-AdoTestCase -PlanId 812 -SuiteId 818 -WarningAction SilentlyContinue).Count | Should -Be 0
            # The profile's suite belongs to the profile's plan, so an explicit plan needs its own suite.
            { Get-AdoTestCase -PlanId 812 -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestCaseCommand' -ExpectedMessage '*-SuiteId.*'
            $lines = @($server.Requests.ToArray().Line)
            $lines.Count | Should -Be 9
            $lines[2] | Should -Match '/Plans/812/Suites/814/TestCase\?'
            $lines[5] | Should -Match '/Plans/812/Suites/816/TestCase\?'
            $lines[8] | Should -Match '/Plans/812/Suites/818/TestCase\?'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoTestCase without profile defaults still requires -PlanId and -SuiteId' {
        $server = Start-FakeAdoServer
        try {
            Set-DefaultsProfile -Uri $server.Uri
            { Get-AdoTestCase -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestCaseCommand' -ExpectedMessage '*-PlanId,*'
            { Get-AdoTestCase -SuiteId 814 -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestCaseCommand' -ExpectedMessage '*-PlanId,*'
            { Get-AdoTestCase -PlanId 812 -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestCaseCommand' -ExpectedMessage '*-SuiteId.*'
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoTestCase binds a call without plan or suite to BySuite and piped input to its own parameter set' {
        $server = Start-FakeAdoServer -Responses @(@{ Body = $plansBody }, @{ Body = $suitesBody }, @{ Body = '{"value":[]}' },
            @{ Body = '{"value":[]}' }, @{ Body = '{"value":[]}' }, @{ Body = $plansBody })
        try {
            Set-DefaultsProfile -Uri $server.Uri -Defaults @{ DefaultTestPlanId = 812; DefaultTestSuiteId = 814 }
            (Get-Command Get-AdoTestCase).DefaultParameterSet | Should -Be 'BySuite'
            @(Get-AdoTestCase -Recurse -Strict -WarningAction SilentlyContinue).Count | Should -Be 0
            $suite = [AdoToolkit.Core.TestManagement.AdoTestSuite]@{ Id = 816; PlanId = 812; Name = 'Connexion'
                SuitePath = [string[]] @('Tâches de régression', 'Connexion'); TeamProject = 'Équipe Web'; CollectionUri = (Get-AdoConnection).CollectionUri }
            @($suite | Get-AdoTestCase -WarningAction SilentlyContinue).Count | Should -Be 0
            @(1201 | Get-AdoTestCase -ErrorAction SilentlyContinue -WarningAction SilentlyContinue).Count | Should -Be 0
            $lines = @($server.Requests.ToArray().Line)
            @($lines -match '/TestCase\?' -replace '^.*/Plans/', '' -replace '\?.*$', '') |
                Should -Be @('812/Suites/814/TestCase', '812/Suites/816/TestCase', '812/Suites/818/TestCase', '812/Suites/816/TestCase')
            @($lines -match '^POST .*/_apis/wit/workitemsbatch\?').Count | Should -Be 1
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoTestSuite reads the profile plan, and its suite only with that plan, unless parameters are given' {
        $server = Start-FakeAdoServer -Responses @(@{ Body = $suitesFirst }, @{ Body = $suitesFirst }, @{ Body = $suitesFirst }, @{ Body = $suitesFirst })
        try {
            Set-DefaultsProfile -Uri $server.Uri -Defaults @{ DefaultTestPlanId = 812; DefaultTestSuiteId = 814 }
            @(Get-AdoTestSuite).Id | Should -Be @(814)
            @(Get-AdoTestSuite -SuiteId 815).Id | Should -Be @(815)
            Set-DefaultsProfile -Uri $server.Uri -Defaults @{ DefaultTestPlanId = 820 }
            Disconnect-Ado
            @(Get-AdoTestSuite -PlanId 812).Id | Should -Be @(813)
            $plan = [AdoToolkit.Core.TestManagement.AdoTestPlan]@{ Id = 812; Name = 'P'; RootSuiteId = 813; TeamProject = 'Équipe Web'
                CollectionUri = (Get-AdoConnection).CollectionUri }
            @($plan | Get-AdoTestSuite).Id | Should -Be @(813)
            $lines = @($server.Requests.ToArray().Line)
            $lines.Count | Should -Be 4
            foreach ($line in $lines) { $line | Should -Match '/_apis/testplan/Plans/812/suites\?' }
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Get-AdoTestSuite without profile defaults still requires -PlanId and starts from the root suite' {
        $server = Start-FakeAdoServer -Responses @(@{ Body = $suitesFirst })
        try {
            Set-DefaultsProfile -Uri $server.Uri
            { Get-AdoTestSuite -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestSuiteCommand' -ExpectedMessage '*-PlanId,*'
            @(Get-AdoTestSuite 812).Id | Should -Be @(813)
            $server.Requests.Count | Should -Be 1
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
