BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
}
Describe 'Localized authentication errors' -Tag 'S0-7' {
    It 'uses French resources and stable error IDs for an HTML 401' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Status = 401; ContentType = 'text/html'; Body = '<html>NEVER ECHO THIS</html>' },
            @{ Status = 401; ContentType = 'text/html'; Body = '<html>NEVER ECHO THIS</html>' }
        )
        try {
            $results = foreach ($culture in @('en-US', 'fr-CA')) {
                $shell = [powershell]::Create()
                try {
                    [void] $shell.AddScript({
                        param($Manifest, $Uri, $Culture)
                        [System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo]::GetCultureInfo($Culture)
                        Import-Module -Name $Manifest
                        Connect-Ado -CollectionUrl $Uri -WarningAction SilentlyContinue | Out-Null
                        try { Test-AdoConnection -ErrorAction Stop }
                        catch { [pscustomobject]@{ Id = $_.FullyQualifiedErrorId; Message = $_.ToString(); Culture = $PSUICulture } }
                    }).AddArgument($env:ADOTOOLKIT_MODULE_MANIFEST).AddArgument($server.Uri).AddArgument($culture)
                    $shell.Invoke()
                    $shell.Streams.Error.Count | Should -Be 0
                }
                finally { $shell.Dispose() }
            }
            $results[1].Culture | Should -Be 'fr-CA'
            $results[0].Id | Should -Be 'AdoAuthentication,AdoToolkit.TestAdoConnectionCommand'
            $results[1].Id | Should -Be $results[0].Id
            $results[1].Message | Should -Match 'authentification'
            $results[1].Message | Should -Not -Be $results[0].Message
            foreach ($result in $results) { $result.Message | Should -Not -Match 'NEVER ECHO|<html>' }
            $server.Requests.ToArray()[1].Headers['Accept-Language'] | Should -Be 'fr-CA'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
