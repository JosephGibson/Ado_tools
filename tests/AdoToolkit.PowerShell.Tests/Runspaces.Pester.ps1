BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
}
Describe 'Runspace ownership' {
    It 'keeps connections independent after another runspace disconnects and closes' {
        $server = Start-FakeAdoServer
        $first = [powershell]::Create()
        $second = [powershell]::Create()
        try {
            foreach ($shell in @($first, $second)) {
                [void] $shell.AddScript({ param($Manifest) Import-Module -Name $Manifest; @(Get-AdoConnection).Count }).AddArgument($env:ADOTOOLKIT_MODULE_MANIFEST)
                $shell.Invoke()[0] | Should -Be 0
                $shell.Commands.Clear()
            }
            [void] $first.AddScript({ Connect-Ado -CollectionUrl 'https://first.example.test/Collection' | Out-Null })
            $first.Invoke()
            [void] $second.AddScript({ param($Uri) Connect-Ado -CollectionUrl $Uri -WarningAction SilentlyContinue | Out-Null }).AddArgument($server.Uri)
            $second.Invoke()
            $first.Commands.Clear()
            [void] $first.AddScript({ Disconnect-Ado; @(Get-AdoConnection).Count })
            $first.Invoke()[0] | Should -Be 0
            $first.Dispose()
            $second.Commands.Clear()
            [void] $second.AddScript({ Get-AdoConnection; Test-AdoConnection })
            $result = $second.Invoke()
            $second.HadErrors | Should -BeFalse
            $result[0].CollectionUri.AbsoluteUri | Should -Be $server.Uri
            $result[1].Success | Should -BeTrue
        }
        finally { $first.Dispose(); $second.Dispose(); Stop-FakeAdoServer -Server $server }
    }
}

