BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
}
Describe 'Project command surface' -Tag 'S0-3' {
    AfterEach { Disconnect-Ado }
    It 'pages, filters before Top, caches results and completes without I/O' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = '{"value":[{"id":"11111111-1111-1111-1111-111111111111","name":"Alpha"}]}' },
            @{ Body = '{"value":[{"id":"22222222-2222-2222-2222-222222222222","name":"Équipe Web"}]}' },
            @{ Body = '{"value":[]}' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            $items = @(Get-AdoProject -Name 'Équi*' -Top 1 -Verbose 4>&1)
            @($items | Where-Object { $_ -is [AdoToolkit.Core.Connections.AdoProject] }).Count | Should -Be 1
            @($items | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] }).Count | Should -BeGreaterThan 0
            (Get-AdoProject -Name 'Équi*').Name | Should -Be 'Équipe Web'
            $server.Requests.Count | Should -Be 3
            $requests = $server.Requests.ToArray()
            $requests[1].Line | Should -Match '%24skip=1'
            $requests[0].Line | Should -Match 'api-version=6.0'
            $line = 'Connect-Ado -Project É'
            $completion = [System.Management.Automation.CommandCompletion]::CompleteInput($line, $line.Length, $null)
            $completion.CompletionMatches.CompletionText | Should -Contain "'Équipe Web'"
            $server.Requests.Count | Should -Be 3
            $result = Get-AdoConnection | Test-AdoConnection
            $result.Success | Should -BeTrue
            $result.ApiVersion | Should -Be '6.0'
            $server.Requests.Count | Should -Be 4
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'fails without a session connection or default profile' {
        $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
        # A default profile would connect implicitly, so point at a configuration that does not exist.
        $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '/config.json')
        try {
            Disconnect-Ado
            { Get-AdoProject -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoProjectCommand'
        }
        finally { $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig }
    }
}
