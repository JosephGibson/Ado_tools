BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    function Get-LogFixture {
        param([Parameter(Mandatory = $true)][string] $Name)
        Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Fixtures/$Name") -Raw -Encoding utf8
    }
}

Describe 'Saving build logs' -Tag 'S4-3' {
    AfterEach { Disconnect-Ado }

    It 'supports WhatIf with no requests or files and requires an existing FileSystem directory' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project Web -WarningAction SilentlyContinue | Out-Null
            @(Save-AdoBuildLog -BuildId 401 -LogId 11 -Tail 200 -Path $TestDrive -WhatIf).Count | Should -Be 0
            @(Get-ChildItem -LiteralPath $TestDrive -Force).Count | Should -Be 0
            { Save-AdoBuildLog -BuildId 401 -LogId 11 -Path Env: -WhatIf -ErrorAction Stop } | Should -Throw
            { Save-AdoBuildLog -BuildId 401 -LogId 11 -Path (Join-Path $TestDrive 'missing') -WhatIf -ErrorAction Stop } | Should -Throw
            $file = Join-Path $TestDrive 'existing.txt'
            Set-Content -LiteralPath $file -Value original
            { Save-AdoBuildLog -BuildId 401 -LogId 11 -Path $file -WhatIf -ErrorAction Stop } | Should -Throw
            { Save-AdoBuildLog -BuildId 0 -LogId 11 -Path $TestDrive } | Should -Throw
            { Save-AdoBuildLog -BuildId 401 -LogId 0 -Path $TestDrive } | Should -Throw
            { Save-AdoBuildLog -BuildId 401 -LogId 11 -Tail 0 -Path $TestDrive } | Should -Throw
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'runs the example pipeline, uses tail ranges and outputs FileInfo including an empty log' {
        $body = Get-LogFixture 'Rest/build-log-body.txt'
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-LogFixture 'Rest/build-definitions-first.json' },
            @{ Body = Get-LogFixture 'Rest/builds-first.json' },
            @{ Body = Get-LogFixture 'Timelines/multi-stage.json' },
            @{ Body = Get-LogFixture 'Rest/build-logs.json' },
            @{ Body = Get-LogFixture 'Rest/build-logs.json' },
            @{ Body = $body; ContentType = 'text/plain' },
            @{ Body = Get-LogFixture 'Rest/build-logs.json' },
            @{ Body = ''; ContentType = 'text/plain' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $old = Join-Path $TestDrive 'Build-401-Log-11.txt'
            Set-Content -LiteralPath $old -Value old
            $files = @(Get-AdoBuild -Definition 'Tâches' -Branch main -Latest -Result Failed |
                Get-AdoBuildFailure | Save-AdoBuildLog -Tail 200 -Path $TestDrive)
            $files.Count | Should -Be 2
            $files[0] | Should -BeOfType ([System.IO.FileInfo])
            $files.Name | Should -Be @('Build-401-Log-11.txt', 'Build-401-Log-12.txt')
            [Convert]::ToHexString([IO.File]::ReadAllBytes($files[0].FullName)) | Should -Be ([Convert]::ToHexString([Text.Encoding]::UTF8.GetBytes($body)))
            $files[1].Length | Should -Be 0
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 8
            $requests[5].Line | Should -Match '/logs/11\?startLine=800&endLine=999&api-version=6.0 '
            $requests[7].Line | Should -Match '/logs/12\?api-version=6.0 '
            $requests[5].Headers['Accept'] | Should -Be 'text/plain'
            @(Get-ChildItem -LiteralPath $TestDrive -Filter '*.tmp' -Force).Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'reports a missing log per input, checks provenance, and continues with a valid property-bound input' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-LogFixture 'Rest/builds-first.json' },
            @{ Body = Get-LogFixture 'Timelines/no-log.json' },
            @{ Body = Get-LogFixture 'Rest/build-logs.json' },
            @{ Body = Get-LogFixture 'Rest/build-logs.json' },
            @{ Body = 'synthetic'; ContentType = 'text/plain' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $missing = Get-AdoBuildFailure -BuildId 401
            $foreign = [pscustomobject]@{ BuildId = 401; LogId = 11; CollectionUri = [uri]'https://other.example.test/Collection' }
            $valid = [pscustomobject]@{ BuildId = 401; LogId = 11 }
            $failures = @()
            $files = @(@($missing, $foreign, $valid) | Save-AdoBuildLog -Path $TestDrive -ErrorAction SilentlyContinue -ErrorVariable failures)
            $files.Count | Should -Be 1
            $files[0] | Should -BeOfType ([System.IO.FileInfo])
            $failures.Count | Should -Be 2
            $failures[0].FullyQualifiedErrorId | Should -Be 'BuildLogNotAvailable,AdoToolkit.SaveAdoBuildLogCommand'
            $failures[1].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.SaveAdoBuildLogCommand'
            $server.Requests.Count | Should -Be 5
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'warns in French for an unknown count and downloads the full log' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-LogFixture 'Rest/build-logs.json' },
            @{ Body = 'synthetic'; ContentType = 'text/plain' }
        )
        $previous = [System.Threading.Thread]::CurrentThread.CurrentUICulture
        try {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo]'fr-CA'
            Connect-Ado -CollectionUrl $server.Uri -Project Web -WarningAction SilentlyContinue | Out-Null
            $warnings = @()
            Save-AdoBuildLog -BuildId 401 -LogId 13 -Tail 200 -Path $TestDrive -WarningVariable warnings -WarningAction SilentlyContinue | Out-Null
            $warnings.Count | Should -Be 1
            $warnings[0].Message | Should -Match 'nombre de lignes est inconnu'
            $server.Requests.ToArray()[1].Line | Should -Match '/logs/13\?api-version=6.0 '
        }
        finally {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = $previous
            Stop-FakeAdoServer -Server $server
        }
    }
}
