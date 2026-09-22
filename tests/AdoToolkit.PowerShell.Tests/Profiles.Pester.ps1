BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
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
